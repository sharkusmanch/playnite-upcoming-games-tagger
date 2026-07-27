using Microsoft.VisualStudio.TestTools.UnitTesting;
using Playnite.SDK.Models;
using System;

namespace UpcomingGamesTagger.Tests
{
    [TestClass]
    public class UpcomingGameEvaluatorTests
    {
        private static readonly DateTime Today = new DateTime(2026, 7, 26);

        private const int OneYear = 365;
        private const bool ExcludeUndated = false;
        private const bool IncludeUndated = true;

        #region PeriodEnd

        [TestMethod]
        public void PeriodEnd_YearOnly_IsLastDayOfYear()
        {
            Assert.AreEqual(new DateTime(2026, 12, 31), UpcomingGameEvaluator.PeriodEnd(new ReleaseDate(2026)));
        }

        [TestMethod]
        public void PeriodEnd_YearAndMonth_IsLastDayOfMonth()
        {
            Assert.AreEqual(new DateTime(2026, 11, 30), UpcomingGameEvaluator.PeriodEnd(new ReleaseDate(2026, 11)));
        }

        [TestMethod]
        public void PeriodEnd_February_AccountsForLeapYear()
        {
            Assert.AreEqual(new DateTime(2027, 2, 28), UpcomingGameEvaluator.PeriodEnd(new ReleaseDate(2027, 2)));
            Assert.AreEqual(new DateTime(2028, 2, 29), UpcomingGameEvaluator.PeriodEnd(new ReleaseDate(2028, 2)));
        }

        [TestMethod]
        public void PeriodEnd_FullDate_IsThatDay()
        {
            Assert.AreEqual(new DateTime(2026, 11, 17), UpcomingGameEvaluator.PeriodEnd(new ReleaseDate(2026, 11, 17)));
        }

        #endregion

        #region Year-only precision

        [TestMethod]
        public void IsUpcoming_YearOnlyInCurrentYear_IsUpcoming()
        {
            // Playnite collapses a year-only date to January 1st. Comparing against that
            // start date dropped every unannounced title dated to the current year.
            Assert.IsTrue(Evaluate(new ReleaseDate(2026)));
        }

        [TestMethod]
        public void IsUpcoming_YearOnlyInNextYear_IsUpcoming()
        {
            Assert.IsTrue(Evaluate(new ReleaseDate(2027)));
        }

        [TestMethod]
        public void IsUpcoming_YearOnlyInPastYear_IsNotUpcoming()
        {
            Assert.IsFalse(Evaluate(new ReleaseDate(2025)));
        }

        [TestMethod]
        public void IsUpcoming_YearOnlyStartingBeyondThreshold_IsNotUpcoming()
        {
            // 2028-01-01 is more than a year past 2026-07-26.
            Assert.IsFalse(Evaluate(new ReleaseDate(2028)));
        }

        [TestMethod]
        public void IsUpcoming_YearOnlyLateInThatYear_IsStillUpcoming()
        {
            Assert.IsTrue(UpcomingGameEvaluator.IsUpcoming(
                new ReleaseDate(2026), new DateTime(2026, 12, 30), OneYear, ExcludeUndated));
        }

        [TestMethod]
        public void IsUpcoming_YearOnlyOnLastDayOfThatYear_IsNotUpcoming()
        {
            // The last possible release day is treated like a known release day: by then
            // the game has either shipped or is shipping, so it is no longer upcoming.
            Assert.IsFalse(UpcomingGameEvaluator.IsUpcoming(
                new ReleaseDate(2026), new DateTime(2026, 12, 31), OneYear, ExcludeUndated));
        }

        [TestMethod]
        public void IsUpcoming_YearOnlyOnFirstDayOfFollowingYear_IsNotUpcoming()
        {
            Assert.IsFalse(UpcomingGameEvaluator.IsUpcoming(
                new ReleaseDate(2026), new DateTime(2027, 1, 1), OneYear, ExcludeUndated));
        }

        #endregion

        #region Month precision

        [TestMethod]
        public void IsUpcoming_MonthOnlyLaterThisMonth_IsUpcoming()
        {
            // The date collapses to 2026-07-01, which is already past, but the month is not.
            Assert.IsTrue(Evaluate(new ReleaseDate(2026, 7)));
        }

