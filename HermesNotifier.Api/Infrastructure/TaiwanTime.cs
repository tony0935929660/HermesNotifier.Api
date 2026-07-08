namespace HermesNotifier.Api.Infrastructure;

public static class TaiwanTime
{
    private static readonly TimeZoneInfo TimeZone = ResolveTimeZone();

    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone);

    public static DateTime ToTaiwan(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => TimeZoneInfo.ConvertTimeFromUtc(value, TimeZone),
            DateTimeKind.Local => TimeZoneInfo.ConvertTime(value, TimeZone),
            _ => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeZone)
        };
    }

    public static DateTime? ToTaiwan(DateTime? value)
    {
        return value.HasValue ? ToTaiwan(value.Value) : null;
    }

    private static TimeZoneInfo ResolveTimeZone()
    {
        foreach (var id in new[] { "Taipei Standard Time", "Asia/Taipei" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        throw new InvalidOperationException("無法取得台灣時區設定。請確認系統已安裝 Taipei Standard Time 或 Asia/Taipei。");
    }
}