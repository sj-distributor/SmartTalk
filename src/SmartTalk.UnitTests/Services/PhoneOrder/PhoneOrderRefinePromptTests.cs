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
    public void BuildRefineOrderSystemPrompt_ShouldContainExamplesAndOutputSchema()
    {
        var prompt = InvokePrivateStatic<string>("BuildRefineOrderSystemPrompt");

        prompt.ShouldContain("【输出格式】", Case.Sensitive);
        prompt.ShouldContain("\"Orders\"", Case.Sensitive);
        prompt.ShouldContain("\"AiMaterialDesc\"", Case.Sensitive);
        prompt.ShouldContain("Quantity 已在 C# 端计算", Case.Sensitive);
        prompt.ShouldContain("#,+,- 前后不得有空格", Case.Sensitive);
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
