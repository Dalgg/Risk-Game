using System;
using RiskGame.Exceptions;
using RiskGame.Server.Services;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class PlayerReportRepositoryTests
    {
        private const int OneReport = 1;
        private const int TwoReports = 2;
        private const int ThreeReports = 3;
        private const int NoPendingReports = 0;

        [Fact]
        public void RecordReport_ValidSubmission_PersistsReport()
        {
            using PlayerReportRepositoryBuilder setup = new PlayerReportRepositoryBuilder();

            setup.Report(PlayerReportRepositoryBuilder.FirstReporterId, PlayerReportRepositoryBuilder.FirstLobbyId);

            Assert.Equal(OneReport, setup.ReportCount());
        }

        [Fact]
        public void RecordReport_ValidSubmission_PreservesSubmittedTimestamp()
        {
            using PlayerReportRepositoryBuilder setup = new PlayerReportRepositoryBuilder();

            setup.Report(PlayerReportRepositoryBuilder.FirstReporterId, PlayerReportRepositoryBuilder.FirstLobbyId);

            Assert.Equal(PlayerReportRepositoryBuilder.BaseTime, setup.ReadFirstReport().ReportDate);
        }

        [Fact]
        public void RecordReport_SamePairAndLobby_ReportsDuplicate()
        {
            using PlayerReportRepositoryBuilder setup = new PlayerReportRepositoryBuilder();
            setup.Report(PlayerReportRepositoryBuilder.FirstReporterId, PlayerReportRepositoryBuilder.FirstLobbyId);

            PlayerReportResult result = setup.Report(PlayerReportRepositoryBuilder.FirstReporterId,
                PlayerReportRepositoryBuilder.FirstLobbyId);

            Assert.True(result.IsDuplicate);
        }

        [Fact]
        public void RecordReport_SamePairAndLobby_DoesNotInsertAgain()
        {
            using PlayerReportRepositoryBuilder setup = new PlayerReportRepositoryBuilder();
            setup.Report(PlayerReportRepositoryBuilder.FirstReporterId, PlayerReportRepositoryBuilder.FirstLobbyId);

            setup.Report(PlayerReportRepositoryBuilder.FirstReporterId, PlayerReportRepositoryBuilder.FirstLobbyId);

            Assert.Equal(OneReport, setup.ReportCount());
        }

        [Fact]
        public void RecordReport_SecondDistinctReporter_DoesNotSuspend()
        {
            using PlayerReportRepositoryBuilder setup = new PlayerReportRepositoryBuilder();
            setup.Report(PlayerReportRepositoryBuilder.FirstReporterId, PlayerReportRepositoryBuilder.FirstLobbyId);

            PlayerReportResult result = setup.Report(PlayerReportRepositoryBuilder.SecondReporterId,
                PlayerReportRepositoryBuilder.FirstLobbyId);

            Assert.False(result.BannedUntil.HasValue);
        }

        [Fact]
        public void RecordReport_ThirdDistinctReporter_SuspendsForConfiguredDuration()
        {
            using PlayerReportRepositoryBuilder setup = new PlayerReportRepositoryBuilder();

            PlayerReportResult result = setup.TriggerSuspension();

            Assert.Equal(PlayerReportRepositoryBuilder.BaseTime.AddHours(PlayerReportRepositoryBuilder.SuspensionHours),
                result.BannedUntil);
        }

        [Fact]
        public void RecordReport_ThirdDistinctReporter_PersistsSuspension()
        {
            using PlayerReportRepositoryBuilder setup = new PlayerReportRepositoryBuilder();

            setup.TriggerSuspension();

            Assert.Equal(PlayerReportRepositoryBuilder.BaseTime.AddHours(PlayerReportRepositoryBuilder.SuspensionHours),
                setup.StoredBannedUntil());
        }

        [Fact]
        public void RecordReport_ThirdDistinctReporter_ReturnsUtcSuspension()
        {
            using PlayerReportRepositoryBuilder setup = new PlayerReportRepositoryBuilder();

            PlayerReportResult result = setup.TriggerSuspension();

            Assert.Equal(DateTimeKind.Utc, result.BannedUntil.GetValueOrDefault().Kind);
        }

        [Fact]
        public void RecordReport_ThirdDistinctReporter_ProcessesPendingReports()
        {
            using PlayerReportRepositoryBuilder setup = new PlayerReportRepositoryBuilder();

            setup.TriggerSuspension();

            Assert.Equal(NoPendingReports, setup.PendingCount());
        }

        [Fact]
        public void RecordReport_SameReporterAcrossThreeLobbies_DoesNotSuspend()
        {
            using PlayerReportRepositoryBuilder setup = new PlayerReportRepositoryBuilder();
            setup.Report(PlayerReportRepositoryBuilder.FirstReporterId, PlayerReportRepositoryBuilder.FirstLobbyId);
            setup.Report(PlayerReportRepositoryBuilder.FirstReporterId, PlayerReportRepositoryBuilder.SecondLobbyId);

            PlayerReportResult result = setup.Report(PlayerReportRepositoryBuilder.FirstReporterId,
                PlayerReportRepositoryBuilder.ThirdLobbyId);

            Assert.False(result.BannedUntil.HasValue);
        }

        [Fact]
        public void RecordReport_AlreadySuspended_DoesNotAddReport()
        {
            using PlayerReportRepositoryBuilder setup = new PlayerReportRepositoryBuilder();
            setup.TriggerSuspension();

            setup.Report(PlayerReportRepositoryBuilder.FirstReporterId, PlayerReportRepositoryBuilder.SecondLobbyId);

            Assert.Equal(ThreeReports, setup.ReportCount());
        }

        [Fact]
        public void RecordReport_AfterSuspensionExpires_OldReportsDoNotSuspendAgain()
        {
            using PlayerReportRepositoryBuilder setup = new PlayerReportRepositoryBuilder();
            setup.TriggerSuspension();

            PlayerReportResult result = setup.ReportAfterSuspension();

            Assert.True(result.BannedUntil < PlayerReportRepositoryBuilder.BaseTime
                .AddHours(PlayerReportRepositoryBuilder.NewSuspensionOffsetHours));
        }

        [Fact]
        public void RecordReport_AfterSuspensionExpires_KeepsNewReportPending()
        {
            using PlayerReportRepositoryBuilder setup = new PlayerReportRepositoryBuilder();
            setup.TriggerSuspension();

            setup.ReportAfterSuspension();

            Assert.Equal(OneReport, setup.PendingCount());
        }

        [Fact]
        public void RecordReport_SelfReport_Rejects()
        {
            using PlayerReportRepositoryBuilder setup = new PlayerReportRepositoryBuilder();

            Assert.Throws<ReportNotAllowedException>(() => setup.Repository.RecordReport(
                setup.Submission(PlayerReportRepositoryBuilder.ReportedId,
                    PlayerReportRepositoryBuilder.FirstLobbyId)));
        }

        [Fact]
        public void RecordReport_GuestReporter_Rejects()
        {
            using PlayerReportRepositoryBuilder setup = new PlayerReportRepositoryBuilder();

            Assert.Throws<ReportNotAllowedException>(() => setup.Repository.RecordReport(
                setup.Submission(PlayerReportRepositoryBuilder.GuestId, PlayerReportRepositoryBuilder.FirstLobbyId)));
        }

        [Fact]
        public void RecordReport_UnknownReporter_DoesNotPersist()
        {
            using PlayerReportRepositoryBuilder setup = new PlayerReportRepositoryBuilder();
            Record.Exception(() => setup.Repository.RecordReport(setup.Submission(
                PlayerReportRepositoryBuilder.UnknownUserId, PlayerReportRepositoryBuilder.FirstLobbyId)));

            Assert.Equal(NoPendingReports, setup.ReportCount());
        }

        [Fact]
        public void RecordReport_UnknownReportedAccount_Rejects()
        {
            using PlayerReportRepositoryBuilder setup = new PlayerReportRepositoryBuilder();
            PlayerReportSubmission submission = new PlayerReportSubmission
            {
                ReporterId = PlayerReportRepositoryBuilder.FirstReporterId,
                ReportedId = PlayerReportRepositoryBuilder.UnknownUserId,
                LobbyId = PlayerReportRepositoryBuilder.FirstLobbyId,
                ReportDateUtc = PlayerReportRepositoryBuilder.BaseTime,
                RequiredDistinctReporters = PlayerReportRepositoryBuilder.RequiredDistinctReporters,
                BannedUntilUtc = PlayerReportRepositoryBuilder.BaseTime
                    .AddHours(PlayerReportRepositoryBuilder.SuspensionHours)
            };

            Assert.Throws<ProfileNotFoundException>(() => setup.Repository.RecordReport(submission));
        }

        [Fact]
        public void RecordReport_UnknownLobby_Rejects()
        {
            using PlayerReportRepositoryBuilder setup = new PlayerReportRepositoryBuilder();

            Assert.Throws<ReportNotAllowedException>(() => setup.Repository.RecordReport(
                setup.Submission(PlayerReportRepositoryBuilder.FirstReporterId,
                    PlayerReportRepositoryBuilder.UnknownLobbyId)));
        }

        [Fact]
        public void RecordReport_SuspensionSaveFails_RollsBackInsertedReport()
        {
            using PlayerReportRepositoryBuilder setup = new PlayerReportRepositoryBuilder();
            setup.Report(PlayerReportRepositoryBuilder.FirstReporterId, PlayerReportRepositoryBuilder.FirstLobbyId);
            setup.Report(PlayerReportRepositoryBuilder.SecondReporterId, PlayerReportRepositoryBuilder.FirstLobbyId);
            setup.FailSuspensionSave = true;

            Record.Exception(() => setup.Report(PlayerReportRepositoryBuilder.ThirdReporterId,
                PlayerReportRepositoryBuilder.FirstLobbyId));

            Assert.Equal(TwoReports, setup.ReportCount());
        }

        [Fact]
        public void RecordReport_SuspensionSaveFails_PreservesPendingReports()
        {
            using PlayerReportRepositoryBuilder setup = new PlayerReportRepositoryBuilder();
            setup.Report(PlayerReportRepositoryBuilder.FirstReporterId, PlayerReportRepositoryBuilder.FirstLobbyId);
            setup.Report(PlayerReportRepositoryBuilder.SecondReporterId, PlayerReportRepositoryBuilder.FirstLobbyId);
            setup.FailSuspensionSave = true;

            Record.Exception(() => setup.Report(PlayerReportRepositoryBuilder.ThirdReporterId,
                PlayerReportRepositoryBuilder.FirstLobbyId));

            Assert.Equal(TwoReports, setup.PendingCount());
        }

        [Fact]
        public void RecordReport_SuspensionSaveFails_DoesNotPersistSuspension()
        {
            using PlayerReportRepositoryBuilder setup = new PlayerReportRepositoryBuilder();
            setup.Report(PlayerReportRepositoryBuilder.FirstReporterId, PlayerReportRepositoryBuilder.FirstLobbyId);
            setup.Report(PlayerReportRepositoryBuilder.SecondReporterId, PlayerReportRepositoryBuilder.FirstLobbyId);
            setup.FailSuspensionSave = true;

            Record.Exception(() => setup.Report(PlayerReportRepositoryBuilder.ThirdReporterId,
                PlayerReportRepositoryBuilder.FirstLobbyId));

            Assert.False(setup.StoredBannedUntil().HasValue);
        }
    }
}
