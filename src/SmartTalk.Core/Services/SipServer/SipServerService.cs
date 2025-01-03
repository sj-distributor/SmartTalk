using System.Diagnostics;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.SipServer;
using SmartTalk.Messages.Commands.SipServer;

namespace SmartTalk.Core.Services.SipServer;

public interface ISipServerService : IScopedDependency
{
    Task BackupSipServerDataAsync(BackupSipServerDataCommand command, CancellationToken cancellationToken);
}

public class SipServerService : ISipServerService
{
    private readonly SipServerSetting _sipServerSetting;
    
    public SipServerService(SipServerSetting sipServerSetting)
    {
        _sipServerSetting = sipServerSetting;
    }
    
    public async Task BackupSipServerDataAsync(BackupSipServerDataCommand command, CancellationToken cancellationToken)
    {
        command = UseDefaultBackupSettingIfRequired(command);

        var localPath = CreateTempFolder();

        var privateKeyPath = await GeneratePrivateKeyTempPathAsync(cancellationToken).ConfigureAwait(false);
        
        var rsyncCommand = $"-avz --progress -e \"ssh -i {privateKeyPath}\" \"{command.Source.ServerPath}\" \"{localPath}\"";
        
        Log.Information("开始下载文件到本地目录...");
        
        var downloadSuccess = await ExecuteCommandAsync("rsync", rsyncCommand, cancellationToken).ConfigureAwait(false);
        
        if (!downloadSuccess)
        {
            Log.Error("下载文件失败，终止后续上传任务。");
            return;
        }
    
        Log.Information("文件下载完成，开始上传到目标服务器...");
        
        var tasks = command.Destinations.Select(destination => SyncServerDataAsync(privateKeyPath, localPath, destination, cancellationToken));
        
        await Task.WhenAll(tasks).ConfigureAwait(false);
    
        Log.Information("所有同步任务执行完成");
    }
    
    private BackupSipServerDataCommand UseDefaultBackupSettingIfRequired(BackupSipServerDataCommand command)
    {
        if (command.Source != null && command.Destinations != null) return command;
        
        return new BackupSipServerDataCommand
        {
            Source = new BackupSipServerData
            {
                ServerPath = _sipServerSetting.Source
            },
            Destinations = _sipServerSetting.Destinations.Select(destination => new BackupSipServerDestinationData
            {
                ServerPath = destination
            }).ToList()
        };
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
        var privateKeyTempPath = Path.Combine(Path.GetTempPath(), "temp_key.pem");

        try
        {
            if (File.Exists(privateKeyTempPath)) File.Delete(privateKeyTempPath); 
        
            await File.WriteAllTextAsync(privateKeyTempPath, _sipServerSetting.PrivateKey, cancellationToken).ConfigureAwait(false);
        
            File.SetAttributes(privateKeyTempPath, FileAttributes.ReadOnly);

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                var chmodCommand = $"chmod 600 \"{privateKeyTempPath}\"";

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

                return privateKeyTempPath;
            }
        }
        catch (Exception e)
        {
            Log.Error($"文件操作失败：{e.Message}");
            throw;
        }

        return privateKeyTempPath;
    }
    
    private async Task<bool> SyncServerDataAsync(string privateKeyPath, string localPath, BackupSipServerDestinationData destination, CancellationToken cancellationToken)
    {
        var uploadSuccess = await UploadDataToServerAsync(privateKeyPath, localPath, destination, cancellationToken).ConfigureAwait(false);
        
        if (!uploadSuccess)
        {
            Log.Error("上传文件失败！！！");
            return false;
        }
        
        Log.Information("上传成功，准备重启服务...");
        
        return await ReloadServerDataAsync(privateKeyPath, destination, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> UploadDataToServerAsync(string privateKeyPath, string localPath, BackupSipServerDestinationData destination, CancellationToken cancellationToken)
    {
        var rsyncCommand = BuildRsyncCommand(privateKeyPath, localPath, destination);
    
        Log.Information("开始上传...");
    
        return await ExecuteCommandAsync("rsync", rsyncCommand, cancellationToken).ConfigureAwait(false);
    }
    
    private string BuildRsyncCommand(string privateKeyPath, string tempPath, BackupSipServerDestinationData destination)
    {
        var excludeArgs = string.Join(" ", destination.ExcludeFiles.Select(file => $"--exclude=\"{file}\""));
        
        return $"-e \"ssh -i {privateKeyPath}\" -avz --chown=asterisk:asterisk --checksum --delete --progress {excludeArgs} \"{tempPath}\" \"{destination.User}@{destination.Server}:{destination.Path}\"";
    }

    private async Task<bool> ReloadServerDataAsync(string privateKeyPath, BackupSipServerDestinationData destination, CancellationToken cancellationToken)
    {
        const string reloadScriptPath = "/root/asterisk-reload.sh";
        
        var sshCommand = $"-i {privateKeyPath} {destination.User}@{destination.Server} \"bash {reloadScriptPath}\"";
        
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