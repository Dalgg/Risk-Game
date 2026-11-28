using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using RiskGame.Contracts.Auth;

namespace RiskGame.Services
{
    public sealed class AccountSessionConnection : IDisposable
    {
        private const int RecoverySeconds = 30;
        private const int RetryDelaySeconds = 2;
        private const int FirstStateTimeoutSeconds = 10;
        private readonly object connectionLock = new object();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly SessionUser session;
        private readonly Action<AccountSessionConnection> expireSession;
        private readonly Action<AccountSessionConnection>? inboxChanged;
        private long? recoveryStartedTimestamp;
        private bool stopped;

        public AccountSessionConnection(SessionUser session, Action<AccountSessionConnection> expireSession)
            : this(session, expireSession, null)
        {
        }

        public AccountSessionConnection(SessionUser session, Action<AccountSessionConnection> expireSession,
            Action<AccountSessionConnection>? inboxChanged)
        {
            this.session = session;
            this.expireSession = expireSession;
            this.inboxChanged = inboxChanged;
        }

        public SessionUser Session => session;

        public SessionExpiredEventArgs Expiration { get; private set; } = new SessionExpiredEventArgs();

        public void Start()
        {
            Task.Run(MonitorSession);
        }

        public void Stop()
        {
            lock (connectionLock)
            {
                if (!stopped)
                {
                    cancellation.Cancel();
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void MonitorSession()
        {
            CancellationToken cancellationToken = cancellation.Token;
            bool valid = true;
            try
            {
                while (valid && !cancellationToken.IsCancellationRequested)
                {
                    valid = TryReadSession(cancellationToken);
                    if (valid && !cancellationToken.IsCancellationRequested)
                    {
                        valid = WaitBeforeRetry(cancellationToken);
                    }
                }
            }
            finally
            {
                bool notifyExpired;
                lock (connectionLock)
                {
                    notifyExpired = !cancellationToken.IsCancellationRequested;
                    stopped = true;
                    cancellation.Cancel();
                    cancellation.Dispose();
                }
                if (notifyExpired)
                {
                    expireSession(this);
                }
            }
        }

        private bool TryReadSession(CancellationToken cancellationToken)
        {
            bool valid = true;
            try
            {
                valid = ReadSession(cancellationToken);
            }
            catch (RpcException rpcException)
            {
                valid = HandleRpcFailure(rpcException, cancellationToken);
            }
            catch (OperationCanceledException canceledException)
            {
                LogUnexpectedFailure(canceledException,
                    "The account session stream was interrupted for user {0}.", cancellationToken);
            }
            catch (Exception exception)
            {
                LogUnexpectedFailure(exception,
                    "Could not monitor the account session for user {0}.", cancellationToken);
            }
            return valid;
        }

        private bool HandleRpcFailure(RpcException rpcException, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return true;
            }
            if (rpcException.StatusCode == StatusCode.Unauthenticated)
            {
                return false;
            }
            JsonLogger.Error(rpcException, "The account session stream failed for user {0}.", session.IdUser);
            return true;
        }

        private void LogUnexpectedFailure(Exception exception, string logTemplate, CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                JsonLogger.Error(exception, logTemplate, session.IdUser);
            }
        }

        private bool ReadSession(CancellationToken cancellationToken)
        {
            GrpcChannel channel = GrpcChannelProvider.GetChannel();
            AuthGrpc.AuthGrpcClient client = new AuthGrpc.AuthGrpcClient(channel);
            SessionRequest request = new SessionRequest
            {
                UserId = session.IdUser, SessionToken = session.SessionToken
            };
            using AsyncServerStreamingCall<SessionState> call = client.SubscribeSession(request,
                cancellationToken: cancellationToken);
            bool hasNext = call.ResponseStream.MoveNext(cancellationToken)
                .WaitAsync(GetFirstStateTimeout(), cancellationToken).GetAwaiter().GetResult();
            bool valid = true;
            while (hasNext && valid)
            {
                SessionState state = call.ResponseStream.Current;
                valid = state.IsAuthenticated;
                if (!valid)
                {
                    Expiration = new SessionExpiredEventArgs(state.IsSuspended, state.BannedUntilUnixMilliseconds);
                }
                if (valid)
                {
                    if (state.InboxChanged && inboxChanged != null)
                    {
                        inboxChanged(this);
                    }
                    recoveryStartedTimestamp = null;
                    hasNext = call.ResponseStream.MoveNext(cancellationToken).GetAwaiter().GetResult();
                }
            }
            if (valid && !cancellationToken.IsCancellationRequested)
            {
                throw new IOException("The account session stream ended before logout.");
            }
            return valid;
        }

        private TimeSpan GetFirstStateTimeout()
        {
            TimeSpan timeout = TimeSpan.FromSeconds(FirstStateTimeoutSeconds);
            if (recoveryStartedTimestamp.HasValue)
            {
                TimeSpan remaining = GetRecoveryRemaining();
                if (remaining < timeout)
                {
                    timeout = remaining;
                }
            }
            return timeout;
        }

        private bool WaitBeforeRetry(CancellationToken cancellationToken)
        {
            if (!recoveryStartedTimestamp.HasValue)
            {
                recoveryStartedTimestamp = Stopwatch.GetTimestamp();
            }
            TimeSpan remaining = GetRecoveryRemaining();
            bool valid = remaining > TimeSpan.Zero;
            if (valid)
            {
                TimeSpan delay = TimeSpan.FromSeconds(RetryDelaySeconds);
                if (remaining < delay)
                {
                    delay = remaining;
                }
                cancellationToken.WaitHandle.WaitOne(delay);
                valid = GetRecoveryRemaining() > TimeSpan.Zero;
            }
            return valid;
        }

        private TimeSpan GetRecoveryRemaining()
        {
            TimeSpan remaining = TimeSpan.FromSeconds(RecoverySeconds);
            if (recoveryStartedTimestamp.HasValue)
            {
                remaining -= Stopwatch.GetElapsedTime(recoveryStartedTimestamp.Value);
                if (remaining < TimeSpan.Zero)
                {
                    remaining = TimeSpan.Zero;
                }
            }
            return remaining;
        }
    }
}
