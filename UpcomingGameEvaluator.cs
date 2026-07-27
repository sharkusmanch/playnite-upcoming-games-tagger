using Playnite.SDK.Models;
using System;

namespace UpcomingGamesTagger
{
    /// <summary>
    /// Decides whether a game counts as "upcoming". Deliberately free of IPlayniteAPI
    /// so the predicate can be unit tested without a Playnite database.
    /// </summary>
    public static class UpcomingGameEvaluator
    {
        /// <summary>
        /// Earliest day the game could release. Playnite already collapses a partial
        /// ReleaseDate to the start of its period (2027 becomes 2027-01-01).
        /// </summary>
        public static DateTime PeriodStart(ReleaseDate releaseDate)
        {
            return releaseDate.Date.Date;
        }

        /// <summary>
        /// Latest day the game could release, derived from the precision actually stored.
        /// Month and Day are null when the metadata only specified a year, or a year and
        /// month, which is common for titles without a confirmed date.
        /// </summary>
        public static DateTime PeriodEnd(ReleaseDate releaseDate)
        {
            if (releaseDate.Month.HasValue && releaseDate.Day.HasValue)
            {
                return new DateTime(releaseDate.Year, releaseDate.Month.Value, releaseDate.Day.Value);
            }

            if (releaseDate.Month.HasValue)
            {
                var month = releaseDate.Month.Value;
                return new DateTime(releaseDate.Year, month, DateTime.DaysInMonth(releaseDate.Year, month));
            }

            return new DateTime(releaseDate.Year, 12, 31);
        }

        /// <summary>
        /// A game is upcoming while any part of its release period is still ahead of
        /// <paramref name="today"/> and that period begins within the configured window.
        /// </summary>
        /// <param name="daysAheadThreshold">Size of the look-ahead window in days; zero or less means no limit.</param>
        public static bool IsUpcoming(ReleaseDate? releaseDate, DateTime today, int daysAheadThreshold, bool includeGamesWithoutReleaseDate)
        {
            // ReleaseDate.Empty carries Year 0, which is not a constructible DateTime.
            // Treat it the same as no date at all.
            if (!releaseDate.HasValue || releaseDate.Value.Year < 1 || releaseDate.Value.Year > 9999)
            {
                return includeGamesWithoutReleaseDate;
            }

            today = today.Date;

            // Compare against the end of the period, so a year-only 2026 title stays
            // tagged all year instead of dropping out on January 1st.
            if (PeriodEnd(releaseDate.Value) <= today)
            {
                return false;
            }

            if (daysAheadThreshold <= 0)
            {
                return true;
            }

            // The window is measured from the earliest possible release date, so a
            // year-only title isn't excluded merely because its period runs past the end
            // of the window. Subtracting rather than calling today.AddDays keeps an
            // absurd user-supplied threshold from throwing instead of just matching.
            return (PeriodStart(releaseDate.Value) - today).TotalDays <= daysAheadThreshold;
        }
    }
}
