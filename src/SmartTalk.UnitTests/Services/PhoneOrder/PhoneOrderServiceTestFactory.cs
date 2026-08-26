using NSubstitute;
using SmartTalk.Core.Services.PhoneOrder;

namespace SmartTalk.UnitTests.Services.PhoneOrder;

internal static class PhoneOrderServiceTestFactory
{
    public static PhoneOrderService Create(params object[] overrides)
    {
        var constructor = typeof(PhoneOrderService).GetConstructors().Single();
        var arguments = constructor
            .GetParameters()
            .Select(parameter => Resolve(parameter.ParameterType, overrides))
            .ToArray();

        return (PhoneOrderService)constructor.Invoke(arguments);
    }

    private static object? Resolve(Type parameterType, object[] overrides)
    {
        var configuredDependency = overrides.LastOrDefault(parameterType.IsInstanceOfType);
        if (configuredDependency != null) return configuredDependency;

        if (parameterType.IsInterface || parameterType.IsAbstract)
            return Substitute.For([parameterType], Array.Empty<object>());

        if (parameterType.GetConstructor(Type.EmptyTypes) != null)
            return Activator.CreateInstance(parameterType);

        return null;
    }
}
