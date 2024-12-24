using Serilog;
using System.Diagnostics;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.FilesSynchronize;

namespace SmartTalk.Core.Services.FilesSynchronize;

public interface IFilesSynchronizeService : IScopedDependency
{
    Task SynchronizeFilesAsync(SynchronizeFilesCommand command, CancellationToken cancellationToken);
}

public class FilesSynchronizeService : IFilesSynchronizeService
{
    public async Task SynchronizeFilesAsync(SynchronizeFilesCommand command, CancellationToken cancellationToken)
    {
        const string localPath = "/Users/travis/Documents/test-rc/";
        const string privateKeyPath = "/Users/travis/Documents/travis.pem";
        
        var rsyncCommand = $"-avz --progress -e \"ssh -i {privateKeyPath}\" \"{command.Source.User}@{command.Source.Server}:{command.Source.Path}\" \"{localPath}\"";
        
        Log.Information($"开始下载文件到本地目录：{command.Source.Path} -> {localPath}");
        
        var downloadSuccess = await ExecuteCommandAsync("/opt/homebrew/bin/rsync", rsyncCommand, cancellationToken).ConfigureAwait(false);
        
        if (!downloadSuccess)
        {
            Log.Error("下载文件失败，终止后续上传任务。");
            return;
        }
    
        Log.Information("文件下载完成，开始上传到目标服务器...");
        
        var tasks = command.Destinations.Select(destination => SyncServerDataAsync(localPath, destination, cancellationToken));
        
        await Task.WhenAll(tasks).ConfigureAwait(false);
    
        Log.Information("所有同步任务执行完成");
    }
    
    private async Task<bool> SyncServerDataAsync(string tempPath, SynchronizeFilesDestinationData destination, CancellationToken cancellationToken)
    {
        var uploadSuccess = await UploadDataToServerAsync(tempPath, destination, cancellationToken).ConfigureAwait(false);
        
        if (!uploadSuccess)
        {
            Log.Error("上传文件失败！！！");
            return false;
        }
        
        Log.Information("上传成功，准备重启服务...");
        
        return await ReloadServerDataAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> UploadDataToServerAsync(string tempPath, SynchronizeFilesDestinationData destination, CancellationToken cancellationToken)
    {
        var rsyncCommand = BuildRsyncCommand(tempPath, destination);
    
        Log.Information($"开始上传：{tempPath} -> {destination.Path}");
    
        return await ExecuteCommandAsync("/opt/homebrew/bin/rsync", rsyncCommand, cancellationToken).ConfigureAwait(false);
    }
    
    private string BuildRsyncCommand(string tempPath, SynchronizeFilesDestinationData destination)
    {
        const string privateKeyPath = "/Users/travis/Documents/travis.pem";
        
        var excludeArgs = string.Join(" ", destination.ExcludeFiles.Select(file => $"--exclude=\"{file}\""));
        
        return $"-e \"ssh -i {privateKeyPath}\" -avz --chown=asterisk:asterisk --checksum --delete --progress {excludeArgs} \"{tempPath}\" \"{destination.User}@{destination.Server}:{destination.Path}\"";
    }

    private async Task<bool> ReloadServerDataAsync(SynchronizeFilesDestinationData destination, CancellationToken cancellationToken)
    {
        const string privateKeyPath = "/Users/travis/Documents/travis.pem";
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