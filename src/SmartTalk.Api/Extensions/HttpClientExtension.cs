using Correlate;
using SmartTalk.Messages;

namespace SmartTalk.Api.Extensions;

public static class HttpClientExtension
{
    public static void AddHttpClientInternal(this IServiceCollection services)
    {
        services.AddHttpClient(string.Empty, (sp, c) =>
        {
            var correlationContextAccessor = sp.GetRequiredService<ICorrelationContextAccessor>();

            if (correlationContextAccessor.CorrelationContext == null) return;

            foreach (var correlationIdHeader in SmartTalkConstants.CorrelationIdHeaders)
            {
                c.DefaultRequestHeaders.Add(correlationIdHeader, correlationContextAccessor.CorrelationContext.CorrelationId);
            }
        });
    }
}