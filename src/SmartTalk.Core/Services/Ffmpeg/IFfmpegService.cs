using Serilog;
using System.Diagnostics;
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
    
    Task<List<byte[]>> SpiltAudioAsync(byte[] audioBytes, double startTime, double endTime, CancellationToken cancellationToken);
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
            TranscriptionFileType.Wav => file,
            TranscriptionFileType.Mp4 => await ConvertMp4ToWavAsync(file, cancellationToken: cancellationToken).ConfigureAwait(false),
            _ => Array.Empty<byte>()
        };
    }
    
     public async  Task<List<byte[]>> SpiltAudioAsync(byte[] audioBytes, double startTime, double endTime, CancellationToken cancellationToken)
    {
        var audioDataList = new List<byte[]>();
        var baseFileName = Guid.NewGuid().ToString();
        var inputFileName = $"{baseFileName}.wav";
        var outputFileName = $"{baseFileName}-spilt.wav";

        var startTimeSpan = TimeSpan.FromMilliseconds(startTime);
        var endTimeSpan = TimeSpan.FromMilliseconds(endTime);

        var startTimeFormatted = startTimeSpan.ToString(@"hh\:mm\:ss\.fff");
        var endTimeFormatted = endTimeSpan.ToString(@"hh\:mm\:ss\.fff");

        try
        {
            Log.Information("According stareTime Splitting audio, the audio length is {Length}", audioBytes.Length);

            await File.WriteAllBytesAsync(inputFileName, audioBytes, cancellationToken).ConfigureAwait(false);

            if (!File.Exists(inputFileName))
            {
                Log.Error("Splitting audio, persisted failed");
                return audioDataList;
            }

            var spiltArguments =
                $"-i {inputFileName} -ss {startTimeFormatted} -to {endTimeFormatted} -c copy {outputFileName}";

            Log.Information("spilt command arguments: {spiltArguments}", spiltArguments);

            using (var proc = new Process())
            {
                proc.StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    Arguments = spiltArguments
                };
                proc.OutputDataReceived += (_, e) => Log.Information("Splitting audio, {@Output}", e);

                proc.Start();
                proc.BeginErrorReadLine();
                proc.BeginOutputReadLine();

                await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(outputFileName))
            {
                audioDataList.Add(await File.ReadAllBytesAsync(outputFileName, cancellationToken)
                    .ConfigureAwait(false));

                File.Delete(outputFileName);
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
}