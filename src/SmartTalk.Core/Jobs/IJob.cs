using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Jobs;

public interface IJob : IScopedDependency
{
    Task Execute();
    
    string JobId { get; }
}