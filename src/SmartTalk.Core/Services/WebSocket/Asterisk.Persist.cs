using System.Text;
using Newtonsoft.Json;
using SmartTalk.Messages.Dto.WebSocket;

namespace SmartTalk.Core.Services.WebSocket;

public partial class Asterisk
{
    private async Task GetOrAddPhoneOrderAsync(string? question = null, string? answer = null, string? url = null, CancellationToken cancellationToken = default)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCIsImN0eSI6IkpXVCJ9.eyJ1bmlxdWVfbmFtZSI6ImFkbWluIiwibmFtZWlkIjoiMSIsImh0dHA6Ly9zY2hlbWFzLnhtbHNvYXAub3JnL3dzLzIwMDUvMDUvaWRlbnRpdHkvY2xhaW1zL2F1dGhlbnRpY2F0aW9uIjoiU2VsZiIsInJvbGUiOiJBZG1pbmlzdHJhdG9yIiwibmJmIjoxNjk4MzYxNjM3LCJleHAiOjE2OTgzNjUyMzcsImlhdCI6MTY5ODM2MTYzN30.GgerOfhLQdUUq9bF7PyKHRq49JB7fcYaXJ4o9VyVq1c");

        var response = await client.GetAsync(
            $"https://testsmarties.yamimeal.ca/api/PhoneOrder/conversations/detail?sessionId={SessionId}", cancellationToken).ConfigureAwait(false);

        Console.WriteLine("Get redis data" + response);
        
        var orders = await response.Content.ReadAsAsync<PhoneOrderDto>(cancellationToken).ConfigureAwait(false) ?? new PhoneOrderDto();

        orders.FoodItem = OrderedFoods.Select(x => new PhoneOrderFoodItemDto()
        {
            FoodName = x.CineseName,
            Quantity = x.Quantity,
            Price = x.Price,
            Note = x.Notes,
            SessionId = SessionId
        }).ToList();

        if (!string.IsNullOrEmpty(question) && !string.IsNullOrEmpty(answer))
        {
            orders.ConversationDetail.Add(new PhoneOrderConversationDetailDto
            {
                SessionId = SessionId,
                Question = question,
                Answer = answer,
                CreateDate = DateTimeOffset.Now
            });
        }
        
        var content = new StringContent(JsonConvert.SerializeObject(orders), Encoding.UTF8, "application/json");
        var addResponse = await client.PostAsync("https://testsmarties.yamimeal.ca/api/PhoneOrder/conversation/detail/add", content, cancellationToken).ConfigureAwait(false);
    }
}