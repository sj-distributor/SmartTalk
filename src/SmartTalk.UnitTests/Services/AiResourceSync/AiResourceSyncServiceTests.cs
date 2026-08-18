using NSubstitute;
using Microsoft.Extensions.Configuration;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Services.AiResourceSync;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Messages.Dto.AiResourceSync;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiResourceSync;

public class AiResourceSyncServiceTests
{
    [Fact]
    public async Task RefreshCrmCustomerContactPhoneMapsAsync_RefreshesMultipleContactPhoneMappingsForSameCustomer()
    {
        var customer = new CrmSalesAutoSyncCustomerDto
        {
            CustomerId = "118895",
            CustomerName = "PHO DAY",
            SalesName = "TIFFANY.X",
            SalesGroup = "008",
            Language = "中文",
            Contacts =
            [
                new() { Name = "NICOLE", Phone = "415-218-2467", Identity = "老闆", Language = "粵語" },
                new() { Name = "JINGXIAN", Phone = "415-535-7933", Identity = "未知", Language = "粵語" },
                new() { Name = "STEVEN", Phone = "415-407-6788", Identity = "未知", Language = "粵語" }
            ]
        };

        var crmClient = Substitute.For<ICrmClient>();
        crmClient.GetSalesAutoSyncCustomersAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(([customer], 1));

        var posDataProvider = Substitute.For<IPosDataProvider>();
        posDataProvider.GetPosCompanyByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SmartTalk.Core.Domain.Pos.Company { Id = 1, Name = "OME" });
        posDataProvider.GetPosCompanyStoresAsync(
                Arg.Any<List<int>>(), Arg.Any<List<int>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
            [
                new SmartTalk.Core.Domain.Pos.CompanyStore
                {
                    Id = 10,
                    CompanyId = 1,
                    CreatedDate = DateTimeOffset.UtcNow,
                    Names = "{\"en\":{\"name\":\"TIFFANY.X 008\"},\"cn\":{\"name\":\"TIFFANY.X 008\"}}"
                }
            ]);
        posDataProvider.GetPosAgentsAsync(Arg.Any<List<int>>(), null, Arg.Any<CancellationToken>())
            .Returns(
            [
                new PosAgent { StoreId = 10, AgentId = 201, CreatedDate = DateTimeOffset.UtcNow }
            ]);

