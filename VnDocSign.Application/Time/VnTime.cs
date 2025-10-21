using System;

namespace VnDocSign.Application.Time;

public static class VnTime
{
    private static readonly string WindowsTzId = "SE Asia Standard Time"; // Windows
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.FindSystemTimeZoneById(WindowsTzId);

    public static DateTime ToVn(DateTime utc)
    {
        if (utc.Kind != DateTimeKind.Utc) utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, Tz);
    }

    public static DateTime FromVnToUtc(DateTime vnLocal)
    {
        if (vnLocal.Kind == DateTimeKind.Utc) return vnLocal;
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(vnLocal, DateTimeKind.Unspecified), Tz);
    }
}
