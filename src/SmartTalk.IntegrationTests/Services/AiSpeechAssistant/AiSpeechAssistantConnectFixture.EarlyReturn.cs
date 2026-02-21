using Autofac;
using Mediator.Net;
using Newtonsoft.Json;
using NSubstitute;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.IntegrationTests.Services.AiSpeechAssistant;

public partial class AiSpeechAssistantConnectFixture
{
    [Fact]
    public async Task ShouldReturnEmpty_WhenAgentNotFound()
    {
        var command = new ConnectAiSpeechAssistantCommand
        {
            From = TestCallerNumber,
            To = TestDidNumber,
            Host = TestHost
        };

        await Run<IMediator>(async mediator =>
        {
            await mediator.SendAsync(command);
        }, MockExternalServices);
    }

    [Fact]
    public async Task ShouldReturnEmpty_WhenAgentNotReceivingCalls()
    {
        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var agent = new Agent { Name = "TestAgent", IsReceiveCall = false, Type = AgentType.Assistant };
            await repository.InsertAsync(agent);

            var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Name = "TestAssistant", AnsweringNumber = TestDidNumber, ModelProvider = RealtimeAiProvider.OpenAi,
                ModelVoice = "alloy", IsDefault = true, IsDisplay = true
            };
            await repository.InsertAsync(assistant);
            await unitOfWork.SaveChangesAsync();

            await repository.InsertAsync(new AgentAssistant { AgentId = agent.Id, AssistantId = assistant.Id });
        });

        var command = new ConnectAiSpeechAssistantCommand
        {
            From = TestCallerNumber,
            To = TestDidNumber,
            Host = TestHost
        };

        await Run<IMediator>(async mediator =>
        {
            await mediator.SendAsync(command);
        }, MockExternalServices);
    }

    [Fact]
    public async Task ShouldReturnEmpty_WhenOutOfServiceHoursAndNoManualService()
    {
        var serviceHours = JsonConvert.SerializeObject(Enumerable.Range(0, 7).Select(d => new AgentServiceHoursDto
        {
            Day = d,
            Hours = new List<HoursDto> { new() { Start = new TimeSpan(2, 0, 0), End = new TimeSpan(2, 1, 0) } }
        }).ToList());

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var agent = new Agent
            {
                Name = "TestAgent", IsReceiveCall = true, Type = AgentType.Assistant,
                ServiceHours = serviceHours, IsTransferHuman = false
            };
            await repository.InsertAsync(agent);

            var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Name = "TestAssistant", AnsweringNumber = TestDidNumber, ModelProvider = RealtimeAiProvider.OpenAi,
                ModelVoice = "alloy", IsDefault = true, IsDisplay = true
            };
            await repository.InsertAsync(assistant);
            await unitOfWork.SaveChangesAsync();

            await repository.InsertAsync(new AgentAssistant { AgentId = agent.Id, AssistantId = assistant.Id });
            await repository.InsertAsync(new AiSpeechAssistantKnowledge
            {
                AssistantId = assistant.Id, Prompt = "You are a test assistant.", IsActive = true, Version = "1.0"
            });
        });

        var command = new ConnectAiSpeechAssistantCommand
        {
            From = TestCallerNumber,
            To = TestDidNumber,
            Host = TestHost
        };

        await Run<IMediator>(async mediator =>
        {
            await mediator.SendAsync(command);
        }, MockExternalServices);
    }

    [Fact]
    public async Task ShouldDropCall_WhenOutOfServiceHoursWithNoManualServiceEvenIfForwardRouteExists()
    {
        const string forwardNumber = "+15551110000";

        var serviceHours = JsonConvert.SerializeObject(Enumerable.Range(0, 7).Select(d => new AgentServiceHoursDto
        {
            Day = d,
            Hours = new List<HoursDto> { new() { Start = new TimeSpan(2, 0, 0), End = new TimeSpan(2, 1, 0) } }
        }).ToList());

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var agent = new Agent
            {
                Name = "TestAgent", IsReceiveCall = true, Type = AgentType.Assistant,
                ServiceHours = serviceHours, IsTransferHuman = false
            };
            await repository.InsertAsync(agent);

            var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Name = "TestAssistant", AnsweringNumber = TestDidNumber, ModelProvider = RealtimeAiProvider.OpenAi,
                ModelVoice = "alloy", IsDefault = true, IsDisplay = true
            };
            await repository.InsertAsync(assistant);
            await unitOfWork.SaveChangesAsync();

            await repository.InsertAsync(new AgentAssistant { AgentId = agent.Id, AssistantId = assistant.Id });
            await repository.InsertAsync(new AiSpeechAssistantKnowledge
            {
                AssistantId = assistant.Id, Prompt = "You are a test assistant.", IsActive = true, Version = "1.0"
            });
            await repository.InsertAsync(new AiSpeechAssistantInboundRoute
            {
                From = TestCallerNumber, To = TestDidNumber, ForwardNumber = forwardNumber,
                IsFullDay = true, DayOfWeek = "0,1,2,3,4,5,6", Priority = 1,
                TimeZone = "Pacific Standard Time"
            });
        });

        var mockJobClient = Substitute.For<ISmartTalkBackgroundJobClient>();

        var command = new ConnectAiSpeechAssistantCommand
        {
            From = TestCallerNumber, To = TestDidNumber, Host = TestHost
        };

        await Run<IMediator>(async mediator =>
        {
            await mediator.SendAsync(command);
        }, builder =>
        {
            builder.RegisterInstance(mockJobClient).As<ISmartTalkBackgroundJobClient>();
            builder.RegisterInstance(Substitute.For<ISmartiesClient>()).AsImplementedInterfaces();
        });

        // Service hours rejects BEFORE forward — no jobs enqueued
        mockJobClient.DidNotReceiveWithAnyArgs().Enqueue<Mediator.Net.IMediator>(default, default);
        mockJobClient.DidNotReceiveWithAnyArgs().Enqueue<IAiSpeechAssistantProcessJobService>(default, default);
    }
}
