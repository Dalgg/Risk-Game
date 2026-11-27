using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public sealed class SessionExpirationService : IHostedService, IDisposable
    {
        private const int CheckIntervalSeconds = 1;
        private readonly ProfileSessionRegistry sessions;
        private readonly LobbySessionManager lobbies;
        private readonly TimeProvider timeProvider;
        private ITimer? expirationTimer;

        public SessionExpirationService(ProfileSessionRegistry sessions, LobbySessionManager lobbies,
            TimeProvider timeProvider)
        {
            this.sessions = sessions;
            this.lobbies = lobbies;
            this.timeProvider = timeProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            TimeSpan interval = TimeSpan.FromSeconds(CheckIntervalSeconds);
            expirationTimer = timeProvider.CreateTimer(ExpireSessions, null, interval, interval);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Dispose();
            return Task.CompletedTask;
        }

        private void ExpireSessions(object? state)
        {
            try
            {
                if (sessions.ExpireSessions())
                {
                    lobbies.BroadcastPresenceUpdate();
                }
            }
            catch (Exception exception)
            {
                JsonLogger.Error(exception, "Could not expire disconnected account sessions.");
            }
        }

        public void Dispose()
        {
            if (expirationTimer != null)
            {
                expirationTimer.Dispose();
                expirationTimer = null;
            }
        }
    }
}
