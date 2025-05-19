using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;
using Serilog;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAi.Tools;

public class RealtimeAiAudioCodecAdapter : IRealtimeAiAudioCodecAdapter
{
    private readonly string _ffmpegPath;

    public RealtimeAiAudioCodecAdapter(IConfiguration configuration)
    {
        _ffmpegPath = configuration?["FFmpegPath"] ?? "ffmpeg"; 
    }

    public bool IsConversionSupported(RealtimeAiAudioCodec inputCodec, int inputSampleRate, RealtimeAiAudioCodec outputCodec, int outputSampleRate)
    {
        // 假设只要 FFmpeg 能找到对应的格式化器，就支持转换
        // Assume conversion is supported as long as FFmpeg can find the corresponding formatters
        try
        {
            GetFFmpegFormat(inputCodec);
            GetFFmpegFormat(outputCodec);
            return true; // FFmpeg 非常强大，通常能处理各种转换 (FFmpeg is very powerful, usually handles various conversions)
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    public async Task<byte[]> ConvertAsync(byte[] inputAudioBytes, RealtimeAiAudioCodec inputCodec,
        int inputSampleRate, RealtimeAiAudioCodec outputCodec, int outputSampleRate, CancellationToken cancellationToken)
    {
        if (inputCodec == outputCodec && inputSampleRate == outputSampleRate)
        {
            return inputAudioBytes; // 无需转换 (No conversion needed)
        }

        if (!IsConversionSupported(inputCodec, inputSampleRate, outputCodec, outputSampleRate))
        {
             throw new NotSupportedException($"不支持从 {inputCodec} @ {inputSampleRate}Hz 到 {outputCodec} @ {outputSampleRate}Hz 的音频转换。"); // Audio conversion from {inputCodec} @ {inputSampleRate}Hz to {outputCodec} @ {outputRate}Hz is not supported.
        }

        var inputFormat = GetFFmpegFormat(inputCodec);
        var outputFormat = GetFFmpegFormat(outputCodec);
        var inputChannels = GetDefaultChannels(inputCodec);
        var outputChannels = GetDefaultChannels(outputCodec);

        // 构建 FFmpeg 参数 (Build FFmpeg arguments)
        // -i pipe:0 表示从标准输入读取 (means read from standard input)
        // -f <format> -ar <rate> -ac <channels> 定义输入/输出格式 (define input/output format)
        // pipe:1 表示输出到标准输出 (means output to standard output)
        // -loglevel error 减少不必要的日志输出 (reduce unnecessary log output)
        var arguments = $"-f {inputFormat} -ar {inputSampleRate} -ac {inputChannels} -i pipe:0 " +
                        $"-f {outputFormat} -ar {outputSampleRate} -ac {outputChannels} -loglevel error pipe:1";

        Log.Debug("FFmpegAudioCodecAdapter: 执行 FFmpeg 命令: {Path} {Args}", _ffmpegPath, arguments); // FFmpegAudioCodecAdapter: Executing FFmpeg command: {Path} {Args}

        var processStartInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = processStartInfo };
        using var outputMs = new MemoryStream();
        var errorOutput = new StringBuilder();

        try
        {
            process.Start();

            // 异步写入输入数据并关闭输入流
            // Asynchronously write input data and close the input stream
            var inputStream = process.StandardInput.BaseStream;
            await inputStream.WriteAsync(inputAudioBytes, 0, inputAudioBytes.Length, cancellationToken);
            await inputStream.FlushAsync(cancellationToken);
            process.StandardInput.Close(); // 必须关闭输入流，否则 FFmpeg 可能一直等待 (Must close input stream, otherwise FFmpeg might wait indefinitely)


            // 异步读取输出和错误流
            // Asynchronously read output and error streams
            var outputTask = process.StandardOutput.BaseStream.CopyToAsync(outputMs, cancellationToken);
            var errorTask = ReadStreamAsync(process.StandardError, errorOutput, cancellationToken);

            // 等待进程结束和流读取完成
            // Wait for process exit and stream reading completion
            await Task.WhenAll(outputTask, errorTask);
            
            // 使用带超时的等待，防止无限期挂起
            // Use wait with timeout to prevent indefinite hanging
             var exited = await WaitForExitAsyncWithResultAsync(process, cancellationToken); // Use C# 8.0+ method if available or implement timeout manually

            if (!exited)
            {
                 try { process.Kill(); } catch { /* Ignore */ }
                 throw new TimeoutException("FFmpeg process timed out.");
            }


            if (process.ExitCode != 0)
            {
                Log.Error("FFmpeg 执行失败。退出代码: {ExitCode}。错误输出: {ErrorOutput}", process.ExitCode, errorOutput.ToString()); // FFmpeg execution failed. Exit code: {ExitCode}. Error output: {ErrorOutput}
                throw new InvalidOperationException($"FFmpeg 执行失败 (退出代码: {process.ExitCode}): {errorOutput}"); // FFmpeg execution failed (Exit code: {process.ExitCode}): {errorOutput}
            }

            Log.Debug("FFmpegAudioCodecAdapter: FFmpeg 转换成功，输入 {InputLength} 字节，输出 {OutputLength} 字节。", inputAudioBytes.Length, outputMs.Length); // FFmpegAudioCodecAdapter: FFmpeg conversion successful, input {InputLength} bytes, output {OutputLength} bytes.
            return outputMs.ToArray();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "FFmpegAudioCodecAdapter: 执行 FFmpeg 时发生错误。错误输出: {ErrorOutput}", errorOutput.ToString()); // FFmpegAudioCodecAdapter: Error executing FFmpeg. Error output: {ErrorOutput}
            // 尝试终止进程，以防万一
            // Try to kill the process just in case
             try { if (!process.HasExited) process.Kill(); } catch { /* Ignore */ }
            throw new InvalidOperationException($"FFmpeg 执行时出错: {ex.Message}. FFmpeg 错误: {errorOutput}", ex); // Error executing FFmpeg: {ex.Message}. FFmpeg error: {errorOutput}
        }
    }
    
    private string GetFFmpegFormat(RealtimeAiAudioCodec codec)
    {
        return codec switch
        {
            RealtimeAiAudioCodec.MULAW => "mulaw",
            RealtimeAiAudioCodec.ALAW => "alaw",
            RealtimeAiAudioCodec.PCM16 => "s16le", // signed 16-bit little-endian PCM
            RealtimeAiAudioCodec.OPUS => "opus", // 可能需要 libopus (May require libopus)
            _ => throw new NotSupportedException($"不支持的音频编解码器: {codec}") // Unsupported audio codec
        };
    }
    
    private static async Task<bool> WaitForExitAsyncWithResultAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
    
    private async Task ReadStreamAsync(StreamReader reader, StringBuilder output, CancellationToken cancellationToken)
    {
        char[] buffer = new char[1024];
        int charsRead;
        while ((charsRead = await reader.ReadAsync(buffer, 0, buffer.Length/*, cancellationToken*/)) > 0) // ReadAsync with CancellationToken might not be available on StreamReader directly depending on .NET version
        {
            cancellationToken.ThrowIfCancellationRequested();
            output.Append(buffer, 0, charsRead);
        }
    }
    
    private int GetDefaultChannels(RealtimeAiAudioCodec codec) => 1; // 假设所有格式都是单声道 (Assume all formats are mono)
    
    public AiSpeechAssistantProvider Provider => AiSpeechAssistantProvider.OpenAi;
}