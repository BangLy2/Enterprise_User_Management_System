using System;

namespace MyWeb.Helpers
{
    public static class TimeZoneHelper
    {
        private static readonly TimeZoneInfo EasternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

        /// <summary>
        /// Converts UTC DateTime to Eastern Time
        /// </summary>
        public static DateTime ToEasternTime(this DateTime utcDateTime)
        {
            if (utcDateTime.Kind != DateTimeKind.Utc)
            {
                // If not UTC, assume it is and convert
                utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            }

            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, EasternTimeZone);
        }

        /// <summary>
        /// Formats DateTime in Eastern Time with standard format
        /// </summary>
        public static string ToEasternTimeString(this DateTime utcDateTime, string format = "MM/dd/yyyy hh:mm:ss tt")
        {
            return utcDateTime.ToEasternTime().ToString(format);
        }

        /// <summary>
        /// Gets the current time in Eastern Time
        /// </summary>
        public static DateTime GetEasternTimeNow()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EasternTimeZone);
        }

        /// <summary>
        /// Gets the timezone abbreviation (EST or EDT based on daylight saving)
        /// </summary>
        public static string GetEasternTimeZoneAbbreviation(this DateTime dateTime)
        {
            var etTime = dateTime.ToEasternTime();
            return EasternTimeZone.IsDaylightSavingTime(etTime) ? "EDT" : "EST";
        }
    }
}