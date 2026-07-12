using Newtonsoft.Json;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Utils;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Enums.Agent;

namespace SmartTalk.Core.Services.Agents;

public interface IAgentTransferCallRoutingService : IScopedDependency
{
    Task<TimeZoneInfo> ResolveTimeZoneAsync(Agent agent, CancellationToken cancellationToken = default);

    (bool IsInServiceHours, bool IsEnableManualService) CheckIfInServiceHours(
        string serviceHoursJson, bool isTransferHuman, string transferCallNumber, DateTimeOffset utcNow,
        TimeZoneInfo timeZone = null);

    string SelectTransferCallNumber(
        List<AgentTransferCallConfig> transferCallConfigs, DateTimeOffset utcNow, TimeZoneInfo timeZone = null);

    string SelectDefaultTransferCallNumber(List<AgentTransferCallConfig> transferCallConfigs);
}

public sealed class AgentTransferCallRoutingService : IAgentTransferCallRoutingService
{
    private readonly IPosDataProvider _posDataProvider;

    public AgentTransferCallRoutingService(IPosDataProvider posDataProvider)
    {
        _posDataProvider = posDataProvider;
    }

    public async Task<TimeZoneInfo> ResolveTimeZoneAsync(Agent agent, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(agent.Timezone))
            return ResolveTimeZoneOrDefault(agent.Timezone);

        var store = await _posDataProvider.GetPosStoreByAgentIdAsync(agent.Id, cancellationToken).ConfigureAwait(false);

        return ResolveTimeZoneOrDefault(store?.Timezone);
    }

    public (bool IsInServiceHours, bool IsEnableManualService) CheckIfInServiceHours(
        string serviceHoursJson, bool isTransferHuman, string transferCallNumber, DateTimeOffset utcNow,
        TimeZoneInfo timeZone = null)
    {
        if (serviceHoursJson == null)
            return (true, isTransferHuman && !string.IsNullOrEmpty(transferCallNumber));

        timeZone ??= PstTimeZone.Get();
        var localTime = TimeZoneInfo.ConvertTime(utcNow, timeZone);

        var workingHours = JsonConvert.DeserializeObject<List<AgentServiceHoursDto>>(serviceHoursJson);
        var specificWorkingHours = workingHours?.FirstOrDefault(x => x.DayOfWeek == localTime.DayOfWeek);
        var localTimeToMinute = new TimeSpan(localTime.TimeOfDay.Hours, localTime.TimeOfDay.Minutes, 0);

        var isInService = specificWorkingHours != null &&
                          specificWorkingHours.Hours.Any(x => x.Start <= localTimeToMinute && x.End >= localTimeToMinute);

        return (isInService, isTransferHuman && !string.IsNullOrEmpty(transferCallNumber));
    }

    public string SelectTransferCallNumber(
        List<AgentTransferCallConfig> transferCallConfigs, DateTimeOffset utcNow, TimeZoneInfo timeZone = null)
    {
        timeZone ??= PstTimeZone.Get();

        return transferCallConfigs?
            .Where(x => IsInTransferCallServiceHours(x.ServiceHours, utcNow, timeZone))
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Id)
            .FirstOrDefault()?.TransferCallNumber;
    }

    public string SelectDefaultTransferCallNumber(List<AgentTransferCallConfig> transferCallConfigs)
    {
        return transferCallConfigs?
            .Where(x => x.Priority == AgentTransferCallPriority.Default)
            .OrderBy(x => x.Id)
            .FirstOrDefault()?.TransferCallNumber;
    }

    private static TimeZoneInfo ResolveTimeZoneOrDefault(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return PstTimeZone.Get();

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
        }
        catch (TimeZoneNotFoundException)
        {
            return PstTimeZone.Get();
        }
        catch (InvalidTimeZoneException)
        {
            return PstTimeZone.Get();
        }
    }

    private static bool IsInTransferCallServiceHours(
        string serviceHoursJson, DateTimeOffset utcNow, TimeZoneInfo timeZone)
    {
        if (string.IsNullOrWhiteSpace(serviceHoursJson)) return true;

        var localTime = TimeZoneInfo.ConvertTime(utcNow, timeZone);
        var workingHours = JsonConvert.DeserializeObject<List<AgentServiceHoursDto>>(serviceHoursJson);
        var specificWorkingHours = workingHours?.FirstOrDefault(x => x.DayOfWeek == localTime.DayOfWeek);
        var localTimeToMinute = new TimeSpan(localTime.TimeOfDay.Hours, localTime.TimeOfDay.Minutes, 0);

        return specificWorkingHours != null &&
               specificWorkingHours.Hours.Any(x => x.Start <= localTimeToMinute && x.End >= localTimeToMinute);
    }
}
