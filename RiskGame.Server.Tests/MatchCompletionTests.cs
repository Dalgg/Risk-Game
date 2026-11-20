using RiskGame.Contracts.Match;
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
        private readonly StopTurnRequest request;

        public MatchCompletionTests()
        {
            ProfileSessionRegistry sessions = new ProfileSessionRegistry();
            string token = sessions.Register(WinnerId);
            manager = new MatchSessionManager(sessions, repository);
            ActiveMatch match = new CompletedMatchBuilder().WithManager(manager).CreateReadyMatch();
            request = new StopTurnRequest
            {
                MatchId = match.MatchId,
                UserId = WinnerId,
                SessionToken = token,
                TurnId = match.TurnId
            };
        }

        [Fact]
        public void StopTurn_WinningMove_SavesMatchOnce()
        {
            manager.StopTurn(request);

            int saves = repository.SavedMatches;

            Assert.Equal(OneSavedMatch, saves);
        }

        [Fact]
        public void StopTurn_RepeatedCompletion_DoesNotSaveTwice()
        {
            manager.StopTurn(request);

            Record.Exception(() => manager.StopTurn(request));

            Assert.Equal(OneSavedMatch, repository.SavedMatches);
        }

        [Fact]
        public void StopTurn_DatabaseFailure_PropagatesFailure()
        {
            repository.FailNextSave();

            System.Exception? rejectedException = Record.Exception(() => manager.StopTurn(request));

            Assert.IsType<DataOperationException>(rejectedException);
        }

        [Fact]
        public void StopTurn_RetryAfterFailure_SavesMatch()
        {
            repository.FailNextSave();
            Record.Exception(() => manager.StopTurn(request));

            manager.StopTurn(request);

            Assert.Equal(OneSavedMatch, repository.SavedMatches);
        }

        [Fact]
        public void StopTurn_RetryAfterFailure_RetriesRepository()
        {
            repository.FailNextSave();
            Record.Exception(() => manager.StopTurn(request));

            manager.StopTurn(request);

            Assert.Equal(TwoAttempts, repository.SaveAttempts);
        }
    }
}
