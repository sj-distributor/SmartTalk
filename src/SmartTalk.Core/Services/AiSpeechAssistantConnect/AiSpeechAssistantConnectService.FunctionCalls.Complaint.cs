using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.RealtimeAi;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private RealtimeAiFunctionCallResult ProcessCollectComplaintInfo(RealtimeAiWssFunctionCallData functionCallData)
    {
        AiSpeechAssistantComplaintInfoDto incoming = null;

        try
        {
            incoming = JsonConvert.DeserializeObject<AiSpeechAssistantComplaintInfoDto>(functionCallData.ArgumentsJson);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[AiAssistant] Deserialize complaint info failed. Arguments: {Arguments}", functionCallData.ArgumentsJson);
        }

        _ctx.ComplaintInfo = AiSpeechAssistantComplaintInfoHelper.Merge(_ctx.ComplaintInfo, incoming);

        return new RealtimeAiFunctionCallResult
        {
            Output = AiSpeechAssistantComplaintInfoHelper.BuildFunctionOutput(_ctx.ComplaintInfo)
        };
    }
}
