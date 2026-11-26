using System;
using System.Collections.Generic;
using System.Linq;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Contracts.Match;
using RiskGame.Server.Services;
using Xunit;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class InactivityMatchBuilder
    {
        public const int FirstUserId = 41;
        public const int SecondUserId = 43;
        public const int ThreePlayers = 3;
        public const int TwoPlayers = 2;
        private const int ThirdUserId = 47;
        private const int MatchId = 1601;
        private const int LobbyId = 2601;
        private const int TurnSeconds = 30;
        private const int FinalSecond = 1;
        private const int FirstPair = 1;
        private static readonly int[] ParticipantIds = { FirstUserId, SecondUserId, ThirdUserId };
        private static readonly int[] PlayableDice = { 3, 3, 4, 4 };
        private static readonly int[] InvalidDice = { 0, 2, 3, 4 };
        private readonly Dictionary<int, string> tokens = new Dictionary<int, string>();

        public InactivityMatchBuilder(int participantCount = TwoPlayers)
        {
            Time = new ManualTimeProvider();
            ProfileSessionRegistry sessions = new ProfileSessionRegistry();
            Repository = new RecordingMatchRepository();
            Manager = new MatchSessionManager(sessions, Repository, Time);
            LobbyDetails lobby = new LobbyDetails
            {
                LobbyId = LobbyId,
                Rules = new LobbyRules { TurnDurationSeconds = TurnSeconds }
            };
            foreach (int userId in ParticipantIds.Take(participantCount))
            {
                tokens.Add(userId, sessions.Register(userId));
                lobby.Players.Add(new LobbyPlayer { Player = new PlayerSummary { UserId = userId } });
            }
            Match = Manager.CreateMatch(MatchId, lobby);
            foreach (MatchParticipant participant in Match.Participants)
            {
                Match.UpdateConnection(participant.UserId, true);
            }
        }

        public ManualTimeProvider Time { get; }

        public MatchSessionManager Manager { get; }

        public ActiveMatch Match { get; }

        public RecordingMatchRepository Repository { get; }

        public MatchParticipant FirstParticipant => Match.GetParticipant(FirstUserId);

        public void ExpireCurrentTurn()
        {
            Time.Advance(TimeSpan.FromSeconds(TurnSeconds));
            Manager.ExpireTurns();
        }

        public void RollCurrentTurn()
        {
            Match.ApplyRoll(PlayableDice, Match.RollId);
        }

        public void CompleteCurrentTurnWithRoll()
        {
            int userId = Match.CurrentTurnUserId;
            RollCurrentTurn();
            Match.ChoosePair(Match.RollId, FirstPair);
            Manager.StopTurn(Participant(userId), Match.TurnId);
        }

        public void ReturnToFirstPlayer()
        {
            while (!Match.IsFinished && Match.CurrentTurnUserId != FirstUserId)
            {
                CompleteCurrentTurnWithRoll();
            }
        }

        public void MissFirstTurnAndReturn()
        {
            ExpireCurrentTurn();
            ReturnToFirstPlayer();
        }

        public void AttemptInvalidRoll(bool outdatedRequest)
        {
            IReadOnlyList<int> dice = InvalidDice;
            long expectedRollId = Match.RollId;
            if (outdatedRequest)
            {
                dice = PlayableDice;
                expectedRollId++;
            }
            Record.Exception(() => Match.ApplyRoll(dice, expectedRollId));
        }

        public void ReconnectFirstPlayer()
        {
            Match.UpdateConnection(FirstUserId, false);
            Match.UpdateConnection(FirstUserId, true);
        }

        public void DisconnectFirstBeforeTimeout()
        {
            Time.Advance(TimeSpan.FromSeconds(TurnSeconds - FinalSecond));
            Match.UpdateConnection(FirstUserId, false);
            Time.Advance(TimeSpan.FromSeconds(FinalSecond));
            Manager.ExpireTurns();
        }

        public DisconnectedMatchBuilder.SubscriptionProbe SubscribeObserver()
        {
            return new DisconnectedMatchBuilder.SubscriptionProbe(Manager, Participant(SecondUserId));
        }

        public static BoardState ReadAfterAbandonment(DisconnectedMatchBuilder.SubscriptionProbe observer)
        {
            BoardState snapshot;
            do
            {
                snapshot = observer.Read();
            }
            while (!snapshot.Players.Single(player => player.UserId == FirstUserId).HasAbandoned);
            return snapshot;
        }

        private MatchParticipantSession Participant(int userId)
        {
            return new MatchParticipantSession(MatchId, userId, tokens[userId]);
        }
    }
}
