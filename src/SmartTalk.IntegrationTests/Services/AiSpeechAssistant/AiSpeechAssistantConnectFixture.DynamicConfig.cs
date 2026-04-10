using Mediator.Net;
using Shouldly;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;
using Xunit;

namespace SmartTalk.IntegrationTests.Services.AiSpeechAssistant;

public partial class AiSpeechAssistantConnectFixture
{
    [Fact]
    public async Task ShouldGetCurrentCompanyDynamicConfigs_WithTwoSystems_OnlyReturnActiveSystemBranch()
    {
        var caseId = Guid.NewGuid().ToString("N")[..8];
        int storeId = 0;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var now = DateTimeOffset.UtcNow;
            var (targetCompany, targetStore) = await CreateCompanyWithStoreAsync(repository, unitOfWork, $"{caseId}-target", now);
            var (otherCompany, _) = await CreateCompanyWithStoreAsync(repository, unitOfWork, $"{caseId}-other", now);
            storeId = targetStore.Id;

            var systemActive = new AiSpeechAssistantDynamicConfig
            {
                Name = $"SYS-ACTIVE-{caseId}",
                Level = AiSpeechAssistantDynamicConfigLevel.System,
                Status = true
            };
            await repository.InsertAsync(systemActive);
            await unitOfWork.SaveChangesAsync();

            var systemInactive = new AiSpeechAssistantDynamicConfig
            {
                Name = $"SYS-INACTIVE-{caseId}",
                Level = AiSpeechAssistantDynamicConfigLevel.System,
                Status = false
            };
            await repository.InsertAsync(systemInactive);
            await unitOfWork.SaveChangesAsync();

            var activeCategory = new AiSpeechAssistantDynamicConfig
            {
                Name = $"CAT-ACTIVE-{caseId}",
                Level = AiSpeechAssistantDynamicConfigLevel.Category,
                ParentId = systemActive.Id,
                Status = true
            };
            await repository.InsertAsync(activeCategory);
            await unitOfWork.SaveChangesAsync();

            var inactiveSystemCategory = new AiSpeechAssistantDynamicConfig
            {
                Name = $"CAT-UNDER-INACTIVE-SYS-{caseId}",
                Level = AiSpeechAssistantDynamicConfigLevel.Category,
                ParentId = systemInactive.Id,
                Status = true
            };
            await repository.InsertAsync(inactiveSystemCategory);
            await unitOfWork.SaveChangesAsync();

            await repository.InsertAsync(new AiSpeechAssistantDynamicConfig
            {
                Name = $"DATA-ACTIVE-{caseId}",
                Level = AiSpeechAssistantDynamicConfigLevel.Data,
                ParentId = activeCategory.Id,
                Status = true
            });

            await repository.InsertAsync(new AiSpeechAssistantDynamicConfig
            {
                Name = $"DATA-UNDER-INACTIVE-SYS-{caseId}",
                Level = AiSpeechAssistantDynamicConfigLevel.Data,
                ParentId = inactiveSystemCategory.Id,
                Status = true
            });

            await repository.InsertAsync(new AiSpeechAssistantDynamicConfigRelatingCompany
            {
                ConfigId = activeCategory.Id,
                CompanyId = targetCompany.Id,
                CompanyName = targetCompany.Name
            });

            await repository.InsertAsync(new AiSpeechAssistantDynamicConfigRelatingCompany
            {
                ConfigId = inactiveSystemCategory.Id,
                CompanyId = targetCompany.Id,
                CompanyName = targetCompany.Name
            });

            await repository.InsertAsync(new AiSpeechAssistantDynamicConfigRelatingCompany
            {
                ConfigId = activeCategory.Id,
                CompanyId = otherCompany.Id,
                CompanyName = otherCompany.Name
            });
        });

        await Run<IMediator>(async mediator =>
        {
            var response = await mediator.RequestAsync<GetCurrentCompanyDynamicConfigsRequest, GetCurrentCompanyDynamicConfigsResponse>(
                new GetCurrentCompanyDynamicConfigsRequest { StoreId = storeId });

            response.ShouldNotBeNull();
            response.Data.ShouldNotBeNull();
            response.Data.Store.ShouldNotBeNull();
            response.Data.Store.Id.ShouldBe(storeId);

            response.Data.Configs.Count.ShouldBe(1);
            var systemNode = response.Data.Configs.Single();
            systemNode.Level.ShouldBe(AiSpeechAssistantDynamicConfigLevel.System);
            systemNode.Status.ShouldBeTrue();
            systemNode.Name.ShouldBe($"SYS-ACTIVE-{caseId}");
            systemNode.Children.Count.ShouldBe(1);

            var categoryNode = systemNode.Children.Single();
            categoryNode.Level.ShouldBe(AiSpeechAssistantDynamicConfigLevel.Category);
            categoryNode.Name.ShouldBe($"CAT-ACTIVE-{caseId}");
            categoryNode.Children.Count.ShouldBe(1);
            categoryNode.Children.Single().Name.ShouldBe($"DATA-ACTIVE-{caseId}");

            response.Data.Configs.Any(x => x.Name == $"SYS-INACTIVE-{caseId}").ShouldBeFalse();
        });
    }

    [Fact]
    public async Task ShouldGetCurrentCompanyDynamicConfigs_WithThreeSystems_OnlyKeepTwoActiveBranches()
    {
        var caseId = Guid.NewGuid().ToString("N")[..8];
        int storeId = 0;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var now = DateTimeOffset.UtcNow;
            var (targetCompany, targetStore) = await CreateCompanyWithStoreAsync(repository, unitOfWork, $"{caseId}-main", now);
            var (_, _) = await CreateCompanyWithStoreAsync(repository, unitOfWork, $"{caseId}-extra", now);
            storeId = targetStore.Id;

            var system1 = new AiSpeechAssistantDynamicConfig
            {
                Name = $"SYS-1-{caseId}",
                Level = AiSpeechAssistantDynamicConfigLevel.System,
                Status = true
            };
            var system2 = new AiSpeechAssistantDynamicConfig
            {
                Name = $"SYS-2-{caseId}",
                Level = AiSpeechAssistantDynamicConfigLevel.System,
                Status = true
            };
            var system3 = new AiSpeechAssistantDynamicConfig
            {
                Name = $"SYS-3-{caseId}",
                Level = AiSpeechAssistantDynamicConfigLevel.System,
                Status = true
            };
            await repository.InsertAllAsync(new List<AiSpeechAssistantDynamicConfig> { system1, system2, system3 });
            await unitOfWork.SaveChangesAsync();

            var cat1 = new AiSpeechAssistantDynamicConfig
            {
                Name = $"CAT-1-{caseId}",
                Level = AiSpeechAssistantDynamicConfigLevel.Category,
                ParentId = system1.Id,
                Status = true
            };
            var cat2 = new AiSpeechAssistantDynamicConfig
            {
                Name = $"CAT-2-{caseId}",
                Level = AiSpeechAssistantDynamicConfigLevel.Category,
                ParentId = system2.Id,
                Status = true
            };
            var cat3False = new AiSpeechAssistantDynamicConfig
            {
                Name = $"CAT-3-FALSE-{caseId}",
                Level = AiSpeechAssistantDynamicConfigLevel.Category,
                ParentId = system3.Id,
                Status = false
            };
            await repository.InsertAllAsync(new List<AiSpeechAssistantDynamicConfig> { cat1, cat2, cat3False });
            await unitOfWork.SaveChangesAsync();

            await repository.InsertAllAsync(new List<AiSpeechAssistantDynamicConfig>
            {
                new()
                {
                    Name = $"DATA-1-{caseId}",
                    Level = AiSpeechAssistantDynamicConfigLevel.Data,
                    ParentId = cat1.Id,
                    Status = true
                },
                new()
                {
                    Name = $"DATA-2-{caseId}",
                    Level = AiSpeechAssistantDynamicConfigLevel.Data,
                    ParentId = cat2.Id,
                    Status = true
                },
                new()
                {
                    Name = $"DATA-3-{caseId}",
                    Level = AiSpeechAssistantDynamicConfigLevel.Data,
                    ParentId = cat3False.Id,
                    Status = true
                }
            });

            await repository.InsertAllAsync(new List<AiSpeechAssistantDynamicConfigRelatingCompany>
            {
                new() { ConfigId = cat1.Id, CompanyId = targetCompany.Id, CompanyName = targetCompany.Name },
                new() { ConfigId = cat2.Id, CompanyId = targetCompany.Id, CompanyName = targetCompany.Name },
                new() { ConfigId = cat3False.Id, CompanyId = targetCompany.Id, CompanyName = targetCompany.Name }
            });
        });

        await Run<IMediator>(async mediator =>
        {
            var response = await mediator.RequestAsync<GetCurrentCompanyDynamicConfigsRequest, GetCurrentCompanyDynamicConfigsResponse>(
                new GetCurrentCompanyDynamicConfigsRequest { StoreId = storeId });

            response.ShouldNotBeNull();
            response.Data.ShouldNotBeNull();
            response.Data.Store.ShouldNotBeNull();
            response.Data.Store.Id.ShouldBe(storeId);

            response.Data.Configs.Count.ShouldBe(2);
            response.Data.Configs.All(x => x.Level == AiSpeechAssistantDynamicConfigLevel.System).ShouldBeTrue();
            response.Data.Configs.Any(x => x.Name == $"SYS-1-{caseId}").ShouldBeTrue();
            response.Data.Configs.Any(x => x.Name == $"SYS-2-{caseId}").ShouldBeTrue();
            response.Data.Configs.Any(x => x.Name == $"SYS-3-{caseId}").ShouldBeFalse();

            var firstSystem = response.Data.Configs.Single(x => x.Name == $"SYS-1-{caseId}");
            firstSystem.Children.Count.ShouldBe(1);
            firstSystem.Children.Single().Name.ShouldBe($"CAT-1-{caseId}");
            firstSystem.Children.Single().Children.Count.ShouldBe(1);
            firstSystem.Children.Single().Children.Single().Name.ShouldBe($"DATA-1-{caseId}");

            var secondSystem = response.Data.Configs.Single(x => x.Name == $"SYS-2-{caseId}");
            secondSystem.Children.Count.ShouldBe(1);
            secondSystem.Children.Single().Name.ShouldBe($"CAT-2-{caseId}");
            secondSystem.Children.Single().Children.Count.ShouldBe(1);
            secondSystem.Children.Single().Children.Single().Name.ShouldBe($"DATA-2-{caseId}");

            response.Data.Configs.Any(x => x.Children.Any(c => c.Name == $"CAT-3-FALSE-{caseId}")).ShouldBeFalse();
        });
    }

    private static async Task<(Company Company, CompanyStore Store)> CreateCompanyWithStoreAsync(
        IRepository repository,
        IUnitOfWork unitOfWork,
        string suffix,
        DateTimeOffset now)
    {
        var company = new Company
        {
            Name = $"company-{suffix}",
            Description = $"company description {suffix}",
            Status = true,
            ServiceProviderId = null,
            CreatedBy = 1,
            CreatedDate = now,
            LastModifiedBy = 1,
            LastModifiedDate = now
        };
        await repository.InsertAsync(company);
        await unitOfWork.SaveChangesAsync();

        var store = new CompanyStore
        {
            CompanyId = company.Id,
            Names = $"store-{suffix}",
            Description = $"store description {suffix}",
            Status = true,
            PhoneNums = $"+1555{suffix[..Math.Min(6, suffix.Length)]}",
            Logo = $"logo-{suffix}.png",
            Address = $"address-{suffix}",
            Latitude = "22.2800",
            Longitude = "114.1600",
            Link = $"https://store-{suffix}.example.com",
            AppId = $"app-{suffix}",
            AppSecret = $"secret-{suffix}",
            TimePeriod = "09:00-18:00",
            PosName = $"pos-{suffix}",
            PosId = $"pos-id-{suffix}",
            IsLink = true,
            Timezone = "Pacific Standard Time",
            IsManualReview = false,
            IsTaskEnabled = true,
            CreatedBy = 1,
            CreatedDate = now,
            LastModifiedBy = 1,
            LastModifiedDate = now
        };
        await repository.InsertAsync(store);
        await unitOfWork.SaveChangesAsync();

        return (company, store);
    }
}
