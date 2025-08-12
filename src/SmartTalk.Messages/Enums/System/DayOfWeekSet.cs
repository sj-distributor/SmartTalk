namespace SmartTalk.Messages.Enums.System;

[Flags]
public enum DayOfWeekSet
{
    None = 0,
    MON = 1 << 0,
    TUE = 1 << 1,
    WED = 1 << 2,
    THU = 1 << 3,
    FRI = 1 << 4,
    SAT = 1 << 5,
    SUN = 1 << 6
}