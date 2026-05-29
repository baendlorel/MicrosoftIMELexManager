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
}