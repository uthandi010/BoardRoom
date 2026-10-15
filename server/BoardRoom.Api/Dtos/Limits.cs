namespace BoardRoom.Api.Dtos;

/// <summary>Maximum accepted lengths for user-supplied text.</summary>
public static class Limits
{
    public const int Email = 254;
    public const int PersonName = 100;
    public const int Password = 128;
    public const int EntityName = 100;
    public const int CardTitle = 200;
    public const int CardDescription = 2000;
}
