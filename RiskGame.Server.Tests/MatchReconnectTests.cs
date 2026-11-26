using System;
using Grpc.Core;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    [CollectionDefinition(CollectionName, DisableParallelization = true)]
    public sealed class MatchReconnectCollection
    {
        public const string CollectionName = "Match reconnection";
    }

    [Collection(MatchReconnectCollection.CollectionName)]
    public sealed class MatchReconnectTests
    {
        private const int ShortTimeoutMilliseconds = 100;

        [Fact]
        public void Subscribe_AlreadyConnecting_RejectsDuplicate()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchReconnectBuilder setup = new MatchReconnectBuilder();
                setup.Subscribe();
                setup.WaitForAttempts(MatchReconnectBuilder.SingleAttempt);

                bool accepted = setup.Subscribe();

                Assert.False(accepted);
            });
        }

        [Fact]
        public void Reconnect_RepeatedClicks_OpensOnlyOneStream()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchReconnectBuilder setup = new MatchReconnectBuilder();
                setup.ShowWindow();

                setup.RepeatReconnectClicks();

                Assert.Equal(MatchReconnectBuilder.SingleAttempt, setup.AttemptCount);
            });
        }

        [Fact]
        public void Reconnect_WaitingForSnapshot_DisablesButton()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchReconnectBuilder setup = new MatchReconnectBuilder();

                setup.ShowWindow();

                Assert.False(setup.ReconnectButton.IsEnabled);
            });
        }

        [Fact]
        public void Reconnect_FirstSnapshot_KeepsButtonDisabled()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchReconnectBuilder setup = new MatchReconnectBuilder();
                setup.ShowWindow();

                setup.LatestStream.SendSnapshot(MatchReconnectBuilder.InitialRevision);
                setup.WaitForSynchronized();

                Assert.False(setup.ReconnectButton.IsEnabled);
            });
        }

        [Fact]
        public void Reconnect_ConnectedRepeatedClicks_KeepsCurrentStream()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchReconnectBuilder setup = new MatchReconnectBuilder();
                setup.ShowWindow();
                setup.LatestStream.SendSnapshot(MatchReconnectBuilder.InitialRevision);
                setup.WaitForSynchronized();

                setup.RepeatReconnectClicks();

                Assert.Equal(MatchReconnectBuilder.SingleAttempt, setup.AttemptCount);
            });
        }

        [Fact]
        public void Subscribe_AlreadyConnected_RejectsDuplicate()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchReconnectBuilder setup = new MatchReconnectBuilder();
                setup.Subscribe();
                setup.WaitForAttempts(MatchReconnectBuilder.SingleAttempt);
                setup.LatestStream.SendSnapshot(MatchReconnectBuilder.InitialRevision);
                MatchReconnectBuilder.WaitUntil(() =>
                    setup.Callback.LatestRevision == MatchReconnectBuilder.InitialRevision);

                bool accepted = setup.Subscribe();

                Assert.False(accepted);
            });
        }

        [Fact]
        public void Reconnect_StreamError_AllowsNewAttempt()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchReconnectBuilder setup = new MatchReconnectBuilder();
                setup.ShowWindow();
                setup.LatestStream.SendSnapshot(MatchReconnectBuilder.InitialRevision);
                setup.WaitForSynchronized();
                setup.LatestStream.Fail();
                setup.WaitForButtonEnabled();

                setup.ClickReconnect();
                setup.WaitForAttempts(MatchReconnectBuilder.TwoAttempts);

                Assert.Equal(MatchReconnectBuilder.TwoAttempts, setup.AttemptCount);
            });
        }

        [Fact]
        public void Reconnect_InitialTimeout_AllowsNewAttempt()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchReconnectBuilder setup = new MatchReconnectBuilder(
                    TimeSpan.FromMilliseconds(ShortTimeoutMilliseconds));
                setup.ShowWindow();
                setup.WaitForButtonEnabled();

                setup.ClickReconnect();
                setup.WaitForAttempts(MatchReconnectBuilder.TwoAttempts);

                Assert.Equal(MatchReconnectBuilder.TwoAttempts, setup.AttemptCount);
            });
        }

        [Fact]
        public void Subscribe_PreviousStreamCloses_DoesNotCancelReplacement()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchReconnectBuilder setup = new MatchReconnectBuilder();
                setup.Subscribe();
                setup.WaitForAttempts(MatchReconnectBuilder.SingleAttempt);
                MatchReconnectBuilder.FakeMatchStream previous = setup.LatestStream;
                previous.SendSnapshot(MatchReconnectBuilder.InitialRevision);
                MatchReconnectBuilder.WaitUntil(() =>
                    setup.Callback.LatestRevision == MatchReconnectBuilder.InitialRevision);

                setup.ViewModel.StopSubscription();
                setup.Subscribe();
                setup.WaitForAttempts(MatchReconnectBuilder.TwoAttempts);
                MatchReconnectBuilder.WaitUntil(() => previous.IsDisposed);
                setup.LatestStream.SendSnapshot(MatchReconnectBuilder.UpdatedRevision);
                MatchReconnectBuilder.WaitUntil(() =>
                    setup.Callback.LatestRevision == MatchReconnectBuilder.UpdatedRevision);

                Assert.Equal(MatchReconnectBuilder.UpdatedRevision, setup.Callback.LatestRevision);
            });
        }

        [Theory]
        [InlineData(StatusCode.Unavailable)]
        [InlineData(StatusCode.DeadlineExceeded)]
        public void Action_CommunicationFailure_EnablesReconnect(StatusCode statusCode)
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchReconnectBuilder setup = new MatchReconnectBuilder();
                setup.ShowWindow();
                setup.LatestStream.SendSnapshot(MatchReconnectBuilder.InitialRevision);
                setup.WaitForSynchronized();

                setup.FailAction(statusCode);

                Assert.True(setup.ReconnectButton.IsEnabled);
            });
        }

        [Fact]
        public void Action_CommunicationFailure_CancelsPreviousStream()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchReconnectBuilder setup = new MatchReconnectBuilder();
                setup.ShowWindow();
                MatchReconnectBuilder.FakeMatchStream previous = setup.LatestStream;
                previous.SendSnapshot(MatchReconnectBuilder.InitialRevision);
                setup.WaitForSynchronized();

                setup.CreateActionError(StatusCode.Unavailable);
                MatchReconnectBuilder.WaitUntil(() => previous.IsDisposed);

                Assert.True(previous.IsDisposed);
            });
        }

        [Fact]
        public void Action_RuleRejection_PreservesModelSubscription()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchReconnectBuilder setup = new MatchReconnectBuilder();
                setup.ShowWindow();
                setup.LatestStream.SendSnapshot(MatchReconnectBuilder.InitialRevision);
                setup.WaitForSynchronized();
                setup.FailAction(StatusCode.FailedPrecondition);

                bool accepted = setup.Subscribe();

                Assert.False(accepted);
            });
        }

        [Fact]
        public void Action_RuleRejection_KeepsReconnectDisabled()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchReconnectBuilder setup = new MatchReconnectBuilder();
                setup.ShowWindow();
                setup.LatestStream.SendSnapshot(MatchReconnectBuilder.InitialRevision);
                setup.WaitForSynchronized();

                setup.FailAction(StatusCode.FailedPrecondition);

                Assert.False(setup.ReconnectButton.IsEnabled);
            });
        }

        [Fact]
        public void Action_QueuedOldSnapshot_DoesNotRestoreConnectionAfterFailure()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchReconnectBuilder setup = new MatchReconnectBuilder();
                setup.ShowWindow();
                setup.LatestStream.SendSnapshot(MatchReconnectBuilder.InitialRevision);
                setup.WaitForSynchronized();
                setup.Window.MatchUpdated(MatchReconnectBuilder.CreateSnapshot(MatchReconnectBuilder.UpdatedRevision));

                setup.FailAction(StatusCode.Unavailable);

                Assert.True(setup.ReconnectButton.IsEnabled);
            });
        }

        [Fact]
        public void Action_CommunicationFailure_DoesNotReconnectAutomatically()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchReconnectBuilder setup = new MatchReconnectBuilder();
                setup.ShowWindow();
                setup.LatestStream.SendSnapshot(MatchReconnectBuilder.InitialRevision);
                setup.WaitForSynchronized();

                setup.FailAction(StatusCode.Unavailable);
                MatchReconnectBuilder.WaitUntil(() => setup.LatestStream.IsDisposed);

                Assert.Equal(MatchReconnectBuilder.SingleAttempt, setup.AttemptCount);
            });
        }

        [Fact]
        public void Action_CommunicationFailure_RequiresExplicitReconnectClick()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchReconnectBuilder setup = new MatchReconnectBuilder();
                setup.ShowWindow();
                setup.LatestStream.SendSnapshot(MatchReconnectBuilder.InitialRevision);
                setup.WaitForSynchronized();
                setup.FailAction(StatusCode.Unavailable);

                setup.ClickReconnect();
                setup.WaitForAttempts(MatchReconnectBuilder.TwoAttempts);

                Assert.Equal(MatchReconnectBuilder.TwoAttempts, setup.AttemptCount);
            });
        }
    }
}
