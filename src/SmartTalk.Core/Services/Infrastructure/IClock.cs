using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.Infrastructure
{
    public interface IClock : IScopedDependency
    {
        DateTimeOffset Now { get; }
        
        DateTime DateTimeNow { get; }
    }
}