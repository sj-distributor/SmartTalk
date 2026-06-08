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
            CreateCustomer("200", "Bob", "5551110001"),
            CreateCustomer("300", "Carol", "5552220002")
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

        Assert.Equal("100/200 (English)", name);
    }

    [Fact]
    public void TryParseAssistantName_ParsesMergedIds()
    {
        var parsed = CrmSalesAutoSyncGrouping.TryParseAssistantName("100/200 (English)", out var ids, out var language);

        Assert.True(parsed);
        Assert.Equal(["100", "200"], ids);
        Assert.Equal("English", language);
    }

    private static CrmSalesAutoSyncCustomerDto CreateCustomer(string sapId, string salesName, string phone)
    {
        return new CrmSalesAutoSyncCustomerDto
        {
            CustomerId = sapId,
            SalesName = salesName,
            SalesGroup = "GroupA",
            Language = "English",
            Contacts = string.IsNullOrWhiteSpace(phone)
                ? null
                : new List<ContactDto> { new() { Phone = phone } }
        };
    }
}
