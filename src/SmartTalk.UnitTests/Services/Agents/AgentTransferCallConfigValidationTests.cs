using AutoMapper;
using FluentValidation;
using Shouldly;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Mappings;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Messages.Commands.Agent;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Enums.Agent;
using Xunit;

namespace SmartTalk.UnitTests.Services.Agents;

public class AgentTransferCallConfigValidationTests
{
    [Theory]
    [InlineData(false, null)]
    [InlineData(false, "")]
    [InlineData(false, " ")]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(true, " ")]
    public void ConfigsProvided_RejectsMissingServiceHours(bool isTransferHuman, string serviceHours)
    {
        var configs = new List<AgentTransferCallConfigDto>
        {
            new()
            {
                TransferCallNumber = "+12065550100",
                ServiceHours = serviceHours,
                Priority = AgentTransferCallPriority.Default
            }
        };

        var exception = Should.Throw<ValidationException>(() =>
            AgentService.ValidateAgentTransferCallConfigs(isTransferHuman, configs));

        exception.Message.ShouldContain("ServiceHours is required");
    }

    [Fact]
    public void TransferEnabled_AcceptsValidDefaultConfig()
    {
        var configs = new List<AgentTransferCallConfigDto>
        {
            new()
            {
                TransferCallNumber = "+12065550100",
                ServiceHours = """[{"day":1,"hours":[{"start":"09:00","end":"17:00"}]}]""",
                Priority = AgentTransferCallPriority.Default
            }
        };

        Should.NotThrow(() => AgentService.ValidateAgentTransferCallConfigs(true, configs));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TransferEnabled_RejectsMissingConfigs(bool useEmptyConfigs)
    {
        List<AgentTransferCallConfigDto> configs = useEmptyConfigs ? [] : null!;

        var exception = Should.Throw<ValidationException>(() =>
            AgentService.ValidateAgentTransferCallConfigs(true, configs));

        exception.Message.ShouldContain("required when IsTransferHuman is true");
    }

    [Fact]
    public void TransferEnabled_RejectsConfigsWithoutDefault()
    {
        var configs = new List<AgentTransferCallConfigDto>
        {
            CreateValidConfig(AgentTransferCallPriority.Normal)
        };

        var exception = Should.Throw<ValidationException>(() =>
            AgentService.ValidateAgentTransferCallConfigs(true, configs));

        exception.Message.ShouldContain("default config");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TransferDisabled_AcceptsMissingConfigs(bool useEmptyConfigs)
    {
        List<AgentTransferCallConfigDto> configs = useEmptyConfigs ? [] : null!;

        Should.NotThrow(() => AgentService.ValidateAgentTransferCallConfigs(false, configs));
    }

    [Fact]
    public void TransferDisabled_AcceptsConfigsWithoutDefault()
    {
        var configs = new List<AgentTransferCallConfigDto>
        {
            CreateValidConfig(AgentTransferCallPriority.Normal)
        };

        Should.NotThrow(() => AgentService.ValidateAgentTransferCallConfigs(false, configs));
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
            AgentService.ValidateAgentTransferCallConfigs(false, configs));

        exception.Message.ShouldContain("TransferCallNumber is required");
    }

    [Fact]
    public void UpdateMapping_UsesRequestedIsTransferHumanAndPreservesLegacyNumber()
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

        agent.IsTransferHuman.ShouldBeFalse();
        agent.TransferCallNumber.ShouldBe("+12065550100");
    }

    private static AgentTransferCallConfigDto CreateValidConfig(AgentTransferCallPriority priority)
    {
        return new AgentTransferCallConfigDto
        {
            TransferCallNumber = "+12065550100",
            ServiceHours = """[{"day":1,"hours":[{"start":"09:00","end":"17:00"}]}]""",
            Priority = priority
        };
    }
}
