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

    [Fact]
    public async Task ShouldUpdateKnowledgeDetail_WhenSendingUpdateCommand()
    {
        int detailId = 0;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Name = "KnowledgeDetailAssistant", AnsweringNumber = TestDidNumber, ModelProvider = RealtimeAiProvider.OpenAi,
                ModelVoice = "alloy", IsDefault = true, IsDisplay = true
            };
            await repository.InsertAsync(assistant);
            await unitOfWork.SaveChangesAsync();

            var knowledge = new AiSpeechAssistantKnowledge
            {
                AssistantId = assistant.Id, Prompt = "Knowledge detail prompt", IsActive = true, Version = "1.0"
            };
            await repository.InsertAsync(knowledge);
            await unitOfWork.SaveChangesAsync();

            var detail = new AiSpeechAssistantKnowledgeDetail
            {
                KnowledgeId = knowledge.Id,
                KnowledgeName = "Old Detail Name",
                Content = "Old detail content",
                FormatType = AiSpeechAssistantKonwledgeFormatType.Text
            };
            await repository.InsertAsync(detail);
            await unitOfWork.SaveChangesAsync();
            detailId = detail.Id;
        });

        UpdateAiSpeechAssistantKnowledgeDetailResponse response = null!;

        await Run<IMediator>(async mediator =>
        {
            response = await mediator
                .SendAsync<UpdateAiSpeechAssistantKnowledgeDetailCommand, UpdateAiSpeechAssistantKnowledgeDetailResponse>(
                    new UpdateAiSpeechAssistantKnowledgeDetailCommand
                    {
                        DetailId = detailId,
                        DetailName = "Updated Detail Name",
                        DetailContent = "Updated detail content",
                        FormatType = AiSpeechAssistantKonwledgeFormatType.FAQ
                    });
        });

        response.ShouldNotBeNull();
        response.Data.ShouldNotBeNull();
        response.Data.Id.ShouldBe(detailId);
        response.Data.KnowledgeName.ShouldBe("Updated Detail Name");
        response.Data.Content.ShouldBe("Updated detail content");
        response.Data.FormatType.ShouldBe(AiSpeechAssistantKonwledgeFormatType.FAQ);

        await Run<IAiSpeechAssistantDataProvider>(async dataProvider =>
        {
            var updatedDetail = await dataProvider.GetAiSpeechAssistantKnowledgeDetailByDetailIdAsync(detailId);
            updatedDetail.ShouldNotBeNull();
            updatedDetail.KnowledgeName.ShouldBe("Updated Detail Name");
            updatedDetail.Content.ShouldBe("Updated detail content");
            updatedDetail.FormatType.ShouldBe(AiSpeechAssistantKonwledgeFormatType.FAQ);
            updatedDetail.LastModifiedDate.ShouldNotBeNull();
        });
    }

    [Fact]
    public async Task ShouldDeleteKnowledgeDetail_WhenSendingDeleteCommand()
    {
        int detailId = 0;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Name = "KnowledgeDetailDeleteAssistant", AnsweringNumber = TestDidNumber, ModelProvider = RealtimeAiProvider.OpenAi,
                ModelVoice = "alloy", IsDefault = true, IsDisplay = true
            };
            await repository.InsertAsync(assistant);
            await unitOfWork.SaveChangesAsync();

            var knowledge = new AiSpeechAssistantKnowledge
            {
                AssistantId = assistant.Id, Prompt = "Knowledge detail delete prompt", IsActive = true, Version = "1.0"
            };
            await repository.InsertAsync(knowledge);
            await unitOfWork.SaveChangesAsync();

            var detail = new AiSpeechAssistantKnowledgeDetail
            {
                KnowledgeId = knowledge.Id,
                KnowledgeName = "Delete Detail Name",
                Content = "Delete detail content",
                FormatType = AiSpeechAssistantKonwledgeFormatType.Text
            };
            await repository.InsertAsync(detail);
            await unitOfWork.SaveChangesAsync();
            detailId = detail.Id;
        });

        await Run<IMediator>(async mediator =>
        {
            await mediator.SendAsync<DeleteAiSpeechAssistantKnowledgeDetailCommand, DeleteAiSpeechAssistantKnowledgeDetailResponse>(
                new DeleteAiSpeechAssistantKnowledgeDetailCommand
                {
                    DetailId = detailId
                });
        });

        await Run<IAiSpeechAssistantDataProvider>(async dataProvider =>
        {
            var deletedDetail = await dataProvider.GetAiSpeechAssistantKnowledgeDetailByDetailIdAsync(detailId);
            deletedDetail.ShouldBeNull();
        });
    }

    [Fact]
    public async Task ShouldCreateKnowledgeDetails_WhenAddingKnowledgeWithDetails()
    {
        int assistantId = 0;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Name = "KnowledgeAddAssistant", AnsweringNumber = TestDidNumber, ModelProvider = RealtimeAiProvider.OpenAi,
                ModelVoice = "alloy", IsDefault = true, IsDisplay = true
            };
            await repository.InsertAsync(assistant);
            await unitOfWork.SaveChangesAsync();
            assistantId = assistant.Id;

            await repository.InsertAsync(new AiSpeechAssistantKnowledge
            {
                AssistantId = assistant.Id, Prompt = "Old prompt", IsActive = true, Version = "1.0"
            });
        });

        AddAiSpeechAssistantKnowledgeResponse response = null!;

        await Run<IMediator>(async mediator =>
        {
            response = await mediator.SendAsync<AddAiSpeechAssistantKnowledgeCommand, AddAiSpeechAssistantKnowledgeResponse>(
                new AddAiSpeechAssistantKnowledgeCommand
                {
                    AssistantId = assistantId,
                    Details =
                    [
                        new()
                        {
                            KnowledgeName = "Business Hours",
                            Content = "Mon-Fri 09:00-18:00",
                            FormatType = AiSpeechAssistantKonwledgeFormatType.Text
                        },
                        new()
                        {
                            KnowledgeName = "Delivery FAQ",
                            Content = "Delivery takes 30 minutes.",
                            FormatType = AiSpeechAssistantKonwledgeFormatType.FAQ
                        }
                    ]
                });
        });

        response.ShouldNotBeNull();
        response.Data.ShouldNotBeNull();
        response.Data.Id.ShouldBeGreaterThan(0);

        await Run<IAiSpeechAssistantDataProvider>(async dataProvider =>
        {
            var details = await dataProvider.GetKnowledgeDetailsByKnowledgeIdAsync(response.Data.Id, CancellationToken.None);

            details.ShouldNotBeNull();
            details.Count.ShouldBe(2);
            details.Any(x => x.KnowledgeName == "Business Hours" && x.Content == "Mon-Fri 09:00-18:00" && x.FormatType == AiSpeechAssistantKonwledgeFormatType.Text).ShouldBeTrue();
            details.Any(x => x.KnowledgeName == "Delivery FAQ" && x.Content == "Delivery takes 30 minutes." && x.FormatType == AiSpeechAssistantKonwledgeFormatType.FAQ).ShouldBeTrue();
        });
    }
}
