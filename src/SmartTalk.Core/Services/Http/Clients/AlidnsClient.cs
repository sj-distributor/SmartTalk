using AlibabaCloud.SDK.Alidns20150109.Models;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.AliYun;

namespace SmartTalk.Core.Services.Http.Clients;

public interface IAlidnsClient : IScopedDependency
{
    Task<UpdateDomainRecordResponseBody> UpdateDomainRecordAsync(string endpoint, string hostRecords, string value);
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

    public async Task<UpdateDomainRecordResponseBody> UpdateDomainRecordAsync(string endpoint, string hostRecords, string value)
    {
        var client = CreateClient(endpoint);

        var describeDomainRecordsRequest = new DescribeDomainRecordsRequest { DomainName = AliyunConstants.DescribeDomain };

        var runtime = new AlibabaCloud.TeaUtil.Models.RuntimeOptions();

        var resp = await client.DescribeDomainRecordsWithOptionsAsync(describeDomainRecordsRequest, runtime).ConfigureAwait(false);
        
        var describeDomain = resp.Body.DomainRecords.Record.Where(x => x.RR == hostRecords).FirstOrDefault();

        var request = new UpdateDomainRecordRequest();
        
        if (describeDomain != null)
        {
            request.RecordId = describeDomain.RecordId;
            request.RR = describeDomain.RR;
            request.Type = describeDomain.Type;
            request.Value = value;
        }

        var updateDomainRecordWithOptions = await client.UpdateDomainRecordWithOptionsAsync(request, runtime).ConfigureAwait(false);

        return updateDomainRecordWithOptions.Body;
    }
}