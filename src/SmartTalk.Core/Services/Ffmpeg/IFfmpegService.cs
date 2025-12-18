using Serilog;
using System.Diagnostics;
using System.Text;
using SmartTalk.Core.Ioc;
using System.Text.RegularExpressions;
using SmartTalk.Core.Services.Http;
using SmartTalk.Messages.Enums.STT;

namespace SmartTalk.Core.Services.Ffmpeg;

public interface IFfmpegService: IScopedDependency
{
    Task<byte[]> ConvertAmrToWavAsync(byte[] amrBytes, CancellationToken cancellationToken);

    Task<byte[]> ConvertWavToAmrAsync(byte[] wavBytes, string language, CancellationToken cancellationToken);
    
    Task<byte[]> ConvertMp4ToWavAsync(byte[] mp4Bytes, int? samplingRate = null, CancellationToken cancellationToken = default);
    
    Task<byte[]> ConvertMp3ToWavAsync(byte[] mp3Bytes, int? samplingRate = null, CancellationToken cancellationToken = default);

    Task<List<byte[]>> SplitAudioAsync(byte[] audioBytes, long secondsPerAudio, string fileExtension = "wav", CancellationToken cancellationToken = default);

    Task<string> GetAudioDurationAsync(byte[] wavBytes, CancellationToken cancellationToken);
    
    Task<byte[]> ConvertFileFormatAsync(byte[] file, TranscriptionFileType fileType, CancellationToken cancellationToken);
    
    Task<byte[]> SpiltAudioAsync(byte[] audioBytes, double startTime, double endTime, double paddingMs = 50, string byteFormat = "wav", CancellationToken cancellationToken = default);
    
    Task<byte[]> ConvertWavToULawAsync(byte[] wavBytes, CancellationToken cancellationToken);

    Task<byte[]> ConvertUlawWavToMp3Async(byte[] wavBytes, CancellationToken cancellationToken);
    
    Task<byte[]> Convert8KHzWavTo24KHzWavAsync(byte[] bytes, CancellationToken cancellationToken);
    
    Task MergeWavFilesToUniformFormat(List<string> wavFiles, string outputFile, CancellationToken cancellationToken);
}

public class FfmpegService : IFfmpegService
{
    private readonly ISmartiesHttpClientFactory _smartiesHttpClientFactory;

    public FfmpegService(ISmartiesHttpClientFactory smartiesHttpClientFactory)
    {
        _smartiesHttpClientFactory = smartiesHttpClientFactory;
    }

