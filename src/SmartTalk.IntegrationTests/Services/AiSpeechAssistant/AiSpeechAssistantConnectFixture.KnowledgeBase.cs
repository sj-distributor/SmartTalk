using System.Text;
using Autofac;
using Mediator.Net;
using Newtonsoft.Json;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.IntegrationTests.Mocks;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.IntegrationTests.Services.AiSpeechAssistant;

public partial class AiSpeechAssistantConnectFixture
{
    [Fact]
    public async Task ShouldReplacePromptVariables_WhenKnowledgeHasVariables()
    {
        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var agent = new Agent
            {
                Name = "TestAgent", IsReceiveCall = true, Type = AgentType.Assistant,
                ServiceHours = null
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
                AssistantId = assistant.Id,
                Prompt = "Profile: #{user_profile}. Time: #{current_time}. Phone: #{customer_phone}. Date: #{pst_date}.",
                IsActive = true, Version = "1.0"
            });
            await repository.InsertAsync(new Core.Domain.AIAssistant.AiSpeechAssistantUserProfile
            {
                AssistantId = assistant.Id, CallerNumber = TestCallerNumber,
                ProfileJson = "{\"name\":\"VIPCustomer\",\"tier\":\"gold\"}"
            });
        });

        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_KB_VAR", streamSid = "MZ_KB_VAR" }
        }));

        var openaiWs = CreateProviderMock();
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new { type = "session.updated" }));

        var command = new ConnectAiSpeechAssistantCommand
        {
            From = TestCallerNumber,
            To = TestDidNumber,
            Host = TestHost,
            TwilioWebSocket = twilioWs
        };

        await Run<IMediator>(async mediator =>
        {
            await mediator.SendAsync(command);
        }, builder =>
        {
            builder.RegisterInstance(Substitute.For<ISmartTalkBackgroundJobClient>()).As<ISmartTalkBackgroundJobClient>();
            builder.RegisterInstance(Substitute.For<ISmartiesClient>()).AsImplementedInterfaces();
            openaiWs.Register(builder);
        });

        // Session update sent to OpenAI proves all DB lookups + variable replacement completed
        openaiWs.SentMessages.ShouldNotBeEmpty();
        var sessionUpdate = Encoding.UTF8.GetString(openaiWs.SentMessages.First());
        sessionUpdate.ShouldContain("VIPCustomer");
        sessionUpdate.ShouldNotContain("#{user_profile}");
        sessionUpdate.ShouldNotContain("#{current_time}");
        sessionUpdate.ShouldNotContain("#{customer_phone}");
        sessionUpdate.ShouldNotContain("#{pst_date}");
    }

    [Fact]
    public async Task ShouldBuildKnowledge_WithForwardedAssistantId()
    {
        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var agent = new Agent { Name = "TestAgent", IsReceiveCall = true, Type = AgentType.Assistant };
            await repository.InsertAsync(agent);

            var primaryAssistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Name = "PrimaryAssistant", AnsweringNumber = TestDidNumber, ModelProvider = RealtimeAiProvider.OpenAi,
                ModelVoice = "alloy", IsDefault = true, IsDisplay = true
            };
            await repository.InsertAsync(primaryAssistant);

            var forwardedAssistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Name = "ForwardedAssistant", ModelProvider = RealtimeAiProvider.OpenAi,
                ModelVoice = "alloy", IsDefault = false, IsDisplay = true
            };
            await repository.InsertAsync(forwardedAssistant);
            await unitOfWork.SaveChangesAsync();

            await repository.InsertAsync(new AgentAssistant { AgentId = agent.Id, AssistantId = primaryAssistant.Id });
            await repository.InsertAsync(new AgentAssistant { AgentId = agent.Id, AssistantId = forwardedAssistant.Id });

            await repository.InsertAsync(new AiSpeechAssistantInboundRoute
            {
                From = TestCallerNumber, To = TestDidNumber, ForwardAssistantId = forwardedAssistant.Id,
                IsFullDay = true, DayOfWeek = "0,1,2,3,4,5,6", Priority = 1,
                TimeZone = "Pacific Standard Time"
            });

            await repository.InsertAsync(new AiSpeechAssistantKnowledge
            {
                AssistantId = forwardedAssistant.Id, Prompt = "You are a forwarded assistant.", IsActive = true, Version = "1.0"
            });
        });

        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_KB_FWD", streamSid = "MZ_KB_FWD" }
        }));

        var openaiWs = CreateProviderMock();
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new { type = "session.updated" }));

        var command = new ConnectAiSpeechAssistantCommand
        {
            From = TestCallerNumber,
            To = TestDidNumber,
            Host = TestHost,
            TwilioWebSocket = twilioWs
        };

        await Run<IMediator>(async mediator =>
        {
            await mediator.SendAsync(command);
        }, builder =>
        {
            builder.RegisterInstance(Substitute.For<ISmartTalkBackgroundJobClient>()).As<ISmartTalkBackgroundJobClient>();
            builder.RegisterInstance(Substitute.For<ISmartiesClient>()).AsImplementedInterfaces();
            openaiWs.Register(builder);
        });

        // Session update sent to OpenAI proves the forwarded assistant's knowledge was used
        openaiWs.SentMessages.ShouldNotBeEmpty();
        var sessionUpdate = Encoding.UTF8.GetString(openaiWs.SentMessages.First());
        sessionUpdate.ShouldContain("You are a forwarded assistant.");
    }

    [Fact]
    public async Task ShouldProceed_WhenNoServiceHoursConfigured()
    {
        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var agent = new Agent
            {
                Name = "TestAgent", IsReceiveCall = true, Type = AgentType.Assistant,
                ServiceHours = null
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

        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_KB_SVC", streamSid = "MZ_KB_SVC" }
        }));

        var openaiWs = CreateProviderMock();
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new { type = "session.updated" }));

        var command = new ConnectAiSpeechAssistantCommand
        {
            From = TestCallerNumber,
            To = TestDidNumber,
            Host = TestHost,
            TwilioWebSocket = twilioWs
        };

        await Run<IMediator>(async mediator =>
        {
            await mediator.SendAsync(command);
        }, builder =>
        {
            builder.RegisterInstance(Substitute.For<ISmartTalkBackgroundJobClient>()).As<ISmartTalkBackgroundJobClient>();
            builder.RegisterInstance(Substitute.For<ISmartiesClient>()).AsImplementedInterfaces();
            openaiWs.Register(builder);
        });

        // Session update sent to OpenAI proves the service proceeded past the service hours check
        openaiWs.SentMessages.ShouldNotBeEmpty();
    }
}
