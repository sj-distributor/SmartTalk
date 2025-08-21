using OpenAI.Chat;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Commands.SpeechMatics;
using SmartTalk.Messages.Dto.Google;
using SmartTalk.Messages.Enums.Audio;

namespace SmartTalk.Core.Services.Audio.Provider;

public class GoogleAudioModelProvider : IAudioModelProvider
{
    private readonly IGoogleClient _googleClient;

    public GoogleAudioModelProvider(IGoogleClient googleClient)
    {
        _googleClient = googleClient;
    }

    public AudioModelProviderType ModelProviderType { get; set; } = AudioModelProviderType.Google;

    public async Task<string> ExtractAudioDataFromModelProviderAsync(AnalyzeAudioCommand command, BinaryData audioData, CancellationToken cancellationToken)
    {
        var requestBody = new GoogleGenerateContentRequest
        {
            Contents =
            [
                new GoogleContentDto
                {
                    Parts =
                    [
                        new()
                        {
                            InlineData = new ()
                            {
                                Data = Convert.ToBase64String(audioData),
                                MimeType = GetMimeType(command.AudioFileFormat)
                            }
                        }
                    ]
                }
            ],
            GenerationConfig = new GoogleGenerationConfigDto
            {
                Temperature = 0.5
            }
        };
        
        if (!string.IsNullOrWhiteSpace(command.SystemPrompt))
            requestBody.SystemInstruction = new GoogleContentDto()
            {
                Role = "system",
                Parts = [new() { Text = command.SystemPrompt }]
            };
        
        if (!string.IsNullOrWhiteSpace(command.UserPrompt))
            requestBody.Contents.First().Parts.Add(new GooglePartDto()
            {
                Text = command.UserPrompt
            });
        
        var response = await _googleClient.GenerateContentAsync(requestBody, "gemini-2.5-flash", cancellationToken).ConfigureAwait(false);

        return response.Candidates.FirstOrDefault()?.Content.Parts.FirstOrDefault()?.Text;
    }

    private string GetMimeType(AudioFileFormat commandAudioFileFormat)
    {
        return commandAudioFileFormat switch
        {
            AudioFileFormat.Wav => "audio/wav",
            AudioFileFormat.Mp3 => "audio/mpeg",
            _ => throw new ArgumentOutOfRangeException(nameof(commandAudioFileFormat), commandAudioFileFormat, null)
        };
    }
}