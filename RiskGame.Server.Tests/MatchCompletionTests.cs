using RiskGame.Exceptions;
using RiskGame.Server.Services;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class MatchCompletionTests
    {
        private const int WinnerId = 41;
        private const int OneSavedMatch = 1;
        private const int TwoAttempts = 2;
        private readonly RecordingMatchRepository repository = new RecordingMatchRepository();
        private readonly MatchSessionManager manager;
        private readonly MatchParticipantSession winnerSession;
        private readonly long winningTurnId;

        public MatchCompletionTests()
        {
            ProfileSessionRegistry sessions = new ProfileSessionRegistry();
            string token = sessions.Register(WinnerId);
            manager = new MatchSessionManager(sessions, repository);
            ActiveMatch match = new CompletedMatchBuilder().WithManager(manager).CreateReadyMatch();
            winnerSession = new MatchParticipantSession(match.MatchId, WinnerId, token);
            winningTurnId = match.TurnId;
        }

        [Fact]
        public void StopTurn_WinningMove_SavesMatchOnce()
        {
            manager.StopTurn(winnerSession, winningTurnId);

            int saves = repository.SavedMatches;

            Assert.Equal(OneSavedMatch, saves);
        }

        [Fact]
        public void StopTurn_RepeatedCompletion_DoesNotSaveTwice()
        {
            manager.StopTurn(winnerSession, winningTurnId);

            Record.Exception(() => manager.StopTurn(winnerSession, winningTurnId));

            Assert.Equal(OneSavedMatch, repository.SavedMatches);
        }

        [Fact]
        public void StopTurn_DatabaseFailure_PropagatesFailure()
        {
            repository.FailNextSave();

            System.Exception? rejectedException = Record.Exception(() =>
                manager.StopTurn(winnerSession, winningTurnId));

            Assert.IsType<DataOperationException>(rejectedException);
        }

        [Fact]
        public void StopTurn_RetryAfterFailure_SavesMatch()
        {
            repository.FailNextSave();
            Record.Exception(() => manager.StopTurn(winnerSession, winningTurnId));

            manager.StopTurn(winnerSession, winningTurnId);

            Assert.Equal(OneSavedMatch, repository.SavedMatches);
        }

        [Fact]
        public void StopTurn_RetryAfterFailure_RetriesRepository()
        {
            repository.FailNextSave();
            Record.Exception(() => manager.StopTurn(winnerSession, winningTurnId));

            manager.StopTurn(winnerSession, winningTurnId);

            Assert.Equal(TwoAttempts, repository.SaveAttempts);
        }
    }
}
