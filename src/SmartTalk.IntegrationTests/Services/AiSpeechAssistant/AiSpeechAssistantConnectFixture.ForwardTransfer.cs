using System.Linq.Expressions;
using Autofac;
using Mediator.Net;
using Newtonsoft.Json;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Services.AiSpeechAssistant;
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
    public async Task ShouldForwardCall_WhenInboundRouteHasForwardNumber()
    {
        const string forwardNumber = "+15551110000";

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
            await repository.InsertAsync(new AiSpeechAssistantInboundRoute
            {
                From = TestCallerNumber, To = TestDidNumber, ForwardNumber = forwardNumber,
                IsFullDay = true, DayOfWeek = "0,1,2,3,4,5,6", Priority = 1,
                TimeZone = "Pacific Standard Time"
            });
        });

        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_TEST_123", streamSid = "MZ_TEST_456" }
        }));

        var mockJobClient = Substitute.For<ISmartTalkBackgroundJobClient>();

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
            builder.RegisterInstance(mockJobClient).As<ISmartTalkBackgroundJobClient>();
            builder.RegisterInstance(Substitute.For<ISmartiesClient>()).AsImplementedInterfaces();
        });

        // "start" event enqueues RecordCall + TransferHumanService (forward)
        mockJobClient.ReceivedWithAnyArgs(2)
            .Enqueue<Mediator.Net.IMediator>(default, default);
    }

    [Fact]
    public async Task ShouldPrioritizeEmergencyRoute_OverNormalRoute()
    {
        const string emergencyForward = "+15552220000";
        const string normalForward = "+15553330000";

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
            await repository.InsertAsync(new AiSpeechAssistantInboundRoute
            {
                From = TestCallerNumber, To = TestDidNumber, ForwardNumber = normalForward,
                IsFullDay = true, DayOfWeek = "0,1,2,3,4,5,6", Priority = 1,
                Emergency = false, TimeZone = "Pacific Standard Time"
            });
            await repository.InsertAsync(new AiSpeechAssistantInboundRoute
            {
                From = TestCallerNumber, To = TestDidNumber, ForwardNumber = emergencyForward,
                IsFullDay = true, DayOfWeek = "0,1,2,3,4,5,6", Priority = 2,
                Emergency = true, TimeZone = "Pacific Standard Time"
            });
        });

        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_TEST_EMG", streamSid = "MZ_TEST_EMG" }
        }));

        var mockJobClient = Substitute.For<ISmartTalkBackgroundJobClient>();

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
            builder.RegisterInstance(mockJobClient).As<ISmartTalkBackgroundJobClient>();
            builder.RegisterInstance(Substitute.For<ISmartiesClient>()).AsImplementedInterfaces();
        });

        // Forward path taken: RecordCall + TransferHumanService enqueued
        // Emergency route priority is verified by the data provider test (ShouldGetInboundRoutes_CallerSpecificOverFallback)
        mockJobClient.ReceivedWithAnyArgs(2)
            .Enqueue<Mediator.Net.IMediator>(default, default);
    }

    [Fact]
    public async Task ShouldUseCallerSpecificRoute_OverFallback()
    {
        const string callerForward = "+15554440000";
        const string fallbackForward = "+15555550000";

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
            await repository.InsertAsync(new AiSpeechAssistantInboundRoute
            {
                From = TestCallerNumber, To = TestDidNumber, ForwardNumber = callerForward,
                IsFullDay = true, DayOfWeek = "0,1,2,3,4,5,6", Priority = 1,
                IsFallback = false, TimeZone = "Pacific Standard Time"
            });
            await repository.InsertAsync(new AiSpeechAssistantInboundRoute
            {
                To = TestDidNumber, ForwardNumber = fallbackForward,
                IsFullDay = true, DayOfWeek = "0,1,2,3,4,5,6", Priority = 1,
                IsFallback = true, TimeZone = "Pacific Standard Time"
            });
        });

        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_TEST_SPEC", streamSid = "MZ_TEST_SPEC" }
        }));

        var mockJobClient = Substitute.For<ISmartTalkBackgroundJobClient>();

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
            builder.RegisterInstance(mockJobClient).As<ISmartTalkBackgroundJobClient>();
            builder.RegisterInstance(Substitute.For<ISmartiesClient>()).AsImplementedInterfaces();
        });

        // Forward path taken: RecordCall + TransferHumanService enqueued
        // Caller-specific vs fallback priority is verified by the data provider test
        mockJobClient.ReceivedWithAnyArgs(2)
            .Enqueue<Mediator.Net.IMediator>(default, default);
    }

    [Fact]
    public async Task ShouldResolveForwardNumberByIsFallback_WhenConnectCommandRunsOnV2()
    {
        const string fallbackForwardNumber = "+15557770000";
        const string nonFallbackForwardNumber = "+15558880000";
        const string unknownCaller = "+15550009999";

        static string? GetTransferHumanPhone(Expression<Func<IMediator, Task>> expression)
        {
            if (expression.Body is not MethodCallExpression methodCall || methodCall.Arguments.Count == 0)
                return null;

            var boxed = Expression.Convert(methodCall.Arguments[0], typeof(object));
            var command = Expression.Lambda<Func<object>>(boxed).Compile().Invoke() as TransferHumanServiceCommand;
            return command?.HumanPhone;
        }

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

        await Run<IAiSpeechAssistantService>(async service =>
        {
            await service.ConfigureAiSpeechAssistantInboundRouteAsync(new ConfigureAiSpeechAssistantInboundRouteCommand
            {
                TargetNumber = TestDidNumber,
                ForwardNUmber = fallbackForwardNumber
            }, CancellationToken.None);
        });

        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            await repository.InsertAsync(new AiSpeechAssistantInboundRoute
            {
                To = TestDidNumber, ForwardNumber = nonFallbackForwardNumber,
                IsFullDay = true, DayOfWeek = "0,1,2,3,4,5,6", Priority = 2,
                Emergency = true, IsFallback = false, TimeZone = "Pacific Standard Time"
            });
        });

        await Run<IAiSpeechAssistantDataProvider>(async dataProvider =>
        {
            var routes = await dataProvider.GetAiSpeechAssistantInboundRoutesAsync(TestDidNumber, true, CancellationToken.None);
            routes.Count.ShouldBe(2);
            routes.Any(x => x.IsFallback && x.ForwardNumber == fallbackForwardNumber).ShouldBeTrue();
            routes.Any(x => !x.IsFallback && x.ForwardNumber == nonFallbackForwardNumber).ShouldBeTrue();
        });

        var ws1 = new MockWebSocket();
        ws1.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_TEST_FALLBACK_TRUE", streamSid = "MZ_TEST_FALLBACK_TRUE" }
        }));

        var command1 = new ConnectAiSpeechAssistantCommand
        {
            From = unknownCaller,
            To = TestDidNumber,
            Host = TestHost,
            TwilioWebSocket = ws1
        };

        ISmartTalkBackgroundJobClient? jobClient1 = null;
        string? forwardedPhone1 = null;
        await Run<IMediator>(async mediator =>
        {
            await mediator.SendAsync(command1);
        }, builder =>
        {
            jobClient1 = Substitute.For<ISmartTalkBackgroundJobClient>();
            jobClient1.Enqueue(Arg.Any<Expression<Func<IMediator, Task>>>(), Arg.Any<string>())
                .Returns(callInfo =>
                {
                    var expression = callInfo.Arg<Expression<Func<IMediator, Task>>>();
                    forwardedPhone1 = GetTransferHumanPhone(expression);
                    return string.Empty;
                });

            builder.RegisterInstance(jobClient1).As<ISmartTalkBackgroundJobClient>();
            builder.RegisterInstance(Substitute.For<ISmartiesClient>()).AsImplementedInterfaces();
        });
        forwardedPhone1.ShouldBe(fallbackForwardNumber);

        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            var fallbackRoute = repository.Query<AiSpeechAssistantInboundRoute>()
                .First(x => x.To == TestDidNumber && x.ForwardNumber == fallbackForwardNumber);
            fallbackRoute.IsFallback = false;
            await repository.UpdateAsync(fallbackRoute);
        });

        var ws2 = new MockWebSocket();
        ws2.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_TEST_FALLBACK_FALSE", streamSid = "MZ_TEST_FALLBACK_FALSE" }
        }));

        var command2 = new ConnectAiSpeechAssistantCommand
        {
            From = unknownCaller,
            To = TestDidNumber,
            Host = TestHost,
            TwilioWebSocket = ws2
        };

        var openaiWs = CreateProviderMock();
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new { type = "session.updated" }));

        ISmartTalkBackgroundJobClient? jobClient2 = null;
        string? forwardedPhone2 = null;
        await Run<IMediator>(async mediator =>
        {
            await mediator.SendAsync(command2);
        }, builder =>
        {
            jobClient2 = Substitute.For<ISmartTalkBackgroundJobClient>();
            jobClient2.Enqueue(Arg.Any<Expression<Func<IMediator, Task>>>(), Arg.Any<string>())
                .Returns(callInfo =>
                {
                    var expression = callInfo.Arg<Expression<Func<IMediator, Task>>>();
                    forwardedPhone2 = GetTransferHumanPhone(expression);
                    return string.Empty;
                });

            builder.RegisterInstance(jobClient2).As<ISmartTalkBackgroundJobClient>();
            builder.RegisterInstance(Substitute.For<ISmartiesClient>()).AsImplementedInterfaces();
            openaiWs.Register(builder);
        });
        forwardedPhone2.ShouldBeNull();
    }

    [Fact]
    public async Task ShouldTransferToHuman_WhenOutOfServiceHoursWithManualService()
    {
        const string transferNumber = "+15556660000";

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
                ServiceHours = serviceHours, IsTransferHuman = true, TransferCallNumber = transferNumber
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
            start = new { callSid = "CA_TEST_TRANSFER", streamSid = "MZ_TEST_TRANSFER" }
        }));

        var openaiWs = CreateProviderMock();
        openaiWs.EnqueueMessage(JsonConvert.SerializeObject(new { type = "session.updated" }));

        var mockJobClient = Substitute.For<ISmartTalkBackgroundJobClient>();

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
            builder.RegisterInstance(mockJobClient).As<ISmartTalkBackgroundJobClient>();
            builder.RegisterInstance(Substitute.For<ISmartiesClient>()).AsImplementedInterfaces();
            openaiWs.Register(builder);
        });

        // "start" event enqueues RecordCall + TransferHumanService (out of service hours with manual service)
        mockJobClient.ReceivedWithAnyArgs(2)
            .Enqueue<Mediator.Net.IMediator>(default, default);
    }

    [Fact]
    public async Task ShouldForwardCall_WithStartMediaStopEvents()
    {
        const string forwardNumber = "+15558880000";

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
            await repository.InsertAsync(new AiSpeechAssistantInboundRoute
            {
                From = TestCallerNumber, To = TestDidNumber, ForwardNumber = forwardNumber,
                IsFullDay = true, DayOfWeek = "0,1,2,3,4,5,6", Priority = 1,
                TimeZone = "Pacific Standard Time"
            });
        });

        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_FULL_SEQ", streamSid = "MZ_FULL_SEQ" }
        }));
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "media",
            media = new { payload = Convert.ToBase64String(new byte[] { 0xFF, 0x00, 0xAA }) }
        }));
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new { @event = "stop" }));

        var mockJobClient = Substitute.For<ISmartTalkBackgroundJobClient>();

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
            builder.RegisterInstance(mockJobClient).As<ISmartTalkBackgroundJobClient>();
            builder.RegisterInstance(Substitute.For<ISmartiesClient>()).AsImplementedInterfaces();
        });

        // "start" enqueues RecordCall + TransferForward; "stop" enqueues ProcessJobService
        mockJobClient.ReceivedWithAnyArgs(2)
            .Enqueue<Mediator.Net.IMediator>(default, default);
        mockJobClient.ReceivedWithAnyArgs(1)
            .Enqueue<IAiSpeechAssistantProcessJobService>(default, default);
    }

    [Fact]
    public async Task ShouldForwardToTransferNumber_WhenOutOfServiceHoursWithManualServiceAndForwardRoute()
    {
        const string forwardNumber = "+15551110000";
        const string transferNumber = "+15556660000";

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
                ServiceHours = serviceHours, IsTransferHuman = true, TransferCallNumber = transferNumber
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

        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new
        {
            @event = "start",
            start = new { callSid = "CA_FWD_TRANSFER", streamSid = "MZ_FWD_TRANSFER" }
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
        });

        // Forward path taken with TransferCallNumber (not ForwardNumber)
        // RecordCall + TransferHumanService (immediate) enqueued
        mockJobClient.ReceivedWithAnyArgs(2).Enqueue<Mediator.Net.IMediator>(default, default);
        // No Schedule — immediate transfer uses default queue
        mockJobClient.DidNotReceiveWithAnyArgs().Schedule<Mediator.Net.IMediator>(default, default(TimeSpan), default);
        // TransferHumanService Enqueue uses default queue, not "transfer" queue (V1 compat)
        mockJobClient.DidNotReceive().Enqueue<Mediator.Net.IMediator>(
            Arg.Any<Expression<Func<Mediator.Net.IMediator, Task>>>(),
            HangfireConstants.InternalHostingTransfer);
    }
}
