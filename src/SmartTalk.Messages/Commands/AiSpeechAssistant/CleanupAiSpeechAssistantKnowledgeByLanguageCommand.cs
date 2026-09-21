using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.KnowledgeScenario;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class CleanupAiSpeechAssistantKnowledgeByLanguageCommand : ICommand
{
    public int CompanyId { get; set; }

    public List<AutoAddLanguage> Languages { get; set; } = new();
}
