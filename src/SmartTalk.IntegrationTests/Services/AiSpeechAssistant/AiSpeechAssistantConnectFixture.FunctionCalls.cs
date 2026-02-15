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
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.Timer;
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
    public async Task ShouldProcessConfirmOrder_WhenFunctionCallReceived()
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
            await repository.InsertAsync(new AiSpeechAssistantFunctionCall
            {
                AssistantId = assistant.Id, Name = "order",
                Content = "{\"type\":\"function\",\"name\":\"order\"}",
                Type = AiSpeechAssistantSessionConfigType.Tool,
                ModelProvider = RealtimeAiProvider.OpenAi, IsActive = true
            });
        });

        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_ORDER_TEST", streamSid = "MZ_ORDER_TEST" }
        }));
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new { @event = "stop" }));

        var openaiWs = new MockWebSocket(waitForCloseSignal: true);
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new { type = "session.updated" }));
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
                        name = "order",
                        call_id = "call_order_001",
                        arguments = "{\"order_items\":[{\"name\":\"Kung Pao Chicken\",\"quantity\":1}]}"
                    }
                }
            }
        }));

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
            builder.RegisterInstance(Substitute.For<ISmartiesClient>()).AsImplementedInterfaces();
            builder.RegisterInstance(openaiWs).As<WebSocket>();
        });

        var sentMessages = openaiWs.SentMessages.Select(b => Encoding.UTF8.GetString(b)).ToList();
        sentMessages.Any(m => m.Contains("function_call_output")).ShouldBeTrue();
        sentMessages.Any(m => m.Contains("response.create")).ShouldBeTrue();
    }

    [Fact]
    public async Task ShouldProcessTransferCall_WhenFunctionCallReceived()
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
            start = new { callSid = "CA_TRANSFER_TEST", streamSid = "MZ_TRANSFER_TEST" }
        }));
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new { @event = "stop" }));

        var openaiWs = new MockWebSocket(waitForCloseSignal: true);
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new { type = "session.updated" }));
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
                        call_id = "call_transfer_001",
                        arguments = "{}"
                    }
                }
            }
        }));

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
            builder.RegisterInstance(Substitute.For<ISmartiesClient>()).AsImplementedInterfaces();
            builder.RegisterInstance(openaiWs).As<WebSocket>();
        });

        mockJobClient.ReceivedWithAnyArgs(1)
            .Schedule<Mediator.Net.IMediator>(default, default(TimeSpan), default);
    }

    [Fact]
    public async Task ShouldProcessHangup_WhenFunctionCallReceived()
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
            await repository.InsertAsync(new AiSpeechAssistantFunctionCall
            {
                AssistantId = assistant.Id, Name = "hangup",
                Content = "{\"type\":\"function\",\"name\":\"hangup\"}",
                Type = AiSpeechAssistantSessionConfigType.Tool,
                ModelProvider = RealtimeAiProvider.OpenAi, IsActive = true
            });
        });

        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_HANGUP_TEST", streamSid = "MZ_HANGUP_TEST" }
        }));
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new { @event = "stop" }));

        var openaiWs = new MockWebSocket(waitForCloseSignal: true);
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new { type = "session.updated" }));
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
                        call_id = "call_hangup_001",
                        arguments = "{}"
                    }
                }
            }
        }));

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
            builder.RegisterInstance(Substitute.For<ISmartiesClient>()).AsImplementedInterfaces();
            builder.RegisterInstance(openaiWs).As<WebSocket>();
        });

        mockJobClient.ReceivedWithAnyArgs(1)
            .Schedule<IAiSpeechAssistantService>(default, default(TimeSpan), default);

        var sentMessages = openaiWs.SentMessages.Select(b => Encoding.UTF8.GetString(b)).ToList();
        sentMessages.Any(m => m.Contains("function_call_output")).ShouldBeTrue();
        sentMessages.Any(m => m.Contains("response.create")).ShouldBeTrue();
    }

    [Fact]
    public async Task ShouldProcessConfirmCustomerInfo_WhenFunctionCallReceived()
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
            await repository.InsertAsync(new AiSpeechAssistantFunctionCall
            {
                AssistantId = assistant.Id, Name = "confirm_customer_name_phone",
                Content = "{\"type\":\"function\",\"name\":\"confirm_customer_name_phone\"}",
                Type = AiSpeechAssistantSessionConfigType.Tool,
                ModelProvider = RealtimeAiProvider.OpenAi, IsActive = true
            });
        });

        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_CUSTINFO_TEST", streamSid = "MZ_CUSTINFO_TEST" }
        }));
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new { @event = "stop" }));

        var openaiWs = new MockWebSocket(waitForCloseSignal: true);
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new { type = "session.updated" }));
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
                        name = "confirm_customer_name_phone",
                        call_id = "call_custinfo_001",
                        arguments = "{\"customer_name\":\"John\",\"phone_number\":\"5551234567\"}"
                    }
                }
            }
        }));

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
            builder.RegisterInstance(openaiWs).As<WebSocket>();
        });

        var sentMessages = openaiWs.SentMessages.Select(b => Encoding.UTF8.GetString(b)).ToList();
        sentMessages.Any(m => m.Contains("function_call_output")).ShouldBeTrue();
    }

    [Fact]
    public async Task ShouldProcessConfirmPickupTime_WhenFunctionCallReceived()
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
            await repository.InsertAsync(new AiSpeechAssistantFunctionCall
            {
                AssistantId = assistant.Id, Name = "confirm_pickup_time",
                Content = "{\"type\":\"function\",\"name\":\"confirm_pickup_time\"}",
                Type = AiSpeechAssistantSessionConfigType.Tool,
                ModelProvider = RealtimeAiProvider.OpenAi, IsActive = true
            });
        });

        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_PICKUP_TEST", streamSid = "MZ_PICKUP_TEST" }
        }));
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new { @event = "stop" }));

        var openaiWs = new MockWebSocket(waitForCloseSignal: true);
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new { type = "session.updated" }));
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
                        name = "order",
                        call_id = "call_order_pre",
                        arguments = "{\"order_items\":[{\"name\":\"Tea\",\"quantity\":1}]}"
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
                        name = "confirm_pickup_time",
                        call_id = "call_pickup_001",
                        arguments = "{\"comments\":\"Pickup at 5:30 PM\"}"
                    }
                }
            }
        }));

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
            builder.RegisterInstance(openaiWs).As<WebSocket>();
        });

        var sentMessages = openaiWs.SentMessages.Select(b => Encoding.UTF8.GetString(b)).ToList();
        sentMessages.Count(m => m.Contains("function_call_output")).ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ShouldSendInitialGreeting_WhenKnowledgeHasGreetings()
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
                AssistantId = assistant.Id, Prompt = "You are a test assistant.", IsActive = true, Version = "1.0",
                Greetings = "Welcome to our restaurant!"
            });
        });

        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_GREET_TEST", streamSid = "MZ_GREET_TEST" }
        }));
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new { @event = "stop" }));

        var openaiWs = new MockWebSocket(waitForCloseSignal: true);
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
            builder.RegisterInstance(openaiWs).As<WebSocket>();
        });

        var sentMessages = openaiWs.SentMessages.Select(b => Encoding.UTF8.GetString(b)).ToList();
        sentMessages.Any(m => m.Contains("conversation.item.create") && m.Contains("Welcome to our restaurant!")).ShouldBeTrue();
        sentMessages.Any(m => m.Contains("response.create")).ShouldBeTrue();
    }

    [Fact]
    public async Task ShouldForwardAudioDelta_WhenOpenAiSendsAudioDelta()
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
        });

        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_DELTA_TEST", streamSid = "MZ_DELTA_TEST" }
        }));
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new { @event = "stop" }));

        var openaiWs = new MockWebSocket(waitForCloseSignal: true);
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new { type = "session.updated" }));
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            type = "response.audio.delta",
            delta = "dGVzdGF1ZGlvZGF0YQ==",
            item_id = "item_delta_001"
        }));

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
            builder.RegisterInstance(openaiWs).As<WebSocket>();
        });

        var twilioMessages = twilioWs.SentMessages.Select(b => Encoding.UTF8.GetString(b)).ToList();
        twilioMessages.Any(m => m.Contains("\"event\":\"media\"") && m.Contains("dGVzdGF1ZGlvZGF0YQ==")).ShouldBeTrue();
    }

    [Fact]
    public async Task ShouldStartInactivityTimer_WhenResponseDoneReceived()
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
        });

        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_TIMER_TEST", streamSid = "MZ_TIMER_TEST" }
        }));
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new { @event = "stop" }));

        var openaiWs = new MockWebSocket(waitForCloseSignal: true);
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new { type = "session.updated" }));
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            type = "response.done",
            response = new { output = Array.Empty<object>() }
        }));

        var mockTimerManager = Substitute.For<IInactivityTimerManager>();

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
            builder.RegisterInstance(mockTimerManager).As<IInactivityTimerManager>();
            builder.RegisterInstance(openaiWs).As<WebSocket>();
        });

        mockTimerManager.Received(1).StartTimer(
            Arg.Is("CA_TIMER_TEST"),
            Arg.Any<TimeSpan>(),
            Arg.Any<Func<Task>>());
    }

    [Fact]
    public async Task ShouldProcessRepeatOrder_WhenFunctionCallReceived()
    {
        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var agent = new Agent { Name = "TestAgent", IsReceiveCall = true, Type = AgentType.Assistant };
            await repository.InsertAsync(agent);

            var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Name = "TestAssistant", AnsweringNumber = TestDidNumber, ModelProvider = RealtimeAiProvider.OpenAi,
                ModelVoice = "alloy", IsDefault = true, IsDisplay = true,
                CustomRepeatOrderPrompt = "Please repeat the order."
            };
            await repository.InsertAsync(assistant);
            await unitOfWork.SaveChangesAsync();

            await repository.InsertAsync(new AgentAssistant { AgentId = agent.Id, AssistantId = assistant.Id });
            await repository.InsertAsync(new AiSpeechAssistantKnowledge
            {
                AssistantId = assistant.Id, Prompt = "You are a test assistant.", IsActive = true, Version = "1.0"
            });
            await repository.InsertAsync(new AiSpeechAssistantFunctionCall
            {
                AssistantId = assistant.Id, Name = "repeat_order",
                Content = "{\"type\":\"function\",\"name\":\"repeat_order\"}",
                Type = AiSpeechAssistantSessionConfigType.Tool,
                ModelProvider = RealtimeAiProvider.OpenAi, IsActive = true
            });
        });

        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_REPEAT_TEST", streamSid = "MZ_REPEAT_TEST" }
        }));
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new { @event = "stop" }));

        var openaiWs = new MockWebSocket(waitForCloseSignal: true);
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new { type = "session.updated" }));
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
                        name = "repeat_order",
                        call_id = "call_repeat_001",
                        arguments = "{}"
                    }
                }
            }
        }));

        var mockOpenaiClient = Substitute.For<IOpenaiClient>();
        mockOpenaiClient.GenerateAudioChatCompletionAsync(
            Arg.Any<BinaryData>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new byte[] { 1, 2, 3 });

        var mockFfmpegService = Substitute.For<IFfmpegService>();
        mockFfmpegService.ConvertWavToULawAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new byte[] { 4, 5, 6 });

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
            builder.RegisterInstance(mockOpenaiClient).As<IOpenaiClient>();
            builder.RegisterInstance(mockFfmpegService).As<IFfmpegService>();
            builder.RegisterInstance(openaiWs).As<WebSocket>();
        });

        var twilioMessages = twilioWs.SentMessages.Select(b => Encoding.UTF8.GetString(b)).ToList();
        twilioMessages.Any(m => m.Contains("\"event\":\"media\"")).ShouldBeTrue();

        await mockOpenaiClient.Received(1).GenerateAudioChatCompletionAsync(
            Arg.Any<BinaryData>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
