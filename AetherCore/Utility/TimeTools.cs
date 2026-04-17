using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AetherCore.Utility
{
    public static class TimeTools
    {
        static private TimeZoneInfo? _twTz = null;

        public static TimeZoneInfo GetTaiwanTimeZone()
        {
            // Windows: "Taipei Standard Time"
            // Linux:   "Asia/Taipei"
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time"); }
        }

        public static DateTime GetTwDateTime()
        {
            if (_twTz == null || string.IsNullOrWhiteSpace(_twTz.Id))
            {
                _twTz = GetTaiwanTimeZone();
            }

            var twNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _twTz);
            return twNow;
        }
    }
}
