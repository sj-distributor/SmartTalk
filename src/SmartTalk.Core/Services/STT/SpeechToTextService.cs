using Serilog;
using AutoMapper;
using System.Text;
using OpenAI.Interfaces;
using SmartTalk.Core.Ioc;
using OpenAI.ObjectModels;
using SmartTalk.Core.Extensions;
using SmartTalk.Messages.Dto.STT;
using SmartTalk.Messages.Enums.STT;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Ffmpeg;
using OpenAI.ObjectModels.RequestModels;

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
    private readonly IOpenAIService _openAiService;
    private readonly ISmartiesHttpClientFactory _httpClientFactory;

    public SpeechToTextService(IMapper mapper, IFfmpegService ffmpegService, IOpenAIService openAiService, ISmartiesHttpClientFactory httpClientFactory)
    {
        _mapper = mapper;
        _ffmpegService = ffmpegService;
        _openAiService = openAiService;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> SpeechToTextAsync(
        byte[] file, TranscriptionLanguage? language = null, TranscriptionFileType fileType = TranscriptionFileType.Wav,
        TranscriptionResponseFormat responseFormat = TranscriptionResponseFormat.Vtt, string prompt = null, CancellationToken cancellationToken = default)
    {
        if (file == null) return null;
        
        var audioBytes = await _ffmpegService.ConvertFileFormatAsync(file, fileType, cancellationToken).ConfigureAwait(false);

        var splitAudios = await _ffmpegService.SplitAudioAsync(audioBytes, secondsPerAudio: 60 * 3, cancellationToken: cancellationToken).ConfigureAwait(false);

        var transcriptionResult = new StringBuilder();

        foreach (var audio in splitAudios)
        {
            var transcriptionResponse = await TranscriptionAsync(audio, language, fileType, responseFormat, prompt, cancellationToken).ConfigureAwait(false);
            
            transcriptionResult.Append(transcriptionResponse.Text);
        }

        Log.Information("Transcription result {Transcription}", transcriptionResult.ToString());
        
        return transcriptionResult.ToString();
    }
    
    public async Task<AudioTranscriptionResponseDto> TranscriptionAsync(
        byte[] file, TranscriptionLanguage? language, TranscriptionFileType fileType = TranscriptionFileType.Wav, 
        TranscriptionResponseFormat responseFormat = TranscriptionResponseFormat.Vtt, string prompt = null, CancellationToken cancellationToken = default)
    {
        var filename = $"{Guid.NewGuid()}.{fileType.ToString().ToLower()}";
        
        var fileResponseFormat = responseFormat switch
        {
            TranscriptionResponseFormat.Vtt => StaticValues.AudioStatics.ResponseFormat.Vtt,
            TranscriptionResponseFormat.Srt => StaticValues.AudioStatics.ResponseFormat.Srt,
            TranscriptionResponseFormat.Text => StaticValues.AudioStatics.ResponseFormat.Text,
            TranscriptionResponseFormat.Json => StaticValues.AudioStatics.ResponseFormat.Json,
            TranscriptionResponseFormat.VerboseJson => StaticValues.AudioStatics.ResponseFormat.Vtt
        };

        var transcriptionRequest = new AudioCreateTranscriptionRequest
        {
            File = file,
            FileName = filename,
            Model = Models.WhisperV1,
            ResponseFormat = fileResponseFormat
        };

        if (language.HasValue) transcriptionRequest.Language = language.Value.GetDescription();
        
        var response = await _httpClientFactory.SafelyProcessRequestAsync(nameof(SpeechToTextAsync), async () =>
            await _openAiService.Audio.CreateTranscription(new AudioCreateTranscriptionRequest
            {
                File = file,
                FileName = filename,
                Model = Models.WhisperV1,
                ResponseFormat = fileResponseFormat,
                Language = language?.GetDescription(),
                Prompt = prompt
            }, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

        Log.Information("Transcription {FileName} response {@Response}", filename, response);
        
        return _mapper.Map<AudioTranscriptionResponseDto>(response);
    }
}