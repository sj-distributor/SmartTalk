using Serilog;

namespace SmartTalk.Core.Extensions;

public static class DateTimeExtension
{
    public static DateTime ConvertFromUtc(this DateTimeOffset dateTimeOffset, TimeZoneInfo destinationTimeZone)
    {
        return ConvertTimeFromUtc(dateTimeOffset.DateTime, destinationTimeZone);
    }

    public static DateTime ConvertFromUtc(this DateTime dateTime, TimeZoneInfo destinationTimeZone)
    {
        return ConvertTimeFromUtc(dateTime, destinationTimeZone); 
    }

    public static DateTime ConvertToUtc(this DateTimeOffset dateTimeOffset, TimeZoneInfo sourceTimeZone)
    {
        return ConvertTimeToUtc(dateTimeOffset.DateTime, sourceTimeZone);
    }
    
    public static DateTime ConvertToUtc(this DateTime dateTime, TimeZoneInfo sourceTimeZone)
    {
        return ConvertTimeToUtc(dateTime, sourceTimeZone);
    }
    
    private static DateTime ConvertTimeFromUtc(DateTime dateTime, TimeZoneInfo destinationTimeZone)
    {
        var i = 0;
        var hasError = false;
        
        while (i<=10)
        {
            try
            {
                var value = TimeZoneInfo.ConvertTimeFromUtc(dateTime.AddHours(i), destinationTimeZone);
                
                if (hasError)
                    Log.Information(
                        "ConvertTime error FromUtc {@Timezone} source {@Datetime} hasError return values is {@Value}",
                        destinationTimeZone, dateTime, value);

                return value;
            }
            catch
            {
                hasError = true;
            }
            i++;   
        }

        return dateTime;
    }

    private static DateTime ConvertTimeToUtc(DateTime dateTime, TimeZoneInfo sourceTimeZone)
    {
        var i = 0;
        var hasError = false;
        
        while (i<=10)
        {
            try
            {
                var value = TimeZoneInfo.ConvertTimeToUtc(dateTime.AddHours(i), sourceTimeZone);

                if (hasError)
                    Log.Information(
                        "ConvertTime error ToUtc {@Timezone} source {@Datetime} hasError return values is {@Value}",
                        sourceTimeZone, dateTime, value);

                return value;
            }
            catch
            {
                hasError = true;
            }
            i++;   
        }

        return dateTime;
    }
}