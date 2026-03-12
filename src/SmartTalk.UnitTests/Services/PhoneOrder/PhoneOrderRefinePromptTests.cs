using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shouldly;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Dto.Sales;
using Xunit;

namespace SmartTalk.UnitTests.Services.PhoneOrder;

public class PhoneOrderRefinePromptTests
{
    [Fact]
    public void BuildRefineOrderSystemPrompt_ShouldContainCriticalRules()
    {
        var prompt = InvokePrivateStatic<string>("BuildRefineOrderSystemPrompt");

        prompt.ShouldContain("仅输出本次通话涉及的物料", Case.Sensitive);
        prompt.ShouldContain("AiMaterialDesc", Case.Sensitive);
        prompt.ShouldContain("Name 必须以草稿单 AiMaterialDesc 原串为前缀", Case.Sensitive);
        prompt.ShouldContain("蒜头#1箱+2-2", Case.Sensitive);
        prompt.ShouldContain("严禁输出未在通话中提到的物料", Case.Sensitive);
    }

    [Fact]
    public void ApplyDeterministicRefine_TargetQty_ShouldAppendDeltaAndKeepQuantity()
    {
        var storeOrder = new ExtractedOrderDto();
        var originalOrders = new List<ExtractedOrderItemDto>
        {
            new()
            {
                Name = "蒜头",
                Quantity = 1,
                Unit = "箱",
                MaterialNumber = "70040046PC",
                IsTargetQuantity = true
            }
        };

        var draftItems = new List<AiOrderItemSimpleDto>
        {
            new()
            {
                AiMaterialDesc = "蒜头#1箱+2",
                MaterialQuantity = 3,
                MaterialNumber = "70040046PC",
                AiUnit = "箱"
            }
        };

        InvokePrivateStatic("ApplyDeterministicRefine", storeOrder, originalOrders, draftItems);

        storeOrder.Orders.Count.ShouldBe(1);
        storeOrder.Orders[0].Name.ShouldBe("蒜头#1箱+2-2");
        storeOrder.Orders[0].Quantity.ShouldBe(1);
        storeOrder.Orders[0].MaterialNumber.ShouldBe("70040046PC");
        storeOrder.Orders[0].Unit.ShouldBe("箱");
    }

    [Fact]
    public void ApplyDeterministicRefine_Additive_ShouldAppendDeltaAndKeepQuantity()
    {
        var storeOrder = new ExtractedOrderDto();
        var originalOrders = new List<ExtractedOrderItemDto>
        {
            new()
            {
                Name = "蒜头",
                Quantity = 2,
                Unit = "箱",
                MaterialNumber = "70040046PC",
                IsTargetQuantity = false
            }
        };

        var draftItems = new List<AiOrderItemSimpleDto>
        {
            new()
            {
                AiMaterialDesc = "蒜头#1箱",
                MaterialQuantity = 1,
                MaterialNumber = "70040046PC",
                AiUnit = "箱"
            }
        };

        InvokePrivateStatic("ApplyDeterministicRefine", storeOrder, originalOrders, draftItems);

        storeOrder.Orders.Count.ShouldBe(1);
        storeOrder.Orders[0].Name.ShouldBe("蒜头#1箱+2");
        storeOrder.Orders[0].Quantity.ShouldBe(2);
        storeOrder.Orders[0].MaterialNumber.ShouldBe("70040046PC");
        storeOrder.Orders[0].Unit.ShouldBe("箱");
    }

    private static T InvokePrivateStatic<T>(string methodName, params object[] args)
    {
        var method = FindPrivateStaticMethod(methodName, args);
        var result = method.Invoke(null, args);
        result.ShouldNotBeNull();
        return (T)result!;
    }

    private static void InvokePrivateStatic(string methodName, params object[] args)
    {
        var method = FindPrivateStaticMethod(methodName, args);
        method.Invoke(null, args);
    }

    private static MethodInfo FindPrivateStaticMethod(string methodName, object[] args)
    {
        var type = typeof(PhoneOrderProcessJobService);
        var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(m => string.Equals(m.Name, methodName, StringComparison.Ordinal))
            .ToList();

        methods.Count.ShouldBeGreaterThan(0, $"Method {methodName} should exist.");

        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != args.Length) continue;

            var match = true;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (args[i] == null) continue;
                if (!parameters[i].ParameterType.IsInstanceOfType(args[i]))
                {
                    match = false;
                    break;
                }
            }

            if (match) return method;
        }

        throw new MissingMethodException($"No matching method found for {methodName} with {args.Length} parameters.");
    }
}