        var aiSpeechAssistantDataProvider = Substitute.For<IAiSpeechAssistantDataProvider>();
        aiSpeechAssistantDataProvider.GetCrmAutoSyncAssistantByStoreAndNameAsync(10, "118895", Arg.Any<CancellationToken>())
            .Returns(new SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Id = 9801,
                AgentId = 201,
                Name = "118895"
            });
        aiSpeechAssistantDataProvider.GetCrmCustomerContactPhoneMapsByCompanyIdAsync(1, Arg.Any<CancellationToken>())
            .Returns([]);
        aiSpeechAssistantDataProvider.HasCrmCustomerContactPhoneMapsAsync(Arg.Any<CancellationToken>()).Returns(false);

        List<SmartTalk.Core.Domain.Sales.CrmCustomerContactPhoneMap> capturedMappings = null;
        aiSpeechAssistantDataProvider
            .When(x => x.AddCrmCustomerContactPhoneMapsAsync(Arg.Any<List<SmartTalk.Core.Domain.Sales.CrmCustomerContactPhoneMap>>(), true, Arg.Any<CancellationToken>()))
            .Do(call => capturedMappings = call.Arg<List<SmartTalk.Core.Domain.Sales.CrmCustomerContactPhoneMap>>());

        var sut = CreateSut(
            crmClient: crmClient,
            posDataProvider: posDataProvider,
            aiSpeechAssistantDataProvider: aiSpeechAssistantDataProvider);

        await sut.RefreshCrmCustomerContactPhoneMapsAsync(CancellationToken.None);

        Assert.NotNull(capturedMappings);
        Assert.Equal(3, capturedMappings.Count);
        Assert.All(capturedMappings, x =>
        {
            Assert.Equal("118895", x.CustomerId);
            Assert.Equal("PHO DAY", x.CustomerName);
            Assert.Equal(9801, x.AssistantId);
            Assert.Equal(201, x.AgentId);
        });
        Assert.Contains(capturedMappings, x => x.ContactPhoneNormalized == "4152182467" && x.ContactName == "NICOLE");
        Assert.Contains(capturedMappings, x => x.ContactPhoneNormalized == "4155357933" && x.ContactName == "JINGXIAN");
        Assert.Contains(capturedMappings, x => x.ContactPhoneNormalized == "4154076788" && x.ContactName == "STEVEN");
        await aiSpeechAssistantDataProvider.Received(1).HasCrmCustomerContactPhoneMapsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshCrmCustomerContactPhoneMapsAsync_UsesChangedCustomersAfterInitialSync()
    {
        var customer = new CrmSalesAutoSyncCustomerDto
        {
            CustomerId = "118895",
            CustomerName = "PHO DAY",
            SalesName = "TIFFANY.X",
            SalesGroup = "008",
            Language = "中文",
            Contacts =
            [
                new() { Name = "NICOLE", Phone = "415-218-2467", Identity = "老闆", Language = "粵語" }
            ]
        };

        var crmClient = Substitute.For<ICrmClient>();
        crmClient.GetChangedSalesAutoSyncCustomersAsync(Arg.Any<CancellationToken>())
            .Returns([customer]);

        var posDataProvider = Substitute.For<IPosDataProvider>();
        posDataProvider.GetPosCompanyByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SmartTalk.Core.Domain.Pos.Company { Id = 1, Name = "OME" });
        posDataProvider.GetPosCompanyStoresAsync(
                Arg.Any<List<int>>(), Arg.Any<List<int>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
            [
                new SmartTalk.Core.Domain.Pos.CompanyStore
                {
                    Id = 10,
                    CompanyId = 1,
                    CreatedDate = DateTimeOffset.UtcNow,
                    Names = "{\"en\":{\"name\":\"TIFFANY.X 008\"},\"cn\":{\"name\":\"TIFFANY.X 008\"}}"
                }
            ]);

        var aiSpeechAssistantDataProvider = Substitute.For<IAiSpeechAssistantDataProvider>();
        aiSpeechAssistantDataProvider.GetCrmAutoSyncAssistantByStoreAndNameAsync(10, "118895", Arg.Any<CancellationToken>())
            .Returns(new SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Id = 9801,
                AgentId = 201,
                Name = "118895"
            });
        aiSpeechAssistantDataProvider.GetCrmCustomerContactPhoneMapsByCompanyIdAsync(1, Arg.Any<CancellationToken>())
            .Returns([]);
        aiSpeechAssistantDataProvider.HasCrmCustomerContactPhoneMapsAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = CreateSut(
            crmClient: crmClient,
            posDataProvider: posDataProvider,
            aiSpeechAssistantDataProvider: aiSpeechAssistantDataProvider);

        await sut.RefreshCrmCustomerContactPhoneMapsAsync(CancellationToken.None);

        await crmClient.Received(1).GetChangedSalesAutoSyncCustomersAsync(Arg.Any<CancellationToken>());
        await crmClient.DidNotReceive().GetSalesAutoSyncCustomersAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshCrmCustomerContactPhoneMapsAsync_NotDeactivateMissingMappingsIncrementalSync()
    {
        var customer = new CrmSalesAutoSyncCustomerDto
        {
            CustomerId = "118895",
            CustomerName = "PHO DAY",
            SalesName = "TIFFANY.X",
            SalesGroup = "008",
            Language = "中文",
            Contacts =
            [
                new() { Name = "NICOLE", Phone = "415-218-2467", Identity = "老闆", Language = "粵語" }
            ]
        };

        var crmClient = Substitute.For<ICrmClient>();
        crmClient.GetChangedSalesAutoSyncCustomersAsync(Arg.Any<CancellationToken>())
            .Returns([customer]);

        var posDataProvider = Substitute.For<IPosDataProvider>();
        posDataProvider.GetPosCompanyByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SmartTalk.Core.Domain.Pos.Company { Id = 1, Name = "OME" });
        posDataProvider.GetPosCompanyStoresAsync(
                Arg.Any<List<int>>(), Arg.Any<List<int>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
            [
                new SmartTalk.Core.Domain.Pos.CompanyStore
                {
                    Id = 10,
                    CompanyId = 1,
                    CreatedDate = DateTimeOffset.UtcNow,
                    Names = "{\"en\":{\"name\":\"TIFFANY.X 008\"},\"cn\":{\"name\":\"TIFFANY.X 008\"}}"
                }
            ]);

        var existingMapping = new SmartTalk.Core.Domain.Sales.CrmCustomerContactPhoneMap
        {
            Id = 88,
            CompanyId = 1,
            AgentId = 201,
            AssistantId = 9801,
            CustomerId = "118895",
            CustomerName = "PHO DAY",
            ContactName = "OLD",
            ContactPhoneNormalized = "4150000000",
            CreatedDate = DateTimeOffset.UtcNow.AddDays(-1),
            LastModifiedDate = DateTimeOffset.UtcNow.AddDays(-1)
        };

        var aiSpeechAssistantDataProvider = Substitute.For<IAiSpeechAssistantDataProvider>();
        aiSpeechAssistantDataProvider.GetCrmAutoSyncAssistantByStoreAndNameAsync(10, "118895", Arg.Any<CancellationToken>())
            .Returns(new SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Id = 9801,
                AgentId = 201,
                Name = "118895"
            });
        aiSpeechAssistantDataProvider.GetCrmCustomerContactPhoneMapsByCompanyIdAsync(1, Arg.Any<CancellationToken>())
            .Returns([existingMapping]);
        aiSpeechAssistantDataProvider.HasCrmCustomerContactPhoneMapsAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = CreateSut(
            crmClient: crmClient,
            posDataProvider: posDataProvider,
            aiSpeechAssistantDataProvider: aiSpeechAssistantDataProvider);

        await sut.RefreshCrmCustomerContactPhoneMapsAsync(CancellationToken.None);

        await aiSpeechAssistantDataProvider.DidNotReceive().UpdateCrmCustomerContactPhoneMapsAsync(
            Arg.Is<List<SmartTalk.Core.Domain.Sales.CrmCustomerContactPhoneMap>>(x => x.Any(m => m.Id == 88)),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    private static AiResourceSyncService CreateSut(
        ICrmClient crmClient,
        IPosDataProvider posDataProvider,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        return new AiResourceSyncService(
            crmClient,
            posDataProvider,
            aiSpeechAssistantDataProvider,
            Substitute.For<ISalesDataProvider>(),
            new SmartTalk.Core.Settings.Sales.SalesSetting(new ConfigurationBuilder().Build()) { CompanyName = "OME" });
    }
}
