namespace FairwayFinder.Features.Enums;

public static class MissType
{
    public enum MissTypeEnum
    {
        MissLeft = 1, 
        MissRight = 2,
        MissShort = 3, 
        MissLong = 4,
        MissOther = 999
    }
    
    public static bool Is(this long value, MissTypeEnum missType) => value == (long)missType;
    
    public static bool Is(this long? value, MissTypeEnum missType) => value.HasValue && value.Value == (long)missType;
}
