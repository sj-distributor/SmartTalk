namespace SmartTalk.Core.Utils;

public static class CustomerOrderUnitClassifier
{
    public const string Case = "CS";
    public const string Piece = "PC";
    public const string Pound = "LB";

    public static string Classify(string unit)
    {
        if (IsCase(unit)) return Case;
        if (IsPound(unit)) return Pound;
        return Piece;
    }

    public static string GetPreferredMaterialUnit(string unit)
    {
        var unitType = Classify(unit);
        return unitType == Pound ? string.Empty : unitType;
    }

    public static bool IsCase(string unit)
    {
        var normalized = Normalize(unit);
        return normalized.Contains('箱') ||
               normalized is "CS" or "CASE" or "CASES";
    }

    public static bool IsPound(string unit)
    {
        var normalized = Normalize(unit);
        return normalized.Contains('磅') ||
               normalized is "LB" or "LBS" or "POUND" or "POUNDS";
    }

    private static string Normalize(string unit)
    {
        return (unit ?? string.Empty).Trim().ToUpperInvariant();
    }
}
