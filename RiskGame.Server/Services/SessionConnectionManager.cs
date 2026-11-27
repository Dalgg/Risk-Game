using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using RiskGame.Contracts.Auth;

namespace RiskGame.Server.Services
{
    public sealed class SessionConnectionManager
    {
        private const int ConnectionCheckSeconds = 1;
        private readonly ProfileSessionRegistry sessions;

        public SessionConnectionManager(ProfileSessionRegistry sessions)
        {
            this.sessions = sessions;
        }

        public Task Subscribe(SessionRequest request, IServerStreamWriter<SessionState> stream,
            CancellationToken cancellationToken)
        {
            string connectionId = Guid.NewGuid().ToString();
            try
            {
                sessions.AttachSession(request.UserId, request.SessionToken, connectionId);
                stream.WriteAsync(new SessionState { IsAuthenticated = true }).GetAwaiter().GetResult();
                WaitForConnectionEnd(request.SessionToken, connectionId, cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    sessions.ValidateUser(request.UserId, request.SessionToken);
                }
                return Task.CompletedTask;
            }
            finally
            {
                sessions.DetachSession(request.SessionToken, connectionId);
            }
        }

        private void WaitForConnectionEnd(string token, string connectionId, CancellationToken cancellationToken)
        {
            TimeSpan interval = TimeSpan.FromSeconds(ConnectionCheckSeconds);
            bool connected = true;
            while (connected && !cancellationToken.WaitHandle.WaitOne(interval))
            {
                connected = sessions.IsConnectionCurrent(token, connectionId);
            }
        }
    }
}
