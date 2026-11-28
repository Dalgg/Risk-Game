using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using RiskGame.Contracts.Auth;
using RiskGame.Exceptions;

namespace RiskGame.Server.Services
{
    public sealed class SessionConnectionManager
    {
        private const int ConnectionCheckSeconds = 1;
        private readonly ProfileSessionRegistry sessions;
        private readonly LobbySessionManager? lobbies;

        public SessionConnectionManager(ProfileSessionRegistry sessions)
            : this(sessions, null)
        {
        }

        public SessionConnectionManager(ProfileSessionRegistry sessions, LobbySessionManager? lobbies)
        {
            this.sessions = sessions;
            this.lobbies = lobbies;
        }

        public string Register(int userId)
        {
            string token = sessions.Register(userId);
            BroadcastPresence();
            return token;
        }

        public void Logout(string sessionToken)
        {
            sessions.RemoveSession(sessionToken);
            BroadcastPresence();
        }

        private void BroadcastPresence()
        {
            if (lobbies != null)
            {
                lobbies.BroadcastPresenceUpdate();
            }
        }

        public Task Subscribe(AccountSessionIdentity identity, IServerStreamWriter<SessionState> stream,
            CancellationToken cancellationToken)
        {
            Task subscription = Task.CompletedTask;
            try
            {
                subscription = ReadSession(identity, stream, cancellationToken);
            }
            catch (AccountSuspendedException accountSuspendedException)
            {
                SessionState state = new SessionState
                {
                    IsSuspended = true,
                    BannedUntilUnixMilliseconds = new DateTimeOffset(accountSuspendedException.BannedUntilUtc)
                        .ToUnixTimeMilliseconds()
                };
                stream.WriteAsync(state).GetAwaiter().GetResult();
            }
            return subscription;
        }

        private Task ReadSession(AccountSessionIdentity identity, IServerStreamWriter<SessionState> stream,
            CancellationToken cancellationToken)
        {
            string connectionId = Guid.NewGuid().ToString();
            try
            {
                sessions.AttachSession(identity.UserId, identity.SessionToken, connectionId);
                long inboxRevision = sessions.GetInboxRevision(identity.SessionToken);
                stream.WriteAsync(new SessionState { IsAuthenticated = true, InboxChanged = true })
                    .GetAwaiter().GetResult();
                TimeSpan interval = TimeSpan.FromSeconds(ConnectionCheckSeconds);
                bool connected = true;
                while (connected && !cancellationToken.WaitHandle.WaitOne(interval))
                {
                    connected = sessions.IsConnectionCurrent(identity.SessionToken, connectionId);
                    if (connected)
                    {
                        inboxRevision = SendInboxChange(identity, stream, inboxRevision);
                    }
                }
                if (!cancellationToken.IsCancellationRequested)
                {
                    sessions.ValidateUser(identity.UserId, identity.SessionToken);
                }
                return Task.CompletedTask;
            }
            finally
            {
                sessions.DetachSession(identity.SessionToken, connectionId);
            }
        }

        private long SendInboxChange(AccountSessionIdentity identity, IServerStreamWriter<SessionState> stream,
            long previousRevision)
        {
            long currentRevision = sessions.GetInboxRevision(identity.SessionToken);
            if (currentRevision != previousRevision)
            {
                stream.WriteAsync(new SessionState { IsAuthenticated = true, InboxChanged = true })
                    .GetAwaiter().GetResult();
            }
            return currentRevision;
        }
    }
}
