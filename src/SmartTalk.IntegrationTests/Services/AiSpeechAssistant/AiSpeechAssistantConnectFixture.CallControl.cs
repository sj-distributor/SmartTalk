using System.Net.WebSockets;
using System.Text;
using Autofac;
using Mediator.Net;
using Newtonsoft.Json;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Twilio;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.IntegrationTests.Mocks;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.IntegrationTests.Services.AiSpeechAssistant;

public partial class AiSpeechAssistantConnectFixture
{
    [Fact]
    public async Task ShouldHangupCall_WhenNotTransferred()
    {
        var mockTwilioService = Substitute.For<ITwilioService>();

        await Run<IAiSpeechAssistantService>(async service =>
        {
            await service.HangupCallAsync("CA_HANGUP_DIRECT", CancellationToken.None);
        }, builder =>
        {
            builder.RegisterInstance(mockTwilioService).As<ITwilioService>();
            builder.RegisterInstance(Substitute.For<ISmartiesClient>()).AsImplementedInterfaces();
        });

        await mockTwilioService.Received(1).CompleteCallAsync("CA_HANGUP_DIRECT");
    }

    [Fact]
    public async Task ShouldSkipHangup_WhenAlreadyTransferred()
    {
        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var agent = new Agent { Name = "TestAgent", IsReceiveCall = true, Type = AgentType.Assistant };
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
            await repository.InsertAsync(new AiSpeechAssistantHumanContact
            {
                AssistantId = assistant.Id, HumanPhone = "+15559990000"
            });
            await repository.InsertAsync(new AiSpeechAssistantFunctionCall
            {
                AssistantId = assistant.Id, Name = "transfer_call",
                Content = "{\"type\":\"function\",\"name\":\"transfer_call\"}",
                Type = AiSpeechAssistantSessionConfigType.Tool,
                ModelProvider = RealtimeAiProvider.OpenAi, IsActive = true
            });
        });

        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_HANGUP_SKIP", streamSid = "MZ_HANGUP_SKIP" }
        }));
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new { @event = "stop" }));

        var openaiWs = new MockWebSocket(waitForCloseSignal: true);
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new { type = "session.updated" }));
        // transfer_call sets IsTransfer = true, then hangup should be skipped
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            type = "response.done",
            response = new
            {
                output = new[]
                {
                    new
                    {
                        type = "function_call",
                        name = "transfer_call",
                        call_id = "call_transfer_then_hangup",
                        arguments = "{}"
                    }
                }
            }
        }));
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            type = "response.done",
            response = new
            {
                output = new[]
                {
                    new
                    {
                        type = "function_call",
                        name = "hangup",
                        call_id = "call_hangup_after_transfer",
                        arguments = "{}"
                    }
                }
            }
        }));

        var mockTwilioService = Substitute.For<ITwilioService>();
        var mockJobClient = Substitute.For<ISmartTalkBackgroundJobClient>();

        var command = new ConnectAiSpeechAssistantCommand
        {
            From = TestCallerNumber, To = TestDidNumber, Host = TestHost, TwilioWebSocket = twilioWs
        };

        await Run<IMediator>(async mediator =>
        {
            await mediator.SendAsync(command);
        }, builder =>
        {
            builder.RegisterInstance(mockJobClient).As<ISmartTalkBackgroundJobClient>();
            builder.RegisterInstance(mockTwilioService).As<ITwilioService>();
            builder.RegisterInstance(Substitute.For<ISmartiesClient>()).AsImplementedInterfaces();
            builder.RegisterInstance(openaiWs).As<WebSocket>();
        });

        // transfer_call triggers Schedule for transfer, hangup also triggers Schedule for hangup
        // But HangupCallAsync checks IsTransfer and skips the actual Twilio call
        mockJobClient.ReceivedWithAnyArgs(1)
            .Schedule<Mediator.Net.IMediator>(default, default(TimeSpan), default);
        mockJobClient.ReceivedWithAnyArgs(1)
            .Schedule<IAiSpeechAssistantService>(default, default(TimeSpan), default);

        // The actual Twilio call should NOT have been made (IsTransfer = true skips it)
        await mockTwilioService.DidNotReceiveWithAnyArgs().CompleteCallAsync(default);
    }

    [Fact]
    public async Task ShouldTransferHumanService_ViaTwilioService()
    {
        var mockTwilioService = Substitute.For<ITwilioService>();

        await Run<IAiSpeechAssistantService>(async service =>
        {
            await service.TransferHumanServiceAsync(new TransferHumanServiceCommand
            {
                CallSid = "CA_TRANSFER_DIRECT",
                HumanPhone = "+15559990000"
            }, CancellationToken.None);
        }, builder =>
        {
            builder.RegisterInstance(mockTwilioService).As<ITwilioService>();
            builder.RegisterInstance(Substitute.For<ISmartiesClient>()).AsImplementedInterfaces();
        });

        await mockTwilioService.Received(1).UpdateCallTwimlAsync(
            "CA_TRANSFER_DIRECT",
            Arg.Is<string>(twiml => twiml.Contains("+15559990000")));
    }
}
