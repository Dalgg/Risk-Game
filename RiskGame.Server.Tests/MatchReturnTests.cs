using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;
using RiskGame.Server.Services;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class MatchReturnTests
    {
        private const int MatchId = 301;
        private const int LobbyId = 18;
        private const int UserId = 41;
        private const int OtherUserId = 43;
        private const int UnknownMatchId = 999;
        private const int ShortColumn = 2;
        private const int FirstClaim = 3;
        private const int SecondClaim = 12;
        private const int FirstPairIndex = 1;
        private static readonly int[] ShortDice = { 1, 1, 1, 1 };
        private readonly MatchSessionManager manager;
        private readonly ActiveMatch match;
        private readonly string token;

        public MatchReturnTests()
        {
            ProfileSessionRegistry registry = new ProfileSessionRegistry();
            token = registry.Register(UserId);
            manager = new MatchSessionManager(registry, new RecordingMatchRepository());
            LobbyDetails lobby = new LobbyDetails { LobbyId = LobbyId, Rules = new LobbyRules() };
            lobby.Players.Add(new LobbyPlayer { Player = new PlayerSummary { UserId = UserId } });
            lobby.Players.Add(new LobbyPlayer { Player = new PlayerSummary { UserId = OtherUserId } });
            match = manager.CreateMatch(MatchId, lobby);
        }

        [Fact]
        public void GetFinishedLobbyId_UnfinishedMatch_RejectsReturn()
        {
            MatchParticipantSession participant = CreateParticipant();

            Assert.Throws<MatchActionException>(() => manager.GetFinishedLobbyId(participant));
        }

        [Fact]
        public void GetFinishedLobbyId_UnknownMatch_RejectsReturn()
        {
            MatchParticipantSession participant = new MatchParticipantSession(UnknownMatchId, UserId, token);

            Assert.Throws<MatchNotFoundException>(() => manager.GetFinishedLobbyId(participant));
        }

        [Fact]
        public void GetFinishedLobbyId_TokenOfAnotherParticipant_RejectsReturn()
        {
            MatchParticipantSession participant = new MatchParticipantSession(MatchId, OtherUserId, token);

            Assert.Throws<InvalidSessionException>(() => manager.GetFinishedLobbyId(participant));
        }

        [Fact]
        public void GetFinishedLobbyId_FinishedMatch_ReturnsOriginalLobby()
        {
            FinishMatch();

            int originalLobbyId = manager.GetFinishedLobbyId(CreateParticipant());

            Assert.Equal(LobbyId, originalLobbyId);
        }

        private void FinishMatch()
        {
            foreach (MatchColumn column in match.Columns)
            {
                if (column.ColumnNumber == FirstClaim || column.ColumnNumber == SecondClaim)
                {
                    column.SaveProgress(UserId, column.MaximumSteps);
                }
            }
            MatchColumn shortColumn = System.Linq.Enumerable.First(match.Columns,
                column => column.ColumnNumber == ShortColumn);
            while (!match.TurnProgress.StepsByColumn.ContainsKey(ShortColumn)
                || match.TurnProgress.StepsByColumn[ShortColumn] < shortColumn.MaximumSteps)
            {
                match.ApplyRoll(ShortDice, match.RollId);
                match.ChoosePair(match.RollId, FirstPairIndex);
            }
            match.StopTurn(match.TurnId);
        }

        private MatchParticipantSession CreateParticipant()
        {
            return new MatchParticipantSession(MatchId, UserId, token);
        }
    }
}
