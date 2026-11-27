using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using RiskGame.Contracts.Auth;
using RiskGame.Server.Services;
using Xunit;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class SessionConnectionBuilder : IDisposable
    {
        private const int ConnectionCheckMilliseconds = 1000;
        private const int CheckMarginMilliseconds = 100;
        private readonly List<ConnectionProbe> connections = new List<ConnectionProbe>();

        public SessionConnectionBuilder()
        {
            Session = new SingleSessionBuilder();
            Manager = new SessionConnectionManager(Session.Registry);
        }

        public SingleSessionBuilder Session { get; }

        public SessionConnectionManager Manager { get; }

        public ConnectionProbe Subscribe()
        {
            ConnectionProbe connection = StartForUser(SingleSessionBuilder.UserId);
            connection.ReadInitialState();
            return connection;
        }

        public ConnectionProbe StartForUser(int userId)
        {
            SessionRequest request = new SessionRequest { UserId = userId, SessionToken = Session.Token };
            ConnectionProbe connection = new ConnectionProbe(Manager, request);
            connections.Add(connection);
            return connection;
        }

        public static void WaitThroughConnectionCheck()
        {
            Thread.Sleep(ConnectionCheckMilliseconds + CheckMarginMilliseconds);
        }

        public void Dispose()
        {
            foreach (ConnectionProbe connection in connections)
            {
                connection.Dispose();
            }
        }

        public sealed class ConnectionProbe : IDisposable
        {
            private const int WaitSeconds = 5;
            private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
            private readonly RecordingStream stream = new RecordingStream();
            private readonly Task<Exception?> subscription;

            public ConnectionProbe(SessionConnectionManager manager, SessionRequest request)
            {
                subscription = Task.Run<Exception?>(() => Record.Exception(() =>
                    manager.Subscribe(request, stream, cancellation.Token).GetAwaiter().GetResult()));
            }

            public SessionState InitialState { get; private set; } = new SessionState();

            public int MessageCount => stream.MessageCount;

            public bool IsCompleted => subscription.IsCompleted;

            public void ReadInitialState()
            {
                InitialState = stream.Read();
            }

            public Exception? WaitForCompletion()
            {
                return subscription.WaitAsync(TimeSpan.FromSeconds(WaitSeconds)).GetAwaiter().GetResult();
            }

            public bool CancelAndWait()
            {
                cancellation.Cancel();
                return WaitForCompletion() == null;
            }

            public void Dispose()
            {
                cancellation.Cancel();
                WaitForCompletion();
                cancellation.Dispose();
            }
        }

        private sealed class RecordingStream : IServerStreamWriter<SessionState>
        {
            private const int WaitSeconds = 5;
            private readonly BlockingCollection<SessionState> states = new BlockingCollection<SessionState>();
            private int messageCount;

            public WriteOptions? WriteOptions { get; set; }

            public int MessageCount => Volatile.Read(ref messageCount);

            public Task WriteAsync(SessionState message)
            {
                Interlocked.Increment(ref messageCount);
                states.Add(message);
                return Task.CompletedTask;
            }

            public SessionState Read()
            {
                using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(WaitSeconds));
                return states.Take(timeout.Token);
            }
        }
    }
}
