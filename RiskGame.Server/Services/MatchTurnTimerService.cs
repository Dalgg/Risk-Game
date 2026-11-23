using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public sealed class MatchTurnTimerService : IHostedService, IDisposable
    {
        private const int CheckIntervalMilliseconds = 250;
        private readonly MatchSessionManager matchSessionManager;
        private readonly TimeProvider timeProvider;
        private readonly LobbySessionManager? lobbySessionManager;
        private ITimer? turnTimer;

        public MatchTurnTimerService(MatchSessionManager matchSessionManager, TimeProvider timeProvider)
            : this(matchSessionManager, timeProvider, null)
        {
        }

        public MatchTurnTimerService(MatchSessionManager matchSessionManager, TimeProvider timeProvider,
            LobbySessionManager? lobbySessionManager)
        {
            this.matchSessionManager = matchSessionManager;
            this.timeProvider = timeProvider;
            this.lobbySessionManager = lobbySessionManager;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            TimeSpan interval = TimeSpan.FromMilliseconds(CheckIntervalMilliseconds);
            turnTimer = timeProvider.CreateTimer(CheckTurns, null, interval, interval);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Dispose();
            return Task.CompletedTask;
        }

        private void CheckTurns(object? state)
        {
            try
            {
                matchSessionManager.ExpireTurns();
                if (lobbySessionManager != null)
                {
                    foreach (AbandonedMatchPlayer abandoned in matchSessionManager.TakeAbandonedParticipants())
                    {
                        lobbySessionManager.RemoveAbandonedParticipant(abandoned);
                    }
                }
            }
            catch (Exception exception)
            {
                JsonLogger.Error(exception, "Could not process expired match turns.");
            }
        }

        public void Dispose()
        {
            if (turnTimer != null)
            {
                turnTimer.Dispose();
                turnTimer = null;
            }
        }
    }
}
