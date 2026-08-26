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
using SmartTalk.Core.Services.Jobs;
using SmartTalk.IntegrationTests.Mocks;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.Smarties;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
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
                Prompt = "Profile: #{user_profile}. Time: #{current_time}. Phone: #{customer_phone}. Date: #{pst_date}. Question: #{question}.",
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
        sessionUpdate.ShouldNotContain("#{question}");
    }

    [Fact]
    public async Task ShouldUseInstructionAsPrompt_WhenConnectCommandHasInstruction()
    {
        // 保留既有调用契约: Instruction 有值时继续覆盖 DB Assistant prompt。
        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var agent = new Agent
            {
                Name = "TestAgent", IsReceiveCall = true, Type = AgentType.Assistant, ServiceHours = null
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
                Prompt = "DB_PROMPT_MARKER_should_be_overridden",
                IsActive = true, Version = "1.0"
            });
        });

        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_INSTR", streamSid = "MZ_INSTR" }
        }));

        var openaiWs = CreateProviderMock();
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new { type = "session.updated" }));

        const string instruction = "INSTRUCTION_MARKER_use_this_as_prompt — you are calling a merchant on behalf of a customer.";

        var command = new ConnectAiSpeechAssistantCommand
        {
            From = TestCallerNumber,
            To = TestDidNumber,
            Host = TestHost,
            Instruction = instruction,
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

        openaiWs.SentMessages.ShouldNotBeEmpty();
        var sessionUpdate = Encoding.UTF8.GetString(openaiWs.SentMessages.First());
        sessionUpdate.ShouldContain("INSTRUCTION_MARKER_use_this_as_prompt");
        sessionUpdate.ShouldNotContain("DB_PROMPT_MARKER_should_be_overridden");
    }

    [Fact]
    public async Task ShouldDecodeAndReplaceQuestionInAssistantPrompt_WhenConnectCommandHasEncodedQuestion()
    {
        // 代客致电只替换 Assistant knowledge prompt 中的 #{question}; 固定 persona 与通话流程仍来自 DB prompt。
        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var agent = new Agent
            {
                Name = "TestAgent", IsReceiveCall = true, Type = AgentType.Assistant, ServiceHours = null
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
                Prompt = "DB_PROMPT_MARKER_before_question\n#{question}\nDB_PROMPT_MARKER_after_question",
                IsActive = true, Version = "1.0"
            });
        });

        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_INSTR", streamSid = "MZ_INSTR" }
        }));

        var openaiWs = CreateProviderMock();
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new { type = "session.updated" }));

        const string question = "1. What time do you close today?\n2. Is there a wait for four people?";
        var encodedQuestion = Convert.ToBase64String(Encoding.UTF8.GetBytes(question))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var command = new ConnectAiSpeechAssistantCommand
        {
            From = TestCallerNumber,
            To = TestDidNumber,
            Host = TestHost,
            EncodedQuestion = encodedQuestion,
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

        openaiWs.SentMessages.ShouldNotBeEmpty();
        var sessionUpdate = Encoding.UTF8.GetString(openaiWs.SentMessages.First());
        sessionUpdate.ShouldContain("DB_PROMPT_MARKER_before_question");
        sessionUpdate.ShouldContain("1. What time do you close today?");
        sessionUpdate.ShouldContain("2. Is there a wait for four people?");
        sessionUpdate.ShouldContain("DB_PROMPT_MARKER_after_question");
        sessionUpdate.ShouldNotContain("#{question}");
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

    [Fact]
    public async Task ShouldResolveCustomerItemsAtSessionStart_WhenSingleStoreGreetingContainsToken()
    {
        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var agent = new Agent { Name = "TestAgent", IsReceiveCall = true, Type = AgentType.Assistant };
            await repository.InsertAsync(agent);

            var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Name = "CascadeAssistant",
                AnsweringNumber = TestDidNumber,
                ModelProvider = RealtimeAiProvider.OpenAi,
                ModelVoice = "alloy",
                IsDefault = true,
                IsDisplay = true
            };
            await repository.InsertAsync(assistant);
            await unitOfWork.SaveChangesAsync();

            await repository.InsertAsync(new AgentAssistant { AgentId = agent.Id, AssistantId = assistant.Id });
            await repository.InsertAsync(new AiSpeechAssistantKnowledge
            {
                AssistantId = assistant.Id,
                Prompt = "Greeting: #{greeting} End.",
                IsActive = true,
                Version = "1.0"
            });

            // A single-store assistant resolves customer items at session start.
            await repository.InsertAsync(new Core.Domain.Sales.AiSpeechAssistantKnowledgeVariableCache
            {
                CacheKey = "customer_items",
                Filter = "CascadeAssistant",
                CacheValue = "CASCADE_MARKER_PIZZA_LARGE"
            });
        });

        // Smarties returns a greeting that embeds the customer-items token.
        var smartiesMock = Substitute.For<ISmartiesClient>();
        smartiesMock.GetSaleAutoCallNumberAsync(Arg.Any<GetSaleAutoCallNumberRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetSaleAutoCallNumberResponse
            {
                Data = new GetSaleAutoCallNumberResponseData
                {
                    Number = new SettingNumberDto
                    {
                        Greeting = "Welcome! Today's items: #{customer_items}"
                    }
                }
            });

        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_CASCADE_TEST", streamSid = "MZ_CASCADE_TEST" }
        }));

        var openaiWs = CreateProviderMock();
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new { type = "session.updated" }));

        var command = new ConnectAiSpeechAssistantCommand
        {
            From = TestCallerNumber,
            To = TestDidNumber,
            Host = TestHost,
            TwilioWebSocket = twilioWs,
            NumberId = 42   // Must be non-null so ResolveGreetingAsync fires
        };

        await Run<IMediator>(async mediator =>
        {
            await mediator.SendAsync(command);
        }, builder =>
        {
            builder.RegisterInstance(Substitute.For<ISmartTalkBackgroundJobClient>()).As<ISmartTalkBackgroundJobClient>();
            builder.RegisterInstance(smartiesMock).AsImplementedInterfaces();
            openaiWs.Register(builder);
        });

        openaiWs.SentMessages.ShouldNotBeEmpty();
        var sessionUpdate = Encoding.UTF8.GetString(openaiWs.SentMessages.First());

        // Greeting was injected
        sessionUpdate.ShouldContain("Welcome!");

        // The raw token must not leak to the LLM.
        sessionUpdate.ShouldNotContain("#{customer_items}");

        sessionUpdate.ShouldContain("CASCADE_MARKER_PIZZA_LARGE");
    }

    [Fact]
    public async Task ShouldReplaceCustomerItemsWithSpace_WhenNoCacheDataExists()
    {
        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var agent = new Agent { Name = "TestAgent", IsReceiveCall = true, Type = AgentType.Assistant };
            await repository.InsertAsync(agent);

            var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Name = "SoldTo1/SoldTo2", AnsweringNumber = TestDidNumber, ModelProvider = RealtimeAiProvider.OpenAi,
                ModelVoice = "alloy", IsDefault = true, IsDisplay = true
            };
            await repository.InsertAsync(assistant);
            await unitOfWork.SaveChangesAsync();

            await repository.InsertAsync(new AgentAssistant { AgentId = agent.Id, AssistantId = assistant.Id });
            await repository.InsertAsync(new AiSpeechAssistantKnowledge
            {
                AssistantId = assistant.Id,
                Prompt = "Items:#{customer_items}End.",
                IsActive = true, Version = "1.0"
            });
        });

        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_CUSTITEM_TEST", streamSid = "MZ_CUSTITEM_TEST" }
        }));

        var openaiWs = CreateProviderMock();
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new { type = "session.updated" }));

        var command = new ConnectAiSpeechAssistantCommand
        {
            From = TestCallerNumber, To = TestDidNumber, Host = TestHost, TwilioWebSocket = twilioWs
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

        openaiWs.SentMessages.ShouldNotBeEmpty();
        var sessionUpdate = Encoding.UTF8.GetString(openaiWs.SentMessages.First());
        // Empty #{customer_items} replaced with space " ", not empty string ""
        sessionUpdate.ShouldContain("Items: End.");
        sessionUpdate.ShouldNotContain("#{customer_items}");
    }
}
