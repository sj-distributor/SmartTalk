using AutoMapper;
using SmartTalk.Messages.Dto.STT;
using OpenAI.ObjectModels.ResponseModels;

namespace SmartTalk.Core.Mappings;

public class SttMapping : Profile
{
    public SttMapping()
    {
        CreateMap<AudioCreateTranscriptionResponse, AudioTranscriptionResponseDto>();
        CreateMap<AudioCreateTranscriptionResponse.Segment, AudioTranscriptionSegmentDto>();
    }
}