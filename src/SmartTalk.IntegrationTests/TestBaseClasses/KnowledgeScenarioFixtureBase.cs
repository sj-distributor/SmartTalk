using Xunit;

namespace SmartTalk.IntegrationTests.TestBaseClasses;

[Collection("Knowledge Scenario")]
public class KnowledgeScenarioFixtureBase : TestBase
{
    protected KnowledgeScenarioFixtureBase() : base("_knowledge_scenario_", "smart_talk_knowledge_scenario", 5)
    {
    }
}