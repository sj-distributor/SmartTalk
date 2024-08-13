namespace SmartTalk.Core.Services.Infrastructure
{
    public class Clock : IClock
    {
        public DateTimeOffset Now => DateTimeOffset.Now;
        
        public DateTime DateTimeNow => DateTime.Now;
    }
}