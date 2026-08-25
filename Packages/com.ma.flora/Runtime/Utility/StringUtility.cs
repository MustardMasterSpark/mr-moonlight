// Copyright © Magnetic Arcade. All Rights Reserved.

namespace MA.Flora
{
    internal static class StringUtility
    {
        public static string FormatBytes(long bytes)
        {
            const int kb = 1024;
            const int mb = kb * 1024;
            const int gb = mb * 1024;

            return bytes switch
            {
                >= gb => $"{bytes / (double)gb:F2} GB",
                >= mb => $"{bytes / (double)mb:F2} MB",
                >= kb => $"{bytes / (double)kb:F2} KB",
                _     => $"{bytes} B"
            };
        }

        public static string FormatLargeNumber(long num)
        {
            const int k = 1000;
            const int m = k * 1000;
            const int b = m * 1000;

            return num switch
            {
                >= b => $"{num / (double)b:F2} B",
                >= m => $"{num / (double)m:F2} M",
                >= k => $"{num / (double)k:F2} K",
                _    => $"{num}"
            };
        }
    }
}
