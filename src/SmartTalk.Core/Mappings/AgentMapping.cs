using AutoMapper;
using SmartTalk.Core.Domain.System;
using SmartTalk.Messages.Commands.Agent;
using SmartTalk.Messages.Dto.Agent;

namespace SmartTalk.Core.Mappings;

public class AgentMapping : Profile
{
    public AgentMapping()
    {
        CreateMap<Agent, AgentDto>();
        CreateMap<UpdateAgentCommand, Agent>()
            .ForMember(dest => dest.IsReceiveCall, opt => opt.MapFrom(src => src.IsReceivingCall));;
    }
}