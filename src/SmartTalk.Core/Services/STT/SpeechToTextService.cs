using Serilog;
using AutoMapper;
using System.Text;
using OpenAI.Audio;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Extensions;
using SmartTalk.Messages.Dto.STT;
using SmartTalk.Messages.Enums.STT;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Settings.OpenAi;

namespace SmartTalk.Core.Services.STT;

public interface ISpeechToTextService : IScopedDependency
{
    Task<string> SpeechToTextAsync(
        byte[] file, TranscriptionLanguage? language = null, TranscriptionFileType fileType = TranscriptionFileType.Wav,
        TranscriptionResponseFormat responseFormat = TranscriptionResponseFormat.Vtt, string prompt = null, CancellationToken cancellationToken = default);
}

public class SpeechToTextService : ISpeechToTextService
{
    private readonly IMapper _mapper;
    private readonly IFfmpegService _ffmpegService;
    private readonly OpenAiSettings _openAiSettings;
    private readonly ISmartiesHttpClientFactory _httpClientFactory;

    public SpeechToTextService(IMapper mapper, IFfmpegService ffmpegService, OpenAiSettings openAiSettings, ISmartiesHttpClientFactory httpClientFactory)
    {
        _mapper = mapper;
        _ffmpegService = ffmpegService;
        _openAiSettings = openAiSettings;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> SpeechToTextAsync(
        byte[] file, TranscriptionLanguage? language = null,
        TranscriptionFileType fileType = TranscriptionFileType.Wav,
        TranscriptionResponseFormat responseFormat = TranscriptionResponseFormat.Vtt,
        string prompt = null,
        CancellationToken cancellationToken = default)
    {
        if (file == null) return null;

        var audioBytes = await _ffmpegService.ConvertFileFormatAsync(file, fileType, cancellationToken).ConfigureAwait(false);

        var splitAudios = await _ffmpegService.SplitAudioAsync(audioBytes, secondsPerAudio: 180, cancellationToken: cancellationToken).ConfigureAwait(false);

        var transcriptionResult = new StringBuilder();
        byte[] previousShortSegment = null;

        foreach (var audio in splitAudios)
        {
            byte[] segmentToTranscribe = audio;

            if (previousShortSegment != null)
            {
                segmentToTranscribe = previousShortSegment.Concat(audio).ToArray();
                previousShortSegment = null;
            }

            if (segmentToTranscribe.Length < 2000)
            {
                previousShortSegment = segmentToTranscribe;
                continue;
            }

            try
            {
                var transcriptionResponse = await TranscriptionAsync(segmentToTranscribe, language, fileType, responseFormat, prompt, cancellationToken).ConfigureAwait(false);
                transcriptionResult.Append(transcriptionResponse.Trim());
                transcriptionResult.Append(" ");
            }
            catch (Exception e)
            {
                Log.Warning(e, "Transcription of audio segment failed, continuing with next segment.");
            }
        }

        Log.Information("SpeechToTextAsync completed, transcription length: {Length}", transcriptionResult.Length);
        return transcriptionResult.ToString().Trim();
    }
    
    public async Task<string> TranscriptionAsync(
        byte[] file, TranscriptionLanguage? language, TranscriptionFileType fileType = TranscriptionFileType.Wav, 
        TranscriptionResponseFormat responseFormat = TranscriptionResponseFormat.Vtt, string prompt = null, CancellationToken cancellationToken = default)
    {
        AudioClient client = new("whisper-1", _openAiSettings.ApiKey);
        
        var filename = $"{Guid.NewGuid()}.{fileType.ToString().ToLower()}";
        
        var fileResponseFormat = responseFormat switch
        {
            TranscriptionResponseFormat.Vtt => "vtt",
            TranscriptionResponseFormat.Srt => "srt",
            TranscriptionResponseFormat.Text => "text",
            TranscriptionResponseFormat.Json => "json",
            TranscriptionResponseFormat.VerboseJson => "verbose_json",
            _ => "text"
        };
        var stream = new MemoryStream(file);
        
        AudioTranscriptionOptions options = new()
        {
            ResponseFormat = AudioTranscriptionFormat.Text,
            Prompt = prompt
        };
        
        if (language.HasValue) options.Language = language.Value.GetDescription();
        
        var response = await client.TranscribeAudioAsync(stream, "test.wav", options, cancellationToken);

        Log.Information("Transcription {FileName} response {@Response}", filename, response);
        
        return response?.Value?.Text;
    }
}