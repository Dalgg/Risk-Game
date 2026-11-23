using System.Linq;
using RiskGame.Exceptions;
using RiskGame.Server.Services;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class AbandonedLobbyTests
    {
        private const int TwoPlayers = 2;
        private const int ThreePlayers = 3;

        [Fact]
        public void RemoveAbandonedParticipant_Host_MigratesHost()
        {
            using AbandonedLobbyBuilder setup = new AbandonedLobbyBuilder();

            setup.Lobbies.RemoveAbandonedParticipant(setup.HostAbandonment());

            Assert.Equal(AbandonedLobbyBuilder.RemainingId, setup.Snapshot().HostUserId);
        }

        [Fact]
        public void RemoveAbandonedParticipant_Host_UpdatesHostFlag()
        {
            using AbandonedLobbyBuilder setup = new AbandonedLobbyBuilder();

            setup.Lobbies.RemoveAbandonedParticipant(setup.HostAbandonment());

            Assert.True(setup.Snapshot().Players.Single(player =>
                player.Player.UserId == AbandonedLobbyBuilder.RemainingId).IsHost);
        }

        [Fact]
        public void RemoveAbandonedParticipant_OriginalMatch_RemovesSeat()
        {
            using AbandonedLobbyBuilder setup = new AbandonedLobbyBuilder();

            setup.Lobbies.RemoveAbandonedParticipant(setup.HostAbandonment());

            Assert.Equal(TwoPlayers, setup.Snapshot().Players.Count);
        }

        [Fact]
        public void RemoveAbandonedParticipant_RemovedUser_CannotSubscribeToLobby()
        {
            using AbandonedLobbyBuilder setup = new AbandonedLobbyBuilder();
            setup.Lobbies.RemoveAbandonedParticipant(setup.HostAbandonment());

            Assert.Throws<LobbyNotFoundException>(() => setup.Snapshot(AbandonedLobbyBuilder.HostId));
        }

        [Fact]
        public void RemoveAbandonedParticipant_RepeatedCleanup_IsIdempotent()
        {
            using AbandonedLobbyBuilder setup = new AbandonedLobbyBuilder();
            setup.Lobbies.RemoveAbandonedParticipant(setup.HostAbandonment());

            setup.Lobbies.RemoveAbandonedParticipant(setup.HostAbandonment());

            Assert.Equal(TwoPlayers, setup.Snapshot().Players.Count);
        }

        [Fact]
        public void RemoveAbandonedParticipant_DifferentMatch_DoesNotRemoveSeat()
        {
            using AbandonedLobbyBuilder setup = new AbandonedLobbyBuilder();
            AbandonedMatchPlayer lateCleanup = new AbandonedMatchPlayer(AbandonedLobbyBuilder.DifferentMatchId,
                AbandonedLobbyBuilder.OriginalLobbyId, AbandonedLobbyBuilder.HostId);

            setup.Lobbies.RemoveAbandonedParticipant(lateCleanup);

            Assert.Equal(ThreePlayers, setup.Snapshot().Players.Count);
        }

        [Fact]
        public void RemoveAbandonedParticipant_DifferentLobby_DoesNotRemoveSeat()
        {
            using AbandonedLobbyBuilder setup = new AbandonedLobbyBuilder();
            AbandonedMatchPlayer lateCleanup = new AbandonedMatchPlayer(AbandonedLobbyBuilder.MatchId,
                AbandonedLobbyBuilder.NewLobbyId, AbandonedLobbyBuilder.HostId);

            setup.Lobbies.RemoveAbandonedParticipant(lateCleanup);

            Assert.Equal(ThreePlayers, setup.Snapshot().Players.Count);
        }

        [Fact]
        public void RemoveAbandonedParticipant_UserJoinedNewLobby_KeepsNewMembership()
        {
            using AbandonedLobbyBuilder setup = new AbandonedLobbyBuilder();
            setup.MoveHostToNewLobby();

            setup.Lobbies.RemoveAbandonedParticipant(setup.HostAbandonment());

            Assert.Equal(AbandonedLobbyBuilder.NewLobbyId, setup.Snapshot(AbandonedLobbyBuilder.HostId).LobbyId);
        }

        [Fact]
        public void Cleanup_AfterAbandonment_ClearsPendingReturns()
        {
            using AbandonedLobbyBuilder setup = new AbandonedLobbyBuilder();
            setup.FinishByAbandonment();
            setup.DrainCleanup();

            bool canStartAgain = setup.ReturnSurvivorAndJoinNewPlayer().CanStartMatch;

            Assert.True(canStartAgain);
        }

        [Fact]
        public void TakeAbandonedParticipants_AfterDrain_ReturnsEmpty()
        {
            using AbandonedLobbyBuilder setup = new AbandonedLobbyBuilder();
            setup.FinishByAbandonment();

            setup.DrainCleanup();

            Assert.Empty(setup.Matches.TakeAbandonedParticipants());
        }
    }
}