    public async Task<byte[]> ConvertAmrToWavAsync(byte[] amrBytes, CancellationToken cancellationToken)
    {
        Log.Information("Start converting AMR to WAV, the wav length is {Length}", amrBytes.Length);

        var fileName = Guid.NewGuid();

        await File.WriteAllBytesAsync(fileName + ".amr", amrBytes, cancellationToken).ConfigureAwait(false);

        if (!File.Exists(fileName + ".amr"))
            Log.Error("Amr file persisted failed");

        // ffmpeg -i input.wav -ar 8000 -ab 12.2k -ac 1 output.amr
        var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                Arguments = $" -i {fileName}.amr -ar 44100 -ac 2 {fileName}.wav",
            }
        };

        proc.OutputDataReceived += (a, e) => Log.Information("Output data {@Object} {@Output}", a, e);

        proc.Start();
        proc.BeginErrorReadLine();
        proc.BeginOutputReadLine();

        await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var wavBytes = await File.ReadAllBytesAsync(fileName + ".wav", cancellationToken).ConfigureAwait(false);

        return wavBytes;
    }

    public async Task<byte[]> ConvertWavToAmrAsync(byte[] wavBytes, string language, CancellationToken cancellationToken)
    {
        Log.Information("Start converting WAV to AMR, the wav length is {Length}", wavBytes.Length);

        var fileName = Guid.NewGuid();

        await File.WriteAllBytesAsync(fileName + ".wav", wavBytes, cancellationToken).ConfigureAwait(false);

        if (!File.Exists(fileName + ".wav"))
            Log.Error("Wav file persisted failed");

        // ffmpeg -i input.wav -ar 8000 -ab 12.2k -ac 1 output.amr
        var speed = language.ToUpper() == "ZH-HK" ? "1.1" : "1.0";

        var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                Arguments = $" -i {fileName}.wav -filter:a atempo={speed} -ar 8000 -ab 12.2k -ac 1 {fileName}.amr",
            }
        };

        proc.OutputDataReceived += (a, e) => Log.Information("Output data {@Object} {@Output}", a, e);

        proc.Start();
        proc.BeginErrorReadLine();
        proc.BeginOutputReadLine();

        await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var amrBytes = await File.ReadAllBytesAsync(fileName + ".amr", cancellationToken).ConfigureAwait(false);

        return amrBytes;
    }

    public async Task<byte[]> ConvertMp4ToWavAsync(byte[] mp4Bytes, int? samplingRate = null, CancellationToken cancellationToken = default)
    {
        var baseFileName = Guid.NewGuid().ToString();
        var inputFileName = $"{baseFileName}.mp4";
        var outputFileName = $"{baseFileName}.wav";

        try
        {
            Log.Information("Converting mp4 to wav, the mp4 length is {Length}", mp4Bytes.Length);

            await File.WriteAllBytesAsync(inputFileName, mp4Bytes, cancellationToken).ConfigureAwait(false);

            if (!File.Exists(inputFileName))
            {
                Log.Information("Converting mp4 to wav, persisted mp4 file failed");
                return Array.Empty<byte>();
            }

            using (var proc = new Process())
            {
                proc.StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    Arguments = samplingRate.HasValue
                        ? $"-i {inputFileName} -vn -acodec pcm_s16le -ar {samplingRate.Value} {outputFileName}"
                        : $"-i {inputFileName} -vn -acodec pcm_s16le {outputFileName}"
                };

                proc.OutputDataReceived += (_, e) => Log.Information("Converting mp4 to wav, {@Output}", e);

                proc.Start();
                proc.BeginErrorReadLine();
                proc.BeginOutputReadLine();

                await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(outputFileName))
                return await File.ReadAllBytesAsync(outputFileName, cancellationToken).ConfigureAwait(false);

            Log.Information("Converting mp4 to wav, failed to generate wav");

            return Array.Empty<byte>();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Converting mp4 to wav error occurred");
            return Array.Empty<byte>();
        }
        finally
        {
            Log.Information("Converting mp4 to wav finally deleting files");

            if (File.Exists(inputFileName)) File.Delete(inputFileName);
            if (File.Exists(outputFileName)) File.Delete(outputFileName);
        }
    }

    public async Task<byte[]> ConvertMp3ToWavAsync(byte[] mp3Bytes, int? samplingRate = null, CancellationToken cancellationToken = default)
    {
        var baseFileName = Guid.NewGuid().ToString();
        var inputFileName = $"{baseFileName}.mp3";
        var outputFileName = $"{baseFileName}.wav";

        try
        {
            Log.Information("Converting mp3 to wav, the mp3 length is {Length}", mp3Bytes.Length);

            await File.WriteAllBytesAsync(inputFileName, mp3Bytes, cancellationToken).ConfigureAwait(false);

            if (!File.Exists(inputFileName))
            {
                Log.Information("Converting mp3 to wav, persisted mp3 file failed");
                return Array.Empty<byte>();
            }

            using (var proc = new Process())
            {
                proc.StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    Arguments = samplingRate.HasValue
                        ? $"-i {inputFileName} -vn -acodec pcm_s16le -ar {samplingRate.Value} {outputFileName}"
                        : $"-i {inputFileName} -vn -acodec pcm_s16le {outputFileName}"
                };

                proc.OutputDataReceived += (_, e) => Log.Information("Converting mp3 to wav, {@Output}", e);

                proc.Start();
                proc.BeginErrorReadLine();
                proc.BeginOutputReadLine();

                await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(outputFileName))
                return await File.ReadAllBytesAsync(outputFileName, cancellationToken).ConfigureAwait(false);

            Log.Information("Converting mp3 to wav, failed to generate wav");

            return Array.Empty<byte>();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Converting mp3 to wav error occurred");
            return Array.Empty<byte>();
        }
        finally
        {
            Log.Information("Converting mp3 to wav finally deleting files");

            if (File.Exists(inputFileName)) File.Delete(inputFileName);
            if (File.Exists(outputFileName)) File.Delete(outputFileName);
        }
    }

    public async Task<List<byte[]>> SplitAudioAsync(byte[] audioBytes, long secondsPerAudio, string fileExtension = "wav", CancellationToken cancellationToken = default)
    {
        var audioDataList = new List<byte[]>();
        var baseFileName = Guid.NewGuid().ToString();
        var inputFileName = $"{baseFileName}.{fileExtension}";

        try
        {
            Log.Information("Splitting audio, the audio length is {Length}", audioBytes.Length);

            await File.WriteAllBytesAsync(inputFileName, audioBytes, cancellationToken).ConfigureAwait(false);

            if (!File.Exists(inputFileName))
            {
                Log.Error("Splitting audio, persisted failed");
                return audioDataList;
            }

            using (var proc = new Process())
            {
                proc.StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    Arguments = $"-i {inputFileName} -segment_time {secondsPerAudio} -f segment -c copy {baseFileName}-split-%03d.{fileExtension}"
                };

                proc.OutputDataReceived += (_, e) => Log.Information("Splitting audio, {@Output}", e);

                proc.Start();
                proc.BeginErrorReadLine();
                proc.BeginOutputReadLine();

                await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }

            var index = 0;
            string splitFileName;

            while (File.Exists(splitFileName = $"{baseFileName}-split-{index:D3}.{fileExtension}"))
            {
                audioDataList.Add(await File.ReadAllBytesAsync(splitFileName, cancellationToken).ConfigureAwait(false));

                File.Delete(splitFileName);

                index++;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Splitting audio error.");
        }
        finally
        {
            Log.Information("Splitting audio finally deleting files");

            if (File.Exists(inputFileName))
                File.Delete(inputFileName);
        }

        return audioDataList;
    }

    public async Task<string> GetAudioDurationAsync(byte[] wavBytes, CancellationToken cancellationToken)
    {
        Log.Information("Start converting WAV to AMR, the wav length is {Length}", wavBytes.Length);

        var fileName = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.wav");

        try
        {
            await File.WriteAllBytesAsync(fileName, wavBytes, cancellationToken).ConfigureAwait(false);

            if (!File.Exists(fileName))
                Log.Error("Wav file persisted failed");
        
            var ffmpegCmd = $"-i \"{fileName}\"";
        
            var processInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = ffmpegCmd,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(processInfo);
            
            if (process is null) 
            {
                Log.Error("Failed to start ffmpeg process.");
                return string.Empty;
            }
            
            var output = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var regex = new Regex(@"Duration:\s(\d{2}:\d{2}:\d{2}\.\d{2})");
            var match = regex.Match(output);
            
            return match.Success ? match.Groups[1].Value : string.Empty;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred while retrieving audio duration");
        }
        finally
        {
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
        }
        
        return string.Empty;
    }
    
    public async Task<byte[]> ConvertFileFormatAsync(byte[] file, TranscriptionFileType fileType, CancellationToken cancellationToken)
    {
        return fileType switch
        {
            TranscriptionFileType.Mp3 => await ConvertUlawWavToMp3Async(file, cancellationToken).ConfigureAwait(false),
            TranscriptionFileType.Wav => file,
            TranscriptionFileType.Mp4 => await ConvertMp4ToWavAsync(file, cancellationToken: cancellationToken).ConfigureAwait(false),
            _ => Array.Empty<byte>()
        };
    }
    
    public async Task<byte[]> SpiltAudioAsync(byte[] audioBytes, double startTime, double endTime, double paddingMs = 50, string byteFormat = "wav", CancellationToken cancellationToken = default)
    {
        try
        {
            var startTimeAdjusted = Math.Max(0, startTime - paddingMs);
            var endTimeAdjusted = endTime + paddingMs;

            var startTimeSpan = TimeSpan.FromMilliseconds(startTimeAdjusted);
            var endTimeSpan = TimeSpan.FromMilliseconds(endTimeAdjusted);

            var startTimeFormatted = startTimeSpan.ToString(@"hh\:mm\:ss\.fff");
            var endTimeFormatted = endTimeSpan.ToString(@"hh\:mm\:ss\.fff");

            using var inputStream = new MemoryStream(audioBytes);
            using var outputStream = new MemoryStream();

            var args = $"-i pipe:0 -ss {startTimeFormatted} -to {endTimeFormatted} -c copy -f {byteFormat} pipe:1";

            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = args,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi };
            proc.Start();

            await inputStream.CopyToAsync(proc.StandardInput.BaseStream, cancellationToken);
            proc.StandardInput.Close();

            await proc.StandardOutput.BaseStream.CopyToAsync(outputStream, cancellationToken);

            await proc.WaitForExitAsync(cancellationToken);

            var outputBytes = outputStream.ToArray();
            Log.Information("SpiltAudioAsync completed, output length: {Length}", outputBytes.Length);

            return outputBytes.Length > 0 ? outputBytes : null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SpiltAudioAsync error.");
            return null;
        }
    }
     
     public async Task<byte[]> ConvertWavToULawAsync(byte[] wavBytes, CancellationToken cancellationToken = default)
    {
        var baseFileName = Guid.NewGuid().ToString();
        var inputFileName = $"{baseFileName}.wav";
        var outputFileName = $"{baseFileName}_ulaw.wav";
        
        try
        {
            await File.WriteAllBytesAsync(inputFileName, wavBytes, cancellationToken);
        
            using (var proc = new Process())
            {
                proc.StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-y -i {inputFileName} -c:a pcm_mulaw -ar 8000 {outputFileName}",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                var errorBuilder = new StringBuilder();
                proc.ErrorDataReceived += (_, e) =>
                {
                    if (string.IsNullOrEmpty(e.Data)) return;
                    
                    errorBuilder.AppendLine(e.Data);
                    
                    Log.Error("FFmpeg Error: {Error}", e.Data);
                };

                proc.Start();
                proc.BeginErrorReadLine();
                proc.BeginOutputReadLine();

                await proc.WaitForExitAsync(cancellationToken);

                if (proc.ExitCode != 0)
                {
                    Log.Error("FFmpeg exited with code {ExitCode}: {Error}", proc.ExitCode, errorBuilder.ToString());
                    return [];
                }
            }

            return File.Exists(outputFileName) 
                ? await File.ReadAllBytesAsync(outputFileName, cancellationToken) 
                : [];
        }
        finally
        {
            try { File.Delete(inputFileName); } catch { /* Ignore */ }
            try { File.Delete(outputFileName); } catch { /* Ignore */ }
        }
    }
    
     public async Task<byte[]> ConvertUlawWavToMp3Async(byte[] wavBytes, CancellationToken cancellationToken = default)
     {
         var baseFileName = Guid.NewGuid().ToString();
         var inputFileName = $"{baseFileName}.wav";
         var outputFileName = $"{baseFileName}.mp3";

         try
         {
             Log.Information("Converting ulaw WAV(8kHz) to MP3(16kHz), input length: {Length}", wavBytes.Length);

             await File.WriteAllBytesAsync(inputFileName, wavBytes, cancellationToken).ConfigureAwait(false);

             if (!File.Exists(inputFileName))
             {
                 Log.Warning("Failed to persist ulaw WAV file");
                 return Array.Empty<byte>();
             }

             using (var proc = new Process())
             {
                 proc.StartInfo = new ProcessStartInfo
                 {
                     FileName = "ffmpeg",
                     RedirectStandardError = true,
                     RedirectStandardOutput = true,
                     UseShellExecute = false,
                     CreateNoWindow = true,
                     Arguments = $"-i {inputFileName} -ar 16000 -ac 1 -acodec libmp3lame -b:a 128k {outputFileName}"
                 };

                 proc.OutputDataReceived += (_, e) =>
                 {
                     if (!string.IsNullOrEmpty(e.Data))
                         Log.Information("ffmpeg output: {Data}", e.Data);
                 };

                 proc.ErrorDataReceived += (_, e) =>
                 {
                     if (!string.IsNullOrEmpty(e.Data))
                         Log.Warning("ffmpeg error: {Data}", e.Data);
                 };

                 proc.Start();
                 proc.BeginErrorReadLine();
                 proc.BeginOutputReadLine();

                 await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
             }

             if (File.Exists(outputFileName))
             {
                 var mp3Bytes = await File.ReadAllBytesAsync(outputFileName, cancellationToken).ConfigureAwait(false); 
                 Log.Information("Conversion ulaw WAV(8kHz) → MP3(16kHz) success, output size: {Length}", mp3Bytes.Length);
                 return mp3Bytes;
             }

             Log.Warning("Failed to generate mp3 file");
             return Array.Empty<byte>();
         }
         catch (Exception ex)
         {
             Log.Error(ex, "Error converting ulaw WAV(8kHz) to MP3(16kHz)");
             return Array.Empty<byte>();
         }
         finally
         {
             Log.Information("Cleaning up temporary files");

             if (File.Exists(inputFileName)) File.Delete(inputFileName);
             if (File.Exists(outputFileName)) File.Delete(outputFileName);
         }
     }
     
    public async Task<byte[]> Convert8KHzWavTo24KHzWavAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        var baseFileName = Guid.NewGuid().ToString();
        var inputFileName = $"{baseFileName}.wav";
        var outputFileName = $"{baseFileName}_out.wav";

        try
        {
            await File.WriteAllBytesAsync(inputFileName, bytes, cancellationToken).ConfigureAwait(false);

            if (!File.Exists(inputFileName))
            {
                Log.Warning("Failed to persist ulaw WAV file");
                return Array.Empty<byte>();
            }

            using (var proc = new Process())
            {
                proc.StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = $"-i {inputFileName} -ar 24000 {outputFileName}"
                };

                proc.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Log.Information("ffmpeg output: {Data}", e.Data);
                };

                proc.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Log.Warning("ffmpeg error: {Data}", e.Data);
                };

                proc.Start();
                proc.BeginErrorReadLine();
                proc.BeginOutputReadLine();

                await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(outputFileName))
            {
                var wavBytes = await File.ReadAllBytesAsync(outputFileName, cancellationToken).ConfigureAwait(false);
                return wavBytes;
            }

            return Array.Empty<byte>();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error converting ulaw WAV(8kHz) to WAV(24kHz)");
            return Array.Empty<byte>();
        }
        finally
        {
            if (File.Exists(inputFileName)) File.Delete(inputFileName);
            if (File.Exists(outputFileName)) File.Delete(outputFileName);
        }
    }
    
    public async Task MergeWavFilesToUniformFormat(List<string> wavFiles, string outputFile, CancellationToken cancellationToken)
    {
        if (wavFiles.Count == 0)
            throw new ArgumentException("没有 WAV 文件可合并");

        var listFile = Path.GetTempFileName();
        await File.WriteAllLinesAsync(listFile, wavFiles.Select(f => $"file '{f}'"), cancellationToken).ConfigureAwait(false);
        var args = $"-y -f concat -safe 0 -i \"{listFile}\" -ar 24000 -ac 1 -acodec pcm_s16le \"{outputFile}\"";
        RunFfmpeg(args);
        File.Delete(listFile);
    }
     
    private void RunFfmpeg(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)!;
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var err = process.StandardError.ReadToEnd();
            throw new Exception($"ffmpeg 执行失败：{err}");
        }
    }    
}