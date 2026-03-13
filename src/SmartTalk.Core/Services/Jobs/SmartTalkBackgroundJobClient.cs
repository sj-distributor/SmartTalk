using Hangfire;
using Hangfire.States;
using Hangfire.Storage;
using SmartTalk.Core.Ioc;
using System.Linq.Expressions;

namespace SmartTalk.Core.Services.Jobs;

public interface ISmartTalkBackgroundJobClient : IScopedDependency
{
    string Enqueue<T>(Expression<Action> methodCall, string queue = "default");
    
    string Enqueue<T>(Expression<Action<T>> methodCall, string queue = "default");
    
    string Enqueue(Expression<Func<Task>> methodCall, string queue = "default");
    
    string Enqueue<T>(Expression<Func<T, Task>> methodCall, string queue = "default");

    string Schedule(Expression<Func<Task>> methodCall, TimeSpan delay, string queue = "default");
    
    string Schedule(Expression<Func<Task>> methodCall, DateTimeOffset enqueueAt, string queue = "default");

    string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay, string queue = "default");
    
    string Schedule<T>(Expression<Func<T, Task>> methodCall, DateTimeOffset enqueueAt, string queue = "default");
    
    string ContinueJobWith(string parentJobId, Expression<Func<Task>> methodCall, string queue = "default");
        
    string ContinueJobWith<T>(string parentJobId, Expression<Func<T,Task>> methodCall, string queue = "default");
    
    void AddOrUpdateRecurringJob<T>(string recurringJobId, Expression<Func<T, Task>> methodCall, string cronExpression, TimeZoneInfo timeZone = null, string queue = "default");
    
    bool DeleteJob(string jobId);

    void RemoveRecurringJobIfExists(string jobId);
    
    List<RecurringJobDto> GetRecurringJobs();
    
    StateData GetJobState(string jobId);
}

public class SmartTalkBackgroundJobClient : ISmartTalkBackgroundJobClient
{
    private readonly IBackgroundJobClient _client;
    private readonly IRecurringJobManager _recurringJobManager;

    public SmartTalkBackgroundJobClient(IBackgroundJobClient client, IRecurringJobManager recurringJobManager)
    {
        _client = client;
        _recurringJobManager = recurringJobManager;
    }

    public string Enqueue(Expression<Func<Task>> methodCall, string queue = "default")
    {
        return _client.Create(methodCall, new EnqueuedState(queue));
    }

    public string Enqueue<T>(Expression<Func<T, Task>> methodCall, string queue = "default")
    {
        return _client.Create(methodCall, new EnqueuedState(queue));
    }

    public string Enqueue<T>(Expression<Action> methodCall, string queue = "default")
    {
        return _client.Create(methodCall, new EnqueuedState(queue));
    }

    public string Enqueue<T>(Expression<Action<T>> methodCall, string queue = "default")
    {
        return _client.Create(methodCall, new EnqueuedState(queue));
    }

    public string Schedule(Expression<Func<Task>> methodCall, TimeSpan delay, string queue = "default")
    {
        return _client.Schedule(queue, methodCall, delay);
    }

    public string Schedule(Expression<Func<Task>> methodCall, DateTimeOffset enqueueAt, string queue = "default")
    {
        return _client.Schedule(queue, methodCall, enqueueAt);
    }

    public string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay, string queue = "default")
    {
        return _client.Schedule(queue, methodCall, delay);
    }

    public string Schedule<T>(Expression<Func<T, Task>> methodCall, DateTimeOffset enqueueAt, string queue = "default")
    {
        return _client.Schedule(queue, methodCall, enqueueAt);
    }

    public string ContinueJobWith(string parentJobId, Expression<Func<Task>> methodCall, string queue = "default")
    {
        return _client.ContinueJobWith(parentJobId, methodCall, new EnqueuedState(queue));
    }

    public string ContinueJobWith<T>(string parentJobId, Expression<Func<T, Task>> methodCall, string queue = "default")
    {
        return _client.ContinueJobWith(parentJobId, methodCall, new EnqueuedState(queue));
    }

    public void AddOrUpdateRecurringJob<T>(string recurringJobId, Expression<Func<T, Task>> methodCall, string cronExpression, TimeZoneInfo timeZone = null, string queue = "default")
    {
        _recurringJobManager.AddOrUpdate(recurringJobId, queue, methodCall, cronExpression, new RecurringJobOptions
        {
            TimeZone = timeZone ?? TimeZoneInfo.Utc
        });
    }

    public bool DeleteJob(string jobId)
    {
        return _client.Delete(jobId);
    }

    public void RemoveRecurringJobIfExists(string jobId)
    {
        _recurringJobManager.RemoveIfExists(jobId);
    }

    public List<RecurringJobDto> GetRecurringJobs()
    {
        return JobStorage.Current.GetConnection().GetRecurringJobs();
    }
    
    public StateData GetJobState(string jobId)
    {
        return JobStorage.Current.GetConnection().GetStateData(jobId);
    }
}