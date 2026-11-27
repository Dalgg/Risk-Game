using System;
using RiskGame.Exceptions;
using RiskGame.Server.Services;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class ReportFlowTests
    {
        private const int OneTick = 1;
        private const int RequiredDistinctReporters = 3;

        [Fact]
        public void ReportPlayer_RegisteredPlayersInWaitingLobby_RecordsReport()
        {
            ReportFlowBuilder setup = new ReportFlowBuilder();

            setup.Manager.ReportPlayer(setup.Command());

            Assert.Equal(ReportFlowBuilder.OneReport, setup.Repository.Calls);
        }

        [Fact]
        public void ReportPlayer_UsesProvidedUtcClock()
        {
            ReportFlowBuilder setup = new ReportFlowBuilder();

            setup.Manager.ReportPlayer(setup.Command());

            Assert.Equal(setup.Time.GetUtcNow().UtcDateTime, setup.Repository.LastSubmission.ReportDateUtc);
        }

        [Fact]
        public void ReportPlayer_ProposesExactlyTwentyFourHours()
        {
            ReportFlowBuilder setup = new ReportFlowBuilder();

            setup.Manager.ReportPlayer(setup.Command());

            Assert.Equal(setup.Time.GetUtcNow().AddHours(ReportFlowBuilder.SuspensionHours).UtcDateTime,
                setup.Repository.LastSubmission.BannedUntilUtc);
        }

        [Fact]
        public void ReportPlayer_RequiresThreeDistinctReporters()
        {
            ReportFlowBuilder setup = new ReportFlowBuilder();

            setup.Manager.ReportPlayer(setup.Command());

            Assert.Equal(RequiredDistinctReporters, setup.Repository.LastSubmission.RequiredDistinctReporters);
        }

        [Fact]
        public void ReportPlayer_InvalidToken_DoesNotAccessRepository()
        {
            ReportFlowBuilder setup = new ReportFlowBuilder();
            setup.Registry.RemoveSession(setup.ReporterToken);

            Record.Exception(() => setup.Manager.ReportPlayer(setup.Command()));

            Assert.Equal(ReportFlowBuilder.NoReports, setup.Repository.Calls);
        }

        [Fact]
        public void ReportPlayer_AnotherAccountsToken_DoesNotAccessRepository()
        {
            ReportFlowBuilder setup = new ReportFlowBuilder();
            PlayerReportCommand command = setup.CommandForPlayer(ReportFlowBuilder.ReporterId, setup.HostToken);

            Record.Exception(() => setup.Manager.ReportPlayer(command));

            Assert.Equal(ReportFlowBuilder.NoReports, setup.Repository.Calls);
        }

        [Fact]
        public void ReportPlayer_SelfReport_DoesNotAccessRepository()
        {
            ReportFlowBuilder setup = new ReportFlowBuilder();
            PlayerReportCommand command = setup.CommandForTarget(ReportFlowBuilder.ReporterId);

            Record.Exception(() => setup.Manager.ReportPlayer(command));

            Assert.Equal(ReportFlowBuilder.NoReports, setup.Repository.Calls);
        }

        [Fact]
        public void ReportPlayer_GuestReporter_DoesNotAccessRepository()
        {
            ReportFlowBuilder setup = new ReportFlowBuilder();
            setup.JoinGuest();
            PlayerReportCommand command = setup.CommandForPlayer(ReportFlowBuilder.GuestId, string.Empty);

            Record.Exception(() => setup.Manager.ReportPlayer(command));

            Assert.Equal(ReportFlowBuilder.NoReports, setup.Repository.Calls);
        }

        [Fact]
        public void ReportPlayer_GuestTarget_DoesNotAccessRepository()
        {
            ReportFlowBuilder setup = new ReportFlowBuilder();
            setup.JoinGuest();
            PlayerReportCommand command = setup.CommandForTarget(ReportFlowBuilder.GuestId);

            Record.Exception(() => setup.Manager.ReportPlayer(command));

            Assert.Equal(ReportFlowBuilder.NoReports, setup.Repository.Calls);
        }

        [Fact]
        public void ReportPlayer_DifferentLobbyIdentifier_DoesNotAccessRepository()
        {
            ReportFlowBuilder setup = new ReportFlowBuilder();
            PlayerReportCommand command = setup.CommandForLobby(ReportFlowBuilder.WrongLobbyId);

            Record.Exception(() => setup.Manager.ReportPlayer(command));

            Assert.Equal(ReportFlowBuilder.NoReports, setup.Repository.Calls);
        }

        [Fact]
        public void ReportPlayer_TargetOutsideLobby_DoesNotAccessRepository()
        {
            ReportFlowBuilder setup = new ReportFlowBuilder();
            PlayerReportCommand command = setup.CommandForTarget(ReportFlowBuilder.OutsiderId);

            Record.Exception(() => setup.Manager.ReportPlayer(command));

            Assert.Equal(ReportFlowBuilder.NoReports, setup.Repository.Calls);
        }

        [Fact]
        public void ReportPlayer_ReporterOutsideLobby_DoesNotAccessRepository()
        {
            ReportFlowBuilder setup = new ReportFlowBuilder();
            string outsiderToken = setup.Registry.Register(ReportFlowBuilder.OutsiderId);
            PlayerReportCommand command = setup.CommandForPlayer(ReportFlowBuilder.OutsiderId, outsiderToken);

            Record.Exception(() => setup.Manager.ReportPlayer(command));

            Assert.Equal(ReportFlowBuilder.NoReports, setup.Repository.Calls);
        }

        [Fact]
        public void ReportPlayer_MatchAlreadyStarted_DoesNotAccessRepository()
        {
            ReportFlowBuilder setup = new ReportFlowBuilder();
            setup.Lobbies.StartMatch(ReportFlowBuilder.HostId);

            Record.Exception(() => setup.Manager.ReportPlayer(setup.Command()));

            Assert.Equal(ReportFlowBuilder.NoReports, setup.Repository.Calls);
        }

        [Fact]
        public void ReportPlayer_Duplicate_ReturnsDuplicateOutcome()
        {
            ReportFlowBuilder setup = new ReportFlowBuilder();
            setup.Repository.IsDuplicate = true;

            bool duplicate = setup.Manager.ReportPlayer(setup.Command());

            Assert.True(duplicate);
        }

        [Fact]
        public void ReportPlayer_SuspendedHost_ReassignsHostToRemainingPlayer()
        {
            ReportFlowBuilder setup = new ReportFlowBuilder();

            setup.SuspendReportedHost();

            Assert.Equal(ReportFlowBuilder.ReporterId, setup.Snapshot().HostUserId);
        }

        [Fact]
        public void ReportPlayer_SuspendedHost_RemovesHostFromLobby()
        {
            ReportFlowBuilder setup = new ReportFlowBuilder();

            setup.SuspendReportedHost();

            Assert.Single(setup.Snapshot().Players);
        }

        [Fact]
        public void ReportPlayer_Suspension_ImmediatelyRejectsOldToken()
        {
            ReportFlowBuilder setup = new ReportFlowBuilder();

            setup.SuspendReportedHost();

            Assert.Throws<AccountSuspendedException>(() => setup.Registry.GetUserId(setup.HostToken));
        }

        [Fact]
        public void ReportPlayer_SaveFails_KeepsHostInLobby()
        {
            ReportFlowBuilder setup = new ReportFlowBuilder();
            setup.Repository.FailSave = true;

            Record.Exception(() => setup.Manager.ReportPlayer(setup.Command()));

            Assert.Equal(ReportFlowBuilder.HostId, setup.Snapshot().HostUserId);
        }

        [Fact]
        public void ReportPlayer_SaveFails_KeepsHostTokenValid()
        {
            ReportFlowBuilder setup = new ReportFlowBuilder();
            setup.Repository.FailSave = true;

            Record.Exception(() => setup.Manager.ReportPlayer(setup.Command()));

            Assert.Equal(ReportFlowBuilder.HostId, setup.Registry.GetUserId(setup.HostToken));
        }

        [Fact]
        public void SuspendUser_JustBeforeTwentyFourHours_RejectsNewSession()
        {
            ReportFlowBuilder setup = new ReportFlowBuilder();
            setup.SuspendReportedHost();
            setup.Time.Advance(TimeSpan.FromHours(ReportFlowBuilder.SuspensionHours) - TimeSpan.FromTicks(OneTick));

            Assert.Throws<AccountSuspendedException>(() => setup.Registry.Register(ReportFlowBuilder.HostId));
        }

        [Fact]
        public void SuspendUser_ExactlyAtExpiry_AllowsNewSession()
        {
            ReportFlowBuilder setup = new ReportFlowBuilder();
            setup.SuspendReportedHost();
            setup.Time.Advance(TimeSpan.FromHours(ReportFlowBuilder.SuspensionHours));

            string replacementToken = setup.Registry.Register(ReportFlowBuilder.HostId);

            Assert.Equal(ReportFlowBuilder.HostId, setup.Registry.GetUserId(replacementToken));
        }

        [Fact]
        public void SuspendUser_ActiveMatch_AbandonsSuspendedParticipantImmediately()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();

            setup.Manager.SuspendUser(DisconnectedMatchBuilder.FirstUserId);

            Assert.True(setup.Match.GetParticipant(DisconnectedMatchBuilder.FirstUserId).HasAbandoned);
        }

        [Fact]
        public void SuspendUser_CurrentPlayer_AdvancesTurnWithoutGracePeriod()
        {
            DisconnectedMatchBuilder setup = new DisconnectedMatchBuilder();

            setup.Manager.SuspendUser(DisconnectedMatchBuilder.FirstUserId);

            Assert.Equal(DisconnectedMatchBuilder.SecondUserId, setup.Match.CurrentTurnUserId);
        }
    }
}
