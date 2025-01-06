using AlibabaCloud.SDK.Alidns20150109.Models;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.AliYun;

namespace SmartTalk.Core.Services.Http.Clients;

public interface IAlidnsClient : IScopedDependency
{
    public UpdateDomainRecordResponseBody UpdateDomainRecordAsync(UpdateDomainRecordRequest request);
}

public class AlidnsClient : IAlidnsClient
{
    private readonly AliYunSettings _aliYunSettings;

    public AlidnsClient(AliYunSettings aliYunSettings)
    {
        _aliYunSettings = aliYunSettings;
    }

    private AlibabaCloud.SDK.Alidns20150109.Client CreateClient()
    {
        var config = new AlibabaCloud.OpenApiClient.Models.Config
        {
            AccessKeyId = _aliYunSettings.AccessKeyId,
            AccessKeySecret = _aliYunSettings.AccessKeySecret,
        };

        config.Endpoint = "alidns.us-east-1.aliyuncs.com";
        return new AlibabaCloud.SDK.Alidns20150109.Client(config);
    }

    public UpdateDomainRecordResponseBody UpdateDomainRecordAsync(UpdateDomainRecordRequest request)
    {
        var client = CreateClient();

        var describeDomainRecordsRequest = new DescribeDomainRecordsRequest() { DomainName = "sip-us.sjfood.us" };

        var runtime = new AlibabaCloud.TeaUtil.Models.RuntimeOptions();

        var resp = client.DescribeDomainRecordsWithOptions(describeDomainRecordsRequest, runtime);

        request.RecordId = resp.Body.DomainRecords.Record[0].RecordId;
        
        var updateDomainRecordWithOptions = client.UpdateDomainRecordWithOptions(request, runtime);

        return updateDomainRecordWithOptions.Body;
    }
}