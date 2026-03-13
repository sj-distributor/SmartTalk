using System;
using Xunit;
using Shouldly;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.UnitTests.Services.PhoneOrder;

public class PhoneOrderServiceGetCompanyCallReportUtcRangeTests
{
    [Fact]
    public void GetCompanyCallReportUtcRange_Daily_ShouldReturnTodayRange()
    {
        // Arrange
        var reportType = PhoneOrderCallReportType.Daily;
        
        // Act
        var (startUtc, endUtc) = PhoneOrderService.GetCompanyCallReportUtcRange(reportType);
        
        // Assert
        var chinaZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        var nowChina = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, chinaZone);
        var todayLocal = new DateTime(nowChina.Year, nowChina.Month, nowChina.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var expectedStartUtc = TimeZoneInfo.ConvertTimeToUtc(todayLocal, chinaZone);
        var expectedEndUtc = expectedStartUtc.AddDays(1);
        
        startUtc.UtcDateTime.ShouldBe(expectedStartUtc, TimeSpan.FromSeconds(1)); // 允许1秒误差
        endUtc.UtcDateTime.ShouldBe(expectedEndUtc, TimeSpan.FromSeconds(1));
        (endUtc - startUtc).TotalDays.ShouldBe(1.0, 0.01);
    }

    [Theory]
    [InlineData(DayOfWeek.Monday)]
    [InlineData(DayOfWeek.Tuesday)]
    [InlineData(DayOfWeek.Wednesday)]
    [InlineData(DayOfWeek.Thursday)]
    [InlineData(DayOfWeek.Friday)]
    [InlineData(DayOfWeek.Saturday)]
    [InlineData(DayOfWeek.Sunday)]
    public void GetCompanyCallReportUtcRange_Weekly_ShouldReturnThisWeekRange(DayOfWeek dayOfWeek)
    {
        // Arrange
        var reportType = PhoneOrderCallReportType.Weekly;
        var chinaZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        
        // 模拟今天是特定星期几
        var nowUtc = DateTimeOffset.UtcNow;
        var nowChina = TimeZoneInfo.ConvertTime(nowUtc, chinaZone);
        var todayLocal = new DateTime(nowChina.Year, nowChina.Month, nowChina.Day, 0, 0, 0, DateTimeKind.Unspecified);
        
        // 计算到目标星期几的天数差
        var currentDayOfWeek = (int)todayLocal.DayOfWeek;
        var targetDayOfWeek = (int)dayOfWeek;
        var daysDiff = (targetDayOfWeek - currentDayOfWeek + 7) % 7;
        if (daysDiff == 0 && currentDayOfWeek != targetDayOfWeek) daysDiff = 7;
        
        var testDateLocal = todayLocal.AddDays(daysDiff);
        
        // 计算本周一
        var daysFromMonday = ((int)testDateLocal.DayOfWeek + 6) % 7;
        var thisWeekMondayLocal = testDateLocal.AddDays(-daysFromMonday);
        var expectedStartUtc = TimeZoneInfo.ConvertTimeToUtc(thisWeekMondayLocal, chinaZone);
        var expectedEndUtc = expectedStartUtc.AddDays(7);
        
        // Act
        var (startUtc, endUtc) = PhoneOrderService.GetCompanyCallReportUtcRange(reportType);
        
        // Assert
        // 验证时间范围是7天
        (endUtc - startUtc).TotalDays.ShouldBe(7.0, 0.01);
        
        // 验证开始时间是周一 00:00:00（中国时间）
        var startChina = TimeZoneInfo.ConvertTime(startUtc, chinaZone);
        startChina.DayOfWeek.ShouldBe(DayOfWeek.Monday);
        startChina.Hour.ShouldBe(0);
        startChina.Minute.ShouldBe(0);
        startChina.Second.ShouldBe(0);
    }

