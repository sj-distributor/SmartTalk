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
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            // 连接池中空闲连接保持时间
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            // 连接空闲多久后发送 TCP Keep-Alive 心跳包，防止防火墙/代理断开连接
            KeepAlivePingDelay = TimeSpan.FromSeconds(60),
            // 心跳包响应超时时间
            KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
            // TCP 连接建立超时时间
            ConnectTimeout = TimeSpan.FromMinutes(2),
            // 读取响应剩余内容的超时时间
            ResponseDrainTimeout = TimeSpan.FromMinutes(5)
        });
    }
}