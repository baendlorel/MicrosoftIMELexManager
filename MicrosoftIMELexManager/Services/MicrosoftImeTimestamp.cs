using System;

namespace MicrosoftIMELexManager.Services;

internal static class MicrosoftImeTimestamp
{
    private static readonly long Epoch2000UnixSeconds = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    public static uint GetCurrentSecondsSinceEpoch2000()
    {
        long seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - Epoch2000UnixSeconds;
        return checked((uint)seconds);
    }

    public static DateTimeOffset ToDateTimeOffset(uint timestamp)
    {
        long unixSeconds = Epoch2000UnixSeconds + timestamp;
        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
    }

    public static string FormatTimestamp(uint timestamp)
    {
        if (timestamp == 0)
        {
            return "-";
        }

        return ToDateTimeOffset(timestamp).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }
}