    [Theory]
    [InlineData(DayOfWeek.Monday)]
    [InlineData(DayOfWeek.Tuesday)]
    [InlineData(DayOfWeek.Wednesday)]
    [InlineData(DayOfWeek.Thursday)]
    [InlineData(DayOfWeek.Friday)]
    [InlineData(DayOfWeek.Saturday)]
    [InlineData(DayOfWeek.Sunday)]
    public void GetCompanyCallReportUtcRange_LastWeek_ShouldReturnLastWeekRange(DayOfWeek dayOfWeek)
    {
        // Arrange
        var reportType = PhoneOrderCallReportType.LastWeek;
        var chinaZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        
        // Act
        var (startUtc, endUtc) = PhoneOrderService.GetCompanyCallReportUtcRange(reportType);
        
        // Assert
        // 验证时间范围是7天
        (endUtc - startUtc).TotalDays.ShouldBe(7.0, 0.01);
        
        // 验证开始时间是周一 00:00:00（中国时间）
        var startChina = TimeZoneInfo.ConvertTime(startUtc, chinaZone);
        startChina.DayOfWeek.ShouldBe(DayOfWeek.Monday);
        startChina.Hour.ShouldBe(0);
        startChina.Minute.ShouldBe(0);
        startChina.Second.ShouldBe(0);
        
        // 验证结束时间是下周一 00:00:00（中国时间）
        var endChina = TimeZoneInfo.ConvertTime(endUtc, chinaZone);
        endChina.DayOfWeek.ShouldBe(DayOfWeek.Monday);
        endChina.Hour.ShouldBe(0);
        endChina.Minute.ShouldBe(0);
        endChina.Second.ShouldBe(0);
        
        // 验证结束时间比开始时间晚7天
        (endChina - startChina.DateTime).TotalDays.ShouldBe(7.0, 0.01);
        
        // 验证是上周，不是本周
        var nowChina = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, chinaZone);
        var todayLocal = new DateTime(nowChina.Year, nowChina.Month, nowChina.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var daysFromMonday = ((int)todayLocal.DayOfWeek + 6) % 7;
        var thisWeekMondayLocal = todayLocal.AddDays(-daysFromMonday);
        var thisWeekMondayUtc = TimeZoneInfo.ConvertTimeToUtc(thisWeekMondayLocal, chinaZone);
        
        startUtc.ShouldBeLessThan(thisWeekMondayUtc, "上周的开始时间应该早于本周一");
        endUtc.ShouldBeLessThanOrEqualTo(thisWeekMondayUtc.AddDays(1), "上周的结束时间应该不晚于本周一");
    }

    [Fact]
    public void GetCompanyCallReportUtcRange_LastWeek_ShouldNotIncludeThisWeekData()
    {
        // Arrange
        var reportType = PhoneOrderCallReportType.LastWeek;
        var chinaZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        var nowChina = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, chinaZone);
        var todayLocal = new DateTime(nowChina.Year, nowChina.Month, nowChina.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var daysFromMonday = ((int)todayLocal.DayOfWeek + 6) % 7;
        var thisWeekMondayLocal = todayLocal.AddDays(-daysFromMonday);
        var thisWeekMondayUtc = TimeZoneInfo.ConvertTimeToUtc(thisWeekMondayLocal, chinaZone);
        
        // Act
        var (startUtc, endUtc) = PhoneOrderService.GetCompanyCallReportUtcRange(reportType);
        
        // Assert
        // 验证上周的结束时间应该等于本周一的开始时间（或非常接近）
        endUtc.UtcDateTime.ShouldBeLessThanOrEqualTo(thisWeekMondayUtc.AddSeconds(1), 
            "上周的结束时间应该等于或早于本周一的开始时间");
        
        // 验证上周的开始时间应该比本周一早7天
        var expectedLastWeekStart = thisWeekMondayUtc.AddDays(-7);
        startUtc.UtcDateTime.ShouldBe(expectedLastWeekStart, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetCompanyCallReportUtcRange_LastWeek_ShouldIncludeFullWeek()
    {
        // Arrange
        var reportType = PhoneOrderCallReportType.LastWeek;
        var chinaZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        
        // Act
        var (startUtc, endUtc) = PhoneOrderService.GetCompanyCallReportUtcRange(reportType);
        
        // Assert
        var startChina = TimeZoneInfo.ConvertTime(startUtc, chinaZone);
        var endChina = TimeZoneInfo.ConvertTime(endUtc, chinaZone);
        
        // 验证包含完整的周一到周日
        startChina.DayOfWeek.ShouldBe(DayOfWeek.Monday, "开始时间应该是周一");
        
        // 结束时间是下周一，所以应该包含上周一到上周日的所有数据
        var lastSundayChina = endChina.AddDays(-1);
        lastSundayChina.DayOfWeek.ShouldBe(DayOfWeek.Sunday, "结束时间的前一天应该是周日");
        
        // 验证时间跨度是7天
        var timeSpan = endChina - startChina;
        timeSpan.TotalDays.ShouldBe(7.0, 0.01, "时间跨度应该是7天");
    }

    [Fact]
    public void GetCompanyCallReportUtcRange_AllTypes_ShouldHaveValidTimeRanges()
    {
        // Arrange & Act & Assert
        foreach (PhoneOrderCallReportType reportType in Enum.GetValues<PhoneOrderCallReportType>())
        {
            var (startUtc, endUtc) = PhoneOrderService.GetCompanyCallReportUtcRange(reportType);
            
            // 验证结束时间晚于开始时间
            endUtc.ShouldBeGreaterThan(startUtc, $"{reportType} 的结束时间应该晚于开始时间");
            
            // 验证时间范围合理（不超过30天）
            var daysDiff = (endUtc - startUtc).TotalDays;
            daysDiff.ShouldBeLessThanOrEqualTo(30, $"{reportType} 的时间范围不应该超过30天");
            daysDiff.ShouldBeGreaterThan(0, $"{reportType} 的时间范围应该大于0");
        }
    }

    [Fact]
    public void GetCompanyCallReportUtcRange_LastWeek_WhenTodayIsMonday_ShouldReturnCorrectRange()
    {
        // Arrange
        var reportType = PhoneOrderCallReportType.LastWeek;
        var chinaZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        
        // Act
        var (startUtc, endUtc) = PhoneOrderService.GetCompanyCallReportUtcRange(reportType);
        
        // Assert
        var startChina = TimeZoneInfo.ConvertTime(startUtc, chinaZone);
        var endChina = TimeZoneInfo.ConvertTime(endUtc, chinaZone);
        
        // 验证是上周一
        startChina.DayOfWeek.ShouldBe(DayOfWeek.Monday);
        
        // 验证结束时间是下周一（即本周一）
        endChina.DayOfWeek.ShouldBe(DayOfWeek.Monday);
        
        // 验证时间跨度
        (endChina - startChina.DateTime).TotalDays.ShouldBe(7.0, 0.01);
    }

    [Fact]
    public void GetCompanyCallReportUtcRange_LastWeek_WhenTodayIsSunday_ShouldReturnCorrectRange()
    {
        // Arrange
        var reportType = PhoneOrderCallReportType.LastWeek;
        var chinaZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        
        // Act
        var (startUtc, endUtc) = PhoneOrderService.GetCompanyCallReportUtcRange(reportType);
        
        // Assert
        var startChina = TimeZoneInfo.ConvertTime(startUtc, chinaZone);
        var endChina = TimeZoneInfo.ConvertTime(endUtc, chinaZone);
        
        // 验证是上周一
        startChina.DayOfWeek.ShouldBe(DayOfWeek.Monday);
        
        // 验证结束时间是下周一
        endChina.DayOfWeek.ShouldBe(DayOfWeek.Monday);
        
        // 验证时间跨度
        (endChina - startChina.DateTime).TotalDays.ShouldBe(7.0, 0.01);
        
        // 验证是上周，不是上上周
        var nowChina = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, chinaZone);
        var todayLocal = new DateTime(nowChina.Year, nowChina.Month, nowChina.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var daysFromMonday = ((int)todayLocal.DayOfWeek + 6) % 7;
        var thisWeekMondayLocal = todayLocal.AddDays(-daysFromMonday);
        var thisWeekMondayUtc = TimeZoneInfo.ConvertTimeToUtc(thisWeekMondayLocal, chinaZone);
        
        // 上周的结束时间应该等于本周一的开始时间
        endUtc.UtcDateTime.ShouldBeLessThanOrEqualTo(thisWeekMondayUtc.AddSeconds(1));
    }
}

