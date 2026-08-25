using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class SyncAiSpeechAssistantDescriptionCommand : ICommand
{
    public List<SyncAiSpeechAssistantDescriptionModel> List { get; set; } = [];
}

public class SyncAiSpeechAssistantDescriptionModel
{
    public string Id { get; set; }

    public string Value { get; set; }

    public string Description { get; set; }

    public AiSpeechAssistantModelDescriptionSyncType Type { get; set; }
}

public class SyncAiSpeechAssistantDescriptionsResponse : SmartTalkResponse<SyncAiSpeechAssistantDescriptionsResponseData>
{
}

public class SyncAiSpeechAssistantDescriptionsResponseData
{
    public bool Result { get; set; }
}