        [TestMethod]
        public void IsUpcoming_MonthOnlyPastTheFirstOfThatMonth_IsStillUpcoming()
        {
            // Previously the tag was stripped on the 1st, before the game had shipped.
            Assert.IsTrue(UpcomingGameEvaluator.IsUpcoming(
                new ReleaseDate(2026, 12), new DateTime(2026, 12, 20), OneYear, ExcludeUndated));
        }

        [TestMethod]
        public void IsUpcoming_MonthOnlyAfterThatMonthEnds_IsNotUpcoming()
        {
            Assert.IsFalse(UpcomingGameEvaluator.IsUpcoming(
                new ReleaseDate(2026, 12), new DateTime(2027, 1, 1), OneYear, ExcludeUndated));
        }

        #endregion

        #region Day precision

        [TestMethod]
        public void IsUpcoming_FutureDate_IsUpcoming()
        {
            Assert.IsTrue(Evaluate(new ReleaseDate(2026, 8, 15)));
        }

        [TestMethod]
        public void IsUpcoming_ReleaseDay_IsNotUpcoming()
        {
            Assert.IsFalse(Evaluate(new ReleaseDate(2026, 7, 26)));
        }

        [TestMethod]
        public void IsUpcoming_PastDate_IsNotUpcoming()
        {
            Assert.IsFalse(Evaluate(new ReleaseDate(2026, 7, 25)));
        }

        [TestMethod]
        public void IsUpcoming_IgnoresTimeOfDay()
        {
            Assert.IsTrue(UpcomingGameEvaluator.IsUpcoming(
                new ReleaseDate(2026, 7, 27), Today.AddHours(23), OneYear, ExcludeUndated));
        }

        #endregion

        #region Threshold

        [TestMethod]
        public void IsUpcoming_OnThresholdBoundary_IsUpcoming()
        {
            Assert.IsTrue(UpcomingGameEvaluator.IsUpcoming(
                new ReleaseDate(2026, 8, 25), Today, 30, ExcludeUndated));
        }

        [TestMethod]
        public void IsUpcoming_JustPastThreshold_IsNotUpcoming()
        {
            Assert.IsFalse(UpcomingGameEvaluator.IsUpcoming(
                new ReleaseDate(2026, 8, 26), Today, 30, ExcludeUndated));
        }

        [TestMethod]
        public void IsUpcoming_ZeroThreshold_MeansNoLimit()
        {
            Assert.IsTrue(UpcomingGameEvaluator.IsUpcoming(
                new ReleaseDate(2099, 1, 1), Today, 0, ExcludeUndated));
        }

        [TestMethod]
        public void IsUpcoming_AbsurdlyLargeThreshold_DoesNotThrow()
        {
            // The threshold comes straight from an unvalidated text box; adding it to a
            // DateTime would overflow and abort every pass.
            Assert.IsTrue(UpcomingGameEvaluator.IsUpcoming(
                new ReleaseDate(2027, 1, 1), Today, int.MaxValue, ExcludeUndated));
        }

        [TestMethod]
        public void IsUpcoming_NegativeThreshold_MeansNoLimit()
        {
            Assert.IsTrue(UpcomingGameEvaluator.IsUpcoming(
                new ReleaseDate(2099, 1, 1), Today, -1, ExcludeUndated));
        }

        #endregion

        #region Missing and malformed dates

        [TestMethod]
        public void IsUpcoming_NoReleaseDate_FollowsSetting()
        {
            Assert.IsFalse(UpcomingGameEvaluator.IsUpcoming(null, Today, OneYear, ExcludeUndated));
            Assert.IsTrue(UpcomingGameEvaluator.IsUpcoming(null, Today, OneYear, IncludeUndated));
        }

        [TestMethod]
        public void IsUpcoming_EmptyReleaseDate_IsTreatedAsUndated()
        {
            // ReleaseDate.Empty carries Year 0, which no DateTime can represent.
            Assert.IsFalse(UpcomingGameEvaluator.IsUpcoming(ReleaseDate.Empty, Today, OneYear, ExcludeUndated));
            Assert.IsTrue(UpcomingGameEvaluator.IsUpcoming(ReleaseDate.Empty, Today, OneYear, IncludeUndated));
        }

        #endregion

        private static bool Evaluate(ReleaseDate releaseDate)
        {
            return UpcomingGameEvaluator.IsUpcoming(releaseDate, Today, OneYear, ExcludeUndated);
        }
    }
}
