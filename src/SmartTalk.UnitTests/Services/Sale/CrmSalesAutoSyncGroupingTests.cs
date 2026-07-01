using SmartTalk.Core.Services.Sale;
using Xunit;
using SmartTalk.Messages.Dto.Crm;
using SmartTalk.Messages.Dto.Sales;

namespace SmartTalk.UnitTests.Services.Sale;

public class CrmSalesAutoSyncGroupingTests
{
    [Fact]
    public void BuildCustomerGroups_MergesCustomersWithSharedPhoneUnderSameSales()
    {
        var customers = new List<CrmSalesAutoSyncCustomerDto>
        {
            CreateCustomer("100", "Alice", "5551110001"),
            CreateCustomer("200", "Alice", "5551110001"),
            CreateCustomer("300", "Alice", "5552220002")
        };

        var groups = CrmSalesAutoSyncGrouping.BuildCustomerGroups(customers);

        Assert.Equal(2, groups.Count);
        Assert.Contains(groups, x => x.CustomerIds.SequenceEqual(new[] { "100", "200" }));
        Assert.Contains(groups, x => x.CustomerIds.SequenceEqual(new[] { "300" }));
    }

    [Fact]
    public void BuildCustomerGroups_DoesNotMergeCustomersWithoutContacts()
    {
        var customers = new List<CrmSalesAutoSyncCustomerDto>
        {
            CreateCustomer("100", "Alice", null),
            CreateCustomer("200", "Bob", null)
        };

        var groups = CrmSalesAutoSyncGrouping.BuildCustomerGroups(customers);

        Assert.Equal(2, groups.Count);
        Assert.All(groups, x => Assert.Single(x.CustomerIds));
    }

    [Fact]
    public void BuildAssistantName_JoinsIdsWithSlash()
    {
        var name = CrmSalesAutoSyncGrouping.BuildAssistantName(["200", "100"], "English");

        Assert.Equal("100/200", name);
    }

    [Fact]
    public void TryParseAssistantName_ParsesLegacyMergedIds()
    {
        var parsed = CrmSalesAutoSyncGrouping.TryParseAssistantName("100/200 (English)", out var ids, out var language);

        Assert.True(parsed);
        Assert.Equal(["100", "200"], ids);
        Assert.Equal("English", language);
    }

    [Fact]
    public void TryParseAssistantName_ParsesMergedIdsWithoutLanguageSuffix()
    {
        var parsed = CrmSalesAutoSyncGrouping.TryParseAssistantName("100/200", out var ids, out var language);

        Assert.True(parsed);
        Assert.Equal(["100", "200"], ids);
        Assert.Null(language);
    }

    [Fact]
    public void BuildSalesKey_TrimsSalesGroupWhenMissing()
    {
        var customer = new CrmSalesAutoSyncCustomerDto
        {
            SalesName = "Jessica.C",
            SalesGroup = null
        };

        Assert.Equal("Jessica.C", CrmSalesAutoSyncGrouping.BuildSalesKey(customer));
    }

    [Fact]
    public void BuildCustomerGroups_DoesNotMergeCustomersUnderDifferentSales()
    {
        var customers = new List<CrmSalesAutoSyncCustomerDto>
        {
            CreateCustomer("100", "Alice", "017", "5551110001", null),
            CreateCustomer("200", "Bob", "018", "5551110001", null)
        };

        var groups = CrmSalesAutoSyncGrouping.BuildCustomerGroups(customers);

        Assert.Equal(2, groups.Count);
        Assert.All(groups, x => Assert.Single(x.CustomerIds));
    }

    [Fact]
    public void BuildCustomerGroups_MergesCustomersWithSamePhoneEvenWhenIdentityDiffers()
    {
        var customers = new List<CrmSalesAutoSyncCustomerDto>
        {
            CreateCustomer("100", "Alice", "GroupA", "5551110001", "owner"),
            CreateCustomer("200", "Alice", "GroupA", "5551110001", "manager")
        };

        var groups = CrmSalesAutoSyncGrouping.BuildCustomerGroups(customers);

        Assert.Single(groups);
        Assert.Equal(new[] { "100", "200" }, groups[0].CustomerIds);
    }

    private static CrmSalesAutoSyncCustomerDto CreateCustomer(string sapId, string salesName, string phone)
        => CreateCustomer(sapId, salesName, "GroupA", phone, null);

    private static CrmSalesAutoSyncCustomerDto CreateCustomer(string sapId, string salesName, string salesGroup, string phone, string identity)
    {
        return new CrmSalesAutoSyncCustomerDto
        {
            CustomerId = sapId,
            SalesName = salesName,
            SalesGroup = salesGroup,
            Language = "English",
            Contacts = string.IsNullOrWhiteSpace(phone)
                ? null
                : new List<ContactDto> { new() { Phone = phone, Identity = identity } }
        };
    }
}
