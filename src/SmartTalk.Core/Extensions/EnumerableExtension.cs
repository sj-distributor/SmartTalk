using System.Reflection;
using System.ComponentModel;

namespace SmartTalk.Core.Extensions;

public static class EnumerableExtension
{
    public static void ForEach<T>(this IEnumerable<T> items, Action<T> callback)
    {
        foreach (var item in items)
        {
            callback(item);
        }
    }
    
    public static string GetDescription(this Enum @enum)
    {
        var descriptionAttribute = @enum.GetType()
            .GetMember(@enum.ToString())
            .FirstOrDefault()
            ?.GetCustomAttribute<DescriptionAttribute>();

        return descriptionAttribute != null ? descriptionAttribute.Description : @enum.ToString();
    }
}