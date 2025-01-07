using AlibabaCloud.SDK.Alidns20150109.Models;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.AliYun;

namespace SmartTalk.Core.Services.Http.Clients;

public interface IAlidnsClient : IScopedDependency
{
    Task<UpdateDomainRecordResponseBody> UpdateDomainRecordAsync(
        string describeDomain, string endpoint, string hostRecords, string value, CancellationToken cancellationToken);
}

public class AlidnsClient : IAlidnsClient
{
    private readonly AliYunSettings _aliYunSettings;

    public AlidnsClient(AliYunSettings aliYunSettings)
    {
        _aliYunSettings = aliYunSettings;
    }

    private AlibabaCloud.SDK.Alidns20150109.Client CreateClient(string endpoint)
    {
        var config = new AlibabaCloud.OpenApiClient.Models.Config
        {
            AccessKeyId = _aliYunSettings.AccessKeyId,
            AccessKeySecret = _aliYunSettings.AccessKeySecret,
        };

        config.Endpoint = endpoint;
        return new AlibabaCloud.SDK.Alidns20150109.Client(config);
    }

    public async Task<UpdateDomainRecordResponseBody> UpdateDomainRecordAsync(
        string describeDomain, string endpoint, string hostRecords, string value, CancellationToken cancellationToken)
    {
        var client = CreateClient(endpoint);

        var describeDomainRecordsRequest = new DescribeDomainRecordsRequest { DomainName = describeDomain };

        var runtime = new AlibabaCloud.TeaUtil.Models.RuntimeOptions();

        var resp = await client.DescribeDomainRecordsWithOptionsAsync(describeDomainRecordsRequest, runtime).ConfigureAwait(false);
        
        var describeDomainRecord = resp.Body.DomainRecords.Record.Where(x => x.RR == hostRecords).FirstOrDefault();

        if (describeDomainRecord == null) { throw new Exception("Cannot find the describeDomain"); }
        
        Log.Information("UpdateDomainRecordAsync describeDomainRecord {@describeDomainRecord}", describeDomainRecord);
        
        var request = new UpdateDomainRecordRequest()
        {
            RecordId = describeDomainRecord.RecordId,
            RR = describeDomainRecord.RR,
            Type = describeDomainRecord.Type,
            Value = value
        };

        var updateDomainRecordWithOptions = await client.UpdateDomainRecordWithOptionsAsync(request, runtime).ConfigureAwait(false);

        return updateDomainRecordWithOptions.Body;
    }
}