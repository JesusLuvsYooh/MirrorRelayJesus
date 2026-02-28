using System;
using System.Globalization;
using UnityEngine;

public static class RelayCountryDetector
{
    public static string DetectCountryCode()
    {
        // 1️⃣ Try system locale
        string localeCode = DetectFromLocale();
        if (!string.IsNullOrEmpty(localeCode))
            return localeCode;

        // 2️⃣ Fallback to timezone guess
        string timezoneCode = DetectFromTimezone();
        if (!string.IsNullOrEmpty(timezoneCode))
            return timezoneCode;

        // 3️⃣ Unknown fallback
        return "ZZ"; // unknown
    }

    // -------------------------
    // Method 1: Locale detection
    // -------------------------
    static string DetectFromLocale()
    {
        try
        {
            RegionInfo region = new RegionInfo(CultureInfo.CurrentCulture.Name);

            if (!string.IsNullOrEmpty(region.TwoLetterISORegionName))
                return region.TwoLetterISORegionName.ToUpper();
        }
        catch
        {
            // Some platforms may throw
        }

        return null;
    }

    // ----------------------------
    // Method 2: Timezone detection
    // ----------------------------
    static string DetectFromTimezone()
    {
        try
        {
            string tz = TimeZoneInfo.Local.Id.ToLower();

            // ----------------
            // United States
            // ----------------
            if (tz.Contains("pacific") ||
                tz.Contains("eastern") ||
                tz.Contains("central") ||
                tz.Contains("mountain"))
                return "US";

            // ----------------
            // United Kingdom
            // ----------------
            if (tz.Contains("gmt") ||
                tz.Contains("british"))
                return "GB";

            // ----------------
            // France / Western Europe
            // ----------------
            if (tz.Contains("romance"))
                return "FR";

            // ----------------
            // Central / Western Europe (fallback)
            // ----------------
            if (tz.Contains("w. europe") ||
                tz.Contains("central europe") ||
                tz.Contains("berlin"))
                return "DE";

            // ----------------
            // Russia
            // ----------------
            if (tz.Contains("moscow") ||
                tz.Contains("russia") ||
                tz.Contains("ekaterinburg") ||
                tz.Contains("novosibirsk") ||
                tz.Contains("vladivostok"))
                return "RU";

            // ----------------
            // Japan
            // ----------------
            if (tz.Contains("tokyo") ||
                tz.Contains("japan"))
                return "JP";

            // ----------------
            // China
            // ----------------
            if (tz.Contains("china") ||
                tz.Contains("beijing"))
                return "CN";

            // ----------------
            // Korea
            // ----------------
            if (tz.Contains("korea") ||
                tz.Contains("seoul"))
                return "KR";

            // ----------------
            // Southeast Asia
            // ----------------
            if (tz.Contains("singapore"))
                return "SG";

            if (tz.Contains("bangkok"))
                return "TH";

            if (tz.Contains("jakarta"))
                return "ID";

            // ----------------
            // India
            // ----------------
            if (tz.Contains("india") ||
                tz.Contains("kolkata"))
                return "IN";

            // ----------------
            // Australia / NZ
            // ----------------
            if (tz.Contains("australia") ||
                tz.Contains("sydney") ||
                tz.Contains("melbourne"))
                return "AU";

            if (tz.Contains("new zealand") ||
                tz.Contains("auckland"))
                return "NZ";

            // ----------------
            // South America
            // ----------------
            if (tz.Contains("brazil") ||
                tz.Contains("sao paulo"))
                return "BR";

            if (tz.Contains("argentina") ||
                tz.Contains("buenos aires"))
                return "AR";
        }
        catch
        {
        }

        return null;
    }
}