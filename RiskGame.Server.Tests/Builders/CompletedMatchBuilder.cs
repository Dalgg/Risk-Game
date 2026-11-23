using System.Linq;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Server.Services;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class CompletedMatchBuilder
    {
        private const int MatchIdentifier = 501;
        private const int LobbyIdentifier = 801;
        private const int RegisteredWinner = 41;
        private const int RegisteredOpponent = 43;
        private const int GuestWinner = -41;
        private const int GuestOpponent = -43;
        private const int FirstClaim = 3;
        private const int SecondClaim = 12;
        private const int FirstPair = 1;
        private const int WinningRollCount = 2;
        private static readonly int[] ShortDice = { 1, 1, 1, 1 };
        private bool guestWinner;
        private bool guestOpponent;
        private MatchSessionManager? manager;
        private int matchId = MatchIdentifier;
        private RulesVariant rulesVariant = RulesVariant.Standard;
        private bool seedSecondClaim = true;

        public CompletedMatchBuilder WithRules(RulesVariant rulesVariant)
        {
            this.rulesVariant = rulesVariant;
            return this;
        }

        public CompletedMatchBuilder WithOnePreviousClaim()
        {
            seedSecondClaim = false;
            return this;
        }

        public CompletedMatchBuilder WithMatchId(int matchId)
        {
            this.matchId = matchId;
            return this;
        }

        public CompletedMatchBuilder WithManager(MatchSessionManager manager)
        {
            this.manager = manager;
            return this;
        }

        public CompletedMatchBuilder AsGuestWinner()
        {
            guestWinner = true;
            return this;
        }

        public CompletedMatchBuilder WithGuestOpponent()
        {
            guestOpponent = true;
            return this;
        }

        public ActiveMatch Build()
        {
            ActiveMatch match = CreateReadyMatch();
            match.StopTurn(match.TurnId);
            return match;
        }

        public ActiveMatch CreateReadyMatch()
        {
            int winnerId = RegisteredWinner;
            int opponentId = RegisteredOpponent;
            if (guestWinner)
            {
                winnerId = GuestWinner;
            }
            if (guestOpponent)
            {
                opponentId = GuestOpponent;
            }
            LobbyDetails lobby = new LobbyDetails
            {
                LobbyId = LobbyIdentifier,
                Rules = new LobbyRules { RulesVariant = rulesVariant }
            };
            lobby.Players.Add(CreatePlayer(winnerId, guestWinner));
            lobby.Players.Add(CreatePlayer(opponentId, guestOpponent));
            ActiveMatch match = CreateMatch(lobby);
            match.UpdateConnection(winnerId, true);
            match.UpdateConnection(opponentId, true);
            SeedWinningProgress(match, winnerId);
            return match;
        }

        private void SeedWinningProgress(ActiveMatch match, int winnerId)
        {
            foreach (MatchColumn column in match.Columns.Where(column =>
                column.ColumnNumber == FirstClaim || (seedSecondClaim && column.ColumnNumber == SecondClaim)))
            {
                column.SaveProgress(winnerId, column.MaximumSteps);
            }
            for (int rollIndex = 0; rollIndex < WinningRollCount; rollIndex++)
            {
                match.ApplyRoll(ShortDice, match.RollId);
                match.ChoosePair(match.RollId, FirstPair);
            }
        }

        private ActiveMatch CreateMatch(LobbyDetails lobby)
        {
            if (manager != null)
            {
                return manager.CreateMatch(matchId, lobby);
            }
            return new ActiveMatch(matchId, lobby);
        }

        private LobbyPlayer CreatePlayer(int userId, bool isGuest)
        {
            return new LobbyPlayer
            {
                Player = new PlayerSummary
                {
                    UserId = userId,
                    IsGuest = isGuest
                }
            };
        }
    }
}
