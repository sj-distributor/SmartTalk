using AutoMapper;
using FluentValidation;
using Shouldly;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Mappings;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Messages.Commands.Agent;
using SmartTalk.Messages.Dto.Agent;
using Xunit;

namespace SmartTalk.UnitTests.Services.Agents;

public class AgentTransferCallConfigValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ConfigsProvided_RejectsMissingServiceHours(string serviceHours)
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
            AgentService.ValidateAgentTransferCallConfigs(configs));

        exception.Message.ShouldContain("ServiceHours is required");
    }

    [Fact]
    public void ConfigsProvided_AcceptsValidConfig()
    {
        var configs = new List<AgentTransferCallConfigDto>
        {
            new()
            {
                TransferCallNumber = "+12065550100",
                ServiceHours = """[{"day":1,"hours":[{"start":"09:00","end":"17:00"}]}]"""
            }
        };

        Should.NotThrow(() => AgentService.ValidateAgentTransferCallConfigs(configs));
        AgentService.HasTransferCallConfigs(configs).ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfigsNotProvided_AreValidAndDisableTransfer(bool useEmptyConfigs)
    {
        List<AgentTransferCallConfigDto> configs = useEmptyConfigs ? [] : null!;

        Should.NotThrow(() => AgentService.ValidateAgentTransferCallConfigs(configs));
        AgentService.HasTransferCallConfigs(configs).ShouldBeFalse();
    }

    [Fact]
    public void ConfigsProvided_RejectsMissingTransferCallNumber()
    {
        var configs = new List<AgentTransferCallConfigDto>
        {
            new()
            {
                TransferCallNumber = " ",
                ServiceHours = "[]"
            }
        };

        var exception = Should.Throw<ValidationException>(() =>
            AgentService.ValidateAgentTransferCallConfigs(configs));

        exception.Message.ShouldContain("transfer call number");
    }

    [Fact]
    public void UpdateMapping_DoesNotOverwriteBackendManagedTransferFields()
    {
        var mapper = new MapperConfiguration(config => config.AddProfile<AgentMapping>()).CreateMapper();
        var agent = new Agent
        {
            IsTransferHuman = true,
            TransferCallNumber = "+12065550100"
        };

        mapper.Map(new UpdateAgentCommand
        {
            IsTransferHuman = false,
            TransferCallNumber = "+12065550200"
        }, agent);

        agent.IsTransferHuman.ShouldBeTrue();
        agent.TransferCallNumber.ShouldBe("+12065550100");
    }
}
