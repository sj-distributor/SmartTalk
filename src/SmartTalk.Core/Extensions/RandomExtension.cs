namespace SmartTalk.Core.Extensions;

public static class RandomExtension
{
    private static readonly Random random = new Random();

    public static T GetRandomElement<T>(this List<T> list)
    {
        if (list == null || list.Count == 0)
        {
            throw new InvalidOperationException("Cannot select a random element from an empty or null list.");
        }
        var index = random.Next(list.Count);
        
        return list[index];
    }
}