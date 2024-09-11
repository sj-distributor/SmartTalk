namespace SmartTalk.Messages.Extensions;

public static class StringExtension
{
    public static string ToCamelCase(this string str)
    {
        return char.ToLowerInvariant(str[0]) + str[1..];
    }

    public static string ToUpperFirstCase(this string str)
    {
        return char.ToUpperInvariant(str[0]) + str[1..];
    }
}