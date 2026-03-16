using System;
using System.Linq;
using System.Reflection;
using Shouldly;
using SmartTalk.Core.Services.PhoneOrder;
using Xunit;

namespace SmartTalk.UnitTests.Services.PhoneOrder;

public class PhoneOrderRefinePromptTests
{
    [Fact]
    public void BuildRefineOrderSystemPrompt_ShouldContainCriticalRules()
    {
        var prompt = InvokePrivateStatic<string>("BuildRefineOrderSystemPrompt");

        prompt.ShouldContain("【总规则】", Case.Sensitive);
        prompt.ShouldContain("仅输出本次通话涉及的物料", Case.Sensitive);
        prompt.ShouldContain("【关键规则一：匹配逻辑（核心）】", Case.Sensitive);
        prompt.ShouldContain("material_number", Case.Sensitive);
        prompt.ShouldContain("草稿基准名", Case.Sensitive);
        prompt.ShouldContain("【关键规则三：整单与删除】", Case.Sensitive);
        prompt.ShouldContain("严禁输出未在通话中提到的物料", Case.Sensitive);
    }

    [Fact]
    public void BuildRefineOrderSystemPrompt_ShouldContainExamplesAndOutputSchema()
    {
        var prompt = InvokePrivateStatic<string>("BuildRefineOrderSystemPrompt");

        prompt.ShouldContain("玉米#1箱+1", Case.Sensitive);
        prompt.ShouldContain("qty = 草稿单 qty + 本次变动 qty", Case.Sensitive);
        prompt.ShouldContain("【输出格式】", Case.Sensitive);
        prompt.ShouldContain("\"Orders\"", Case.Sensitive);
        prompt.ShouldContain("严禁改写草稿单 AiMaterialDesc 原串", Case.Sensitive);
    }

    private static T InvokePrivateStatic<T>(string methodName, params object[] args)
    {
        var method = FindPrivateStaticMethod(methodName, args);
        var result = method.Invoke(null, args);
        result.ShouldNotBeNull();
        return (T)result!;
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
