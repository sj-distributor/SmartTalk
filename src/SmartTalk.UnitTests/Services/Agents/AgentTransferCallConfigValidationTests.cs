using FluentValidation;
using Shouldly;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Messages.Dto.Agent;
using Xunit;

namespace SmartTalk.UnitTests.Services.Agents;

public class AgentTransferCallConfigValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void TransferEnabled_RejectsMissingServiceHours(string serviceHours)
    {
        var configs = new List<AgentTransferCallConfigDto>
        {
            new()
            {
                TransferCallNumber = "+12065550100",
                ServiceHours = serviceHours
            }
        };

        var exception = Should.Throw<ValidationException>(() =>
            AgentService.ValidateAgentTransferCallConfigs(true, "+12065550100", configs));

        exception.Message.ShouldContain("ServiceHours is required");
    }

    [Fact]
    public void TransferEnabled_AcceptsServiceHours()
    {
        var configs = new List<AgentTransferCallConfigDto>
        {
            new()
            {
                TransferCallNumber = "+12065550100",
                ServiceHours = """[{"day":1,"hours":[{"start":"09:00","end":"17:00"}]}]"""
            }
        };

        Should.NotThrow(() => AgentService.ValidateAgentTransferCallConfigs(true, null, configs));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TransferEnabled_AcceptsLegacyNumberWhenConfigsAreNotProvided(bool useEmptyConfigs)
    {
        List<AgentTransferCallConfigDto> configs = useEmptyConfigs ? [] : null!;

        Should.NotThrow(() => AgentService.ValidateAgentTransferCallConfigs(
            true, "+12065550100", configs));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TransferEnabled_RejectsRequestWhenNeitherLegacyNumberNorConfigsAreProvided(bool useEmptyConfigs)
    {
        List<AgentTransferCallConfigDto> configs = useEmptyConfigs ? [] : null!;

        Should.Throw<ValidationException>(() => AgentService.ValidateAgentTransferCallConfigs(
            true, null, configs));
    }

    [Fact]
    public void TransferDisabled_DoesNotRequireLegacyNumberOrConfigs()
    {
        Should.NotThrow(() => AgentService.ValidateAgentTransferCallConfigs(false, null, null));
    }
}
