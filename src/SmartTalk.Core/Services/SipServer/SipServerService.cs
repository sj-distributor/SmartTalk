using Serilog;
using System.Diagnostics;
using AutoMapper;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.SipServer;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Settings.SipServer;
using SmartTalk.Messages.Commands.SipServer;

namespace SmartTalk.Core.Services.SipServer;

public partial interface ISipServerService : IScopedDependency
{
    Task BackupSipServerDataAsync(BackupSipServerDataCommand command, CancellationToken cancellationToken);
}

public partial class SipServerService : ISipServerService
{
    private readonly IMapper _mapper;
    private readonly SipServerSetting _sipServerSetting;
    private readonly SipServerDataProvider _sipServerDataProvider;
    private readonly SmartTalkBackgroundJobClient _smartTalkBackgroundJobClient;
    
    public SipServerService(IMapper mapper, SipServerSetting sipServerSetting, SipServerDataProvider sipServerDataProvider, SmartTalkBackgroundJobClient smartTalkBackgroundJobClient)
    {
        _mapper = mapper;
        _sipServerSetting = sipServerSetting;
        _sipServerDataProvider = sipServerDataProvider;
        _smartTalkBackgroundJobClient = smartTalkBackgroundJobClient;
    }
    
    public async Task BackupSipServerDataAsync(BackupSipServerDataCommand command, CancellationToken cancellationToken)
    {
        var hostServers = await _sipServerDataProvider.GetAllSipHostServersAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        foreach (var hostServer in hostServers)
        {
            _smartTalkBackgroundJobClient.Enqueue(() => ProcessServersBackupAsync(hostServer, cancellationToken), HangfireConstants.InternalHostingSipServer);
        }
    }

    public async Task ProcessServersBackupAsync(SipHostServer server, CancellationToken cancellationToken)
    {
        var localPath = CreateTempFolder();

        var privateKeyPath = await GeneratePrivateKeyTempPathAsync(cancellationToken).ConfigureAwait(false);
        
        var rsyncCommand = $"-avz --progress -e \"ssh -i {privateKeyPath}\" \"{server.ServerPath}\" \"{localPath}\"";
        
        Log.Information("开始下载文件到本地目录...");
        
        var downloadSuccess = await ExecuteCommandAsync("rsync", rsyncCommand, cancellationToken).ConfigureAwait(false);
        
        if (!downloadSuccess)
        {
            Log.Error("下载文件失败，终止后续上传任务。");
            return;
        }
    
        Log.Information("文件下载完成，开始上传到目标服务器...");
        
        var tasks = server.BackupServers.Select(backupServer => SyncServerDataAsync(privateKeyPath, localPath, backupServer, cancellationToken));
        
        await Task.WhenAll(tasks).ConfigureAwait(false);
    
        Log.Information("所有同步任务执行完成");
    }

    private string CreateTempFolder()
    {
        var tempDirectory = Path.GetTempPath();
        var fullFolderPath = Path.Combine(tempDirectory, "AsteriskBackup");

        try
        {
            if (Directory.Exists(fullFolderPath))
            {
                Directory.Delete(fullFolderPath, true);
                Log.Information($"已删除已存在的目录: {fullFolderPath}");
            }
            
            Directory.CreateDirectory(fullFolderPath);
            Log.Information($"临时文件夹已创建: {fullFolderPath}");
        }
        catch (Exception ex)
        {
            Log.Error($"创建临时文件夹时出错: {ex.Message}");
        }

        return $"{fullFolderPath}/";
    }
    
    private async Task<string> GeneratePrivateKeyTempPathAsync(CancellationToken cancellationToken)
    {
        var privateKeyTempPath = Path.Combine(Path.GetTempPath(), "temp_key");

        try
        {
            if (File.Exists(privateKeyTempPath)) File.Delete(privateKeyTempPath);
            
            if (string.IsNullOrEmpty(_sipServerSetting.PrivateKey)) throw new Exception("key should not be null");
        
            await File.WriteAllTextAsync(privateKeyTempPath, _sipServerSetting.PrivateKey, cancellationToken).ConfigureAwait(false);
        
            File.SetAttributes(privateKeyTempPath, FileAttributes.ReadOnly);

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                var chmodCommand = $"chmod 400 \"{privateKeyTempPath}\"";

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"{chmodCommand}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();

                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("Failed to set permissions for the private key file.");
                }
            }
        }
        catch (Exception e)
        {
            Log.Error($"文件操作失败：{e.Message}");
            throw;
        }

        return privateKeyTempPath;
    }
    
    private async Task<bool> SyncServerDataAsync(string privateKeyPath, string localPath, SipBackupServer backupServer, CancellationToken cancellationToken)
    {
        var uploadSuccess = await UploadDataToServerAsync(privateKeyPath, localPath, backupServer, cancellationToken).ConfigureAwait(false);
        
        if (!uploadSuccess)
        {
            Log.Error("上传文件失败！！！");
            return false;
        }
        
        Log.Information("上传成功，准备重启服务...");
        
        return await ReloadServerDataAsync(privateKeyPath, backupServer, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> UploadDataToServerAsync(string privateKeyPath, string localPath, SipBackupServer backupServer, CancellationToken cancellationToken)
    {
        var rsyncCommand = BuildRsyncCommand(privateKeyPath, localPath, backupServer);
    
        Log.Information("开始上传...");
    
        return await ExecuteCommandAsync("rsync", rsyncCommand, cancellationToken).ConfigureAwait(false);
    }
    
    private string BuildRsyncCommand(string privateKeyPath, string tempPath, SipBackupServer backupServer)
    {
        if (string.IsNullOrEmpty(backupServer.ExcludeFiles)) return string.Empty;
        
        var excludeFiles = backupServer.ExcludeFiles.Split(",").ToList();
        
        var excludeArgs = string.Join(" ", excludeFiles.Select(file => $"--exclude=\"{file}\""));
        
        return $"-e \"ssh -i {privateKeyPath}\" -avz --chown=asterisk:asterisk --checksum --delete --progress {excludeArgs} \"{tempPath}\" \"{backupServer.UserName}@{backupServer.ServerIp}:{backupServer.DestinationPath}\"";
    }

    private async Task<bool> ReloadServerDataAsync(string privateKeyPath, SipBackupServer backupServer, CancellationToken cancellationToken)
    {
        const string reloadScriptPath = "/root/asterisk-reload.sh";
        
        var sshCommand = $"-i {privateKeyPath} {backupServer.UserName}@{backupServer.ServerIp} \"bash {reloadScriptPath}\"";
        
        Log.Information($"执行服务重启命令...");
        
        var success = await ExecuteCommandAsync("/usr/bin/ssh", sshCommand, cancellationToken).ConfigureAwait(false);
        
        Log.Information($"远程服务器脚本执行 {(success ? "成功" : "失败")}。");

        return success;
    }
    
    private async Task<bool> ExecuteCommandAsync(string command, string arguments, CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        
        Log.Information($"Execute command: {command} {arguments}");
    
        try
        {
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
    
            if (process.ExitCode == 0)
            {
                Log.Information($"执行成功！输出：{output}");
                return true;
            }
    
            Log.Error($"执行失败！错误：{error}");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error($"执行出现异常！{ex.Message}");
            return false;
        }
    }
}