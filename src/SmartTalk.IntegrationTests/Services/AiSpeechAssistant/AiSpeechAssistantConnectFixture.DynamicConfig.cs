using Mediator.Net;
using Shouldly;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;
using Xunit;

namespace SmartTalk.IntegrationTests.Services.AiSpeechAssistant;

public partial class AiSpeechAssistantConnectFixture
{
    [Fact]
    public async Task ShouldGetCurrentCompanyDynamicConfigs_WhenCompanyIsBound()
    {
        int storeId = 0;
        int activeConfigId = 0;
        int inactiveConfigId = 0;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var company = new Company
            {
                Name = "福滿樓",
                Description = "test",
                Status = true,
                IsBindConfig = true,
                CreatedDate = DateTimeOffset.Now
            };
            await repository.InsertAsync(company);
            await unitOfWork.SaveChangesAsync();

            var store = new CompanyStore
            {
                CompanyId = company.Id,
                Names = "福滿樓-中環",
                Address = "test",
                Status = true,
                CreatedDate = DateTimeOffset.Now
            };
            await repository.InsertAsync(store);
            await unitOfWork.SaveChangesAsync();
            storeId = store.Id;

            var active = new AiSpeechAssistantDynamicConfig
            {
                Name = "POS",
                Level = AiSpeechAssistantDynamicConfigLevel.System,
                Status = true
            };
            await repository.InsertAsync(active);
            await unitOfWork.SaveChangesAsync();
            activeConfigId = active.Id;

            var inactive = new AiSpeechAssistantDynamicConfig
            {
                Name = "Hifood",
                Level = AiSpeechAssistantDynamicConfigLevel.System,
                Status = false
            };
            await repository.InsertAsync(inactive);
            await unitOfWork.SaveChangesAsync();
            inactiveConfigId = inactive.Id;
        });

        GetCurrentCompanyDynamicConfigsResponse response = null!;
        await Run<IMediator>(async mediator =>
        {
            response = await mediator.RequestAsync<GetCurrentCompanyDynamicConfigsRequest, GetCurrentCompanyDynamicConfigsResponse>(
                new GetCurrentCompanyDynamicConfigsRequest { StoreId = storeId });
        });

        response.ShouldNotBeNull();
        response.Data.ShouldNotBeNull();
        response.Data.Store.ShouldNotBeNull();
        response.Data.Configs.Any(x => x.Id == activeConfigId).ShouldBeTrue();
        response.Data.Configs.Any(x => x.Id == inactiveConfigId).ShouldBeFalse();
    }

    [Fact]
    public async Task ShouldReturnEmptyCurrentCompanyDynamicConfigs_WhenCompanyIsNotBound()
    {
        int storeId = 0;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var company = new Company
            {
                Name = "江南春",
                Description = "test",
                Status = true,
                IsBindConfig = false,
                CreatedDate = DateTimeOffset.Now
            };
            await repository.InsertAsync(company);
            await unitOfWork.SaveChangesAsync();

            var store = new CompanyStore
            {
                CompanyId = company.Id,
                Names = "江南春-灣仔",
                Address = "test",
                Status = true,
                CreatedDate = DateTimeOffset.Now
            };
            await repository.InsertAsync(store);
            await unitOfWork.SaveChangesAsync();
            storeId = store.Id;

            await repository.InsertAsync(new AiSpeechAssistantDynamicConfig
            {
                Name = "POS",
                Level = AiSpeechAssistantDynamicConfigLevel.System,
                Status = true
            });
        });

        GetCurrentCompanyDynamicConfigsResponse response = null!;
        await Run<IMediator>(async mediator =>
        {
            response = await mediator.RequestAsync<GetCurrentCompanyDynamicConfigsRequest, GetCurrentCompanyDynamicConfigsResponse>(
                new GetCurrentCompanyDynamicConfigsRequest { StoreId = storeId });
        });

        response.ShouldNotBeNull();
        response.Data.ShouldNotBeNull();
        response.Data.Configs.ShouldBeEmpty();
    }

    [Fact]
    public async Task ShouldGetDynamicConfigsAsTree_WhenSendingGetRequest()
    {
        int systemPosId = 0;
        int categoryPriceId = 0;
        int dataProductPriceId = 0;
        int companyAId = 0;
        int companyBId = 0;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var companyA = new Company
            {
                Name = "福滿樓",
                Description = "test",
                Status = true,
                IsBindConfig = true,
                CreatedDate = DateTimeOffset.Now
            };
            await repository.InsertAsync(companyA);
            await unitOfWork.SaveChangesAsync();
            companyAId = companyA.Id;

            var companyB = new Company
            {
                Name = "江南春",
                Description = "test",
                Status = true,
                IsBindConfig = false,
                CreatedDate = DateTimeOffset.Now
            };
            await repository.InsertAsync(companyB);
            await unitOfWork.SaveChangesAsync();
            companyBId = companyB.Id;

            var systemPos = new AiSpeechAssistantDynamicConfig
            {
                Name = "POS",
                Level = AiSpeechAssistantDynamicConfigLevel.System,
                ParentId = null,
                Status = true
            };
            await repository.InsertAsync(systemPos);
            await unitOfWork.SaveChangesAsync();
            systemPosId = systemPos.Id;

            var systemHifood = new AiSpeechAssistantDynamicConfig
            {
                Name = "Hifood",
                Level = AiSpeechAssistantDynamicConfigLevel.System,
                ParentId = null,
                Status = true
            };
            await repository.InsertAsync(systemHifood);
            await unitOfWork.SaveChangesAsync();

            var categoryPrice = new AiSpeechAssistantDynamicConfig
            {
                Name = "价格",
                Level = AiSpeechAssistantDynamicConfigLevel.Category,
                ParentId = systemPosId,
                Status = true
            };
            await repository.InsertAsync(categoryPrice);
            await unitOfWork.SaveChangesAsync();
            categoryPriceId = categoryPrice.Id;

            var dataProductPrice = new AiSpeechAssistantDynamicConfig
            {
                Name = "商品价格",
                Level = AiSpeechAssistantDynamicConfigLevel.Data,
                ParentId = categoryPriceId,
                Status = true
            };
            await repository.InsertAsync(dataProductPrice);
            await unitOfWork.SaveChangesAsync();
            dataProductPriceId = dataProductPrice.Id;
        });

        GetAiSpeechAssistantDynamicConfigsResponse allResponse = null!;
        GetAiSpeechAssistantDynamicConfigsResponse oneResponse = null!;

        await Run<IMediator>(async mediator =>
        {
            allResponse = await mediator.RequestAsync<GetAiSpeechAssistantDynamicConfigsRequest, GetAiSpeechAssistantDynamicConfigsResponse>(
                new GetAiSpeechAssistantDynamicConfigsRequest());

            oneResponse = await mediator.RequestAsync<GetAiSpeechAssistantDynamicConfigsRequest, GetAiSpeechAssistantDynamicConfigsResponse>(
                new GetAiSpeechAssistantDynamicConfigsRequest { Id = systemPosId });
        });

        allResponse.ShouldNotBeNull();
        allResponse.Data.ShouldNotBeNull();
        allResponse.Data.Configs.Count.ShouldBeGreaterThanOrEqualTo(2);

        var posNode = allResponse.Data.Configs.FirstOrDefault(x => x.Id == systemPosId);
        posNode.ShouldNotBeNull();
        posNode.Children.Any(x => x.Id == categoryPriceId).ShouldBeTrue();
        posNode.Children.First(x => x.Id == categoryPriceId).Children.Any(x => x.Id == dataProductPriceId).ShouldBeTrue();

        oneResponse.ShouldNotBeNull();
        oneResponse.Data.ShouldNotBeNull();
        oneResponse.Data.Configs.Count.ShouldBe(1);
        oneResponse.Data.Configs.First().Id.ShouldBe(systemPosId);
        oneResponse.Data.Configs.First().Children.Any(x => x.Id == categoryPriceId).ShouldBeTrue();
        oneResponse.Data.Companies.Count.ShouldBe(1);
        oneResponse.Data.Companies.Any(x => x.Id == companyAId && x.IsBindConfig).ShouldBeTrue();
        oneResponse.Data.Companies.Any(x => x.Id == companyBId).ShouldBeFalse();
    }

    [Fact]
    public async Task ShouldUpdateDynamicConfig_WhenSendingUpdateCommand()
    {
        int systemPosId = 0;
        int categoryPriceId = 0;
        int dataId = 0;
        int companyAId = 0;
        int companyBId = 0;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var companyA = new Company
            {
                Name = "福滿樓",
                Description = "test",
                Status = true,
                IsBindConfig = false,
                CreatedDate = DateTimeOffset.Now
            };
            await repository.InsertAsync(companyA);
            await unitOfWork.SaveChangesAsync();
            companyAId = companyA.Id;

            var companyB = new Company
            {
                Name = "江南春",
                Description = "test",
                Status = true,
                IsBindConfig = true,
                CreatedDate = DateTimeOffset.Now
            };
            await repository.InsertAsync(companyB);
            await unitOfWork.SaveChangesAsync();
            companyBId = companyB.Id;

            var systemPos = new AiSpeechAssistantDynamicConfig
            {
                Name = "POS",
                Level = AiSpeechAssistantDynamicConfigLevel.System,
                Status = true
            };
            await repository.InsertAsync(systemPos);
            await unitOfWork.SaveChangesAsync();
            systemPosId = systemPos.Id;

            var categoryPrice = new AiSpeechAssistantDynamicConfig
            {
                Name = "价格",
                Level = AiSpeechAssistantDynamicConfigLevel.Category,
                ParentId = systemPosId,
                Status = true
            };
            await repository.InsertAsync(categoryPrice);
            await unitOfWork.SaveChangesAsync();
            categoryPriceId = categoryPrice.Id;

            var data = new AiSpeechAssistantDynamicConfig
            {
                Name = "商品价格",
                Level = AiSpeechAssistantDynamicConfigLevel.Data,
                ParentId = categoryPriceId,
                Status = true
            };
            await repository.InsertAsync(data);
            await unitOfWork.SaveChangesAsync();
            dataId = data.Id;
        });

        UpdateAiSpeechAssistantDynamicConfigResponse response = null!;

        await Run<IMediator>(async mediator =>
        {
            response = await mediator.SendAsync<UpdateAiSpeechAssistantDynamicConfigCommand, UpdateAiSpeechAssistantDynamicConfigResponse>(
                new UpdateAiSpeechAssistantDynamicConfigCommand
                {
                    Id = dataId,
                    Status = false,
                    CompanyIds = [companyAId]
                });
        });

        response.ShouldNotBeNull();
        response.Data.ShouldNotBeNull();
        response.Data.Id.ShouldBe(dataId);
        response.Data.Status.ShouldBeFalse();

        await Run<IAiSpeechAssistantDataProvider>(async dataProvider =>
        {
            var updated = await dataProvider.GetAiSpeechAssistantDynamicConfigByIdAsync(dataId);
            updated.ShouldNotBeNull();
            updated.Status.ShouldBeFalse();
        });

        await Run<IPosDataProvider>(async posDataProvider =>
        {
            var (_, companies) = await posDataProvider.GetPosCompaniesAsync(companyIds: [companyAId, companyBId]);
            companies.Count.ShouldBe(2);
            companies.Single(x => x.Id == companyAId).IsBindConfig.ShouldBeTrue();
            companies.Single(x => x.Id == companyBId).IsBindConfig.ShouldBeFalse();
        });
    }
}
