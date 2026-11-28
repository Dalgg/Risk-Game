extern alias Client;

using Avalonia.Media;
using RiskGame.Server.Tests.Builders;
using Xunit;
using AccountSessionConnection = Client::RiskGame.Services.AccountSessionConnection;
using Strings = Client::RiskGame.Resources.Strings;

namespace RiskGame.Server.Tests
{
    [CollectionDefinition(CollectionName, DisableParallelization = true)]
    public sealed class InboxIndicatorCollection
    {
        public const string CollectionName = "Inbox indicator client";
    }

    [Collection(InboxIndicatorCollection.CollectionName)]
    public sealed class InboxIndicatorTests
    {
        [Fact]
        public void RefreshInboxNotifications_PendingItem_HighlightsInbox()
        {
            ProfileTestApplication.Run(() =>
            {
                using InboxIndicatorBuilder setup = new InboxIndicatorBuilder();
                setup.Client.Enqueue(true);

                setup.RefreshAndWait(InboxIndicatorBuilder.FirstQuery);

                Assert.True(setup.ViewModel.HasPendingInboxNotifications);
            });
        }

        [Fact]
        public void RefreshInboxNotifications_EmptyInbox_ClearsPreviousHighlight()
        {
            ProfileTestApplication.Run(() =>
            {
                using InboxIndicatorBuilder setup = new InboxIndicatorBuilder();
                setup.Client.Enqueue(true);
                setup.RefreshAndWait(InboxIndicatorBuilder.FirstQuery);
                setup.Client.Enqueue(false);

                setup.RefreshAndWait(InboxIndicatorBuilder.SecondQuery);

                Assert.False(setup.ViewModel.HasPendingInboxNotifications);
            });
        }

        [Fact]
        public void RefreshInboxNotifications_Guest_DoesNotQueryServer()
        {
            ProfileTestApplication.Run(() =>
            {
                using InboxIndicatorBuilder setup = new InboxIndicatorBuilder();
                setup.UseGuest();

                setup.ViewModel.RefreshInboxNotifications();

                Assert.Equal(InboxIndicatorBuilder.NoQueries, setup.Client.Calls);
            });
        }

        [Fact]
        public void RefreshInboxNotifications_NoSession_DoesNotQueryServer()
        {
            ProfileTestApplication.Run(() =>
            {
                using InboxIndicatorBuilder setup = new InboxIndicatorBuilder();
                setup.ClearSession();

                setup.ViewModel.RefreshInboxNotifications();

                Assert.Equal(InboxIndicatorBuilder.NoQueries, setup.Client.Calls);
            });
        }

        [Fact]
        public void RefreshInboxNotifications_FailedQuery_PreservesExistingHighlight()
        {
            ProfileTestApplication.Run(() =>
            {
                using InboxIndicatorBuilder setup = new InboxIndicatorBuilder();
                setup.Client.Enqueue(true);
                setup.RefreshAndWait(InboxIndicatorBuilder.FirstQuery);
                setup.Client.EnqueueFailure();

                setup.RefreshAndWait(InboxIndicatorBuilder.SecondQuery);

                Assert.True(setup.ViewModel.HasPendingInboxNotifications);
            });
        }

        [Fact]
        public void RefreshInboxNotifications_FailedQuery_AllowsLaterRefresh()
        {
            ProfileTestApplication.Run(() =>
            {
                using InboxIndicatorBuilder setup = new InboxIndicatorBuilder();
                setup.Client.EnqueueFailure();
                setup.RefreshAndWait(InboxIndicatorBuilder.FirstQuery);
                setup.Client.Enqueue(true);

                setup.RefreshAndWait(InboxIndicatorBuilder.SecondQuery);

                Assert.True(setup.ViewModel.HasPendingInboxNotifications);
            });
        }

        [Fact]
        public void RefreshInboxNotifications_RepeatedDuringQuery_CoalescesRequests()
        {
            ProfileTestApplication.Run(() =>
            {
                using InboxIndicatorBuilder setup = new InboxIndicatorBuilder();
                setup.Client.EnqueueBlocked(false);
                setup.Client.Enqueue(true);
                setup.ViewModel.RefreshInboxNotifications();
                setup.Client.WaitUntilBlocked();

                setup.ViewModel.RefreshInboxNotifications();
                setup.ViewModel.RefreshInboxNotifications();
                setup.Client.ReleaseBlockedQuery();
                setup.WaitForIdle(InboxIndicatorBuilder.SecondQuery);

                Assert.Equal(InboxIndicatorBuilder.SecondQuery, setup.Client.Calls);
            });
        }

        [Fact]
        public void RefreshInboxNotifications_ChangeArrivesDuringQuery_AppliesFreshResult()
        {
            ProfileTestApplication.Run(() =>
            {
                using InboxIndicatorBuilder setup = new InboxIndicatorBuilder();
                setup.Client.EnqueueBlocked(false);
                setup.Client.Enqueue(true);
                setup.ViewModel.RefreshInboxNotifications();
                setup.Client.WaitUntilBlocked();

                setup.ViewModel.RefreshInboxNotifications();
                setup.Client.ReleaseBlockedQuery();
                setup.WaitForIdle(InboxIndicatorBuilder.SecondQuery);

                Assert.True(setup.ViewModel.HasPendingInboxNotifications);
            });
        }

        [Fact]
        public void RefreshInboxNotifications_PreviousAccountResult_DoesNotHighlightNewAccount()
        {
            ProfileTestApplication.Run(() =>
            {
                using InboxIndicatorBuilder setup = new InboxIndicatorBuilder();
                setup.Client.EnqueueBlocked(true);
                setup.ViewModel.RefreshInboxNotifications();
                setup.Client.WaitUntilBlocked();

                InboxIndicatorBuilder.ReplaceSession();
                setup.Client.ReleaseBlockedQuery();
                setup.WaitForIdle(InboxIndicatorBuilder.FirstQuery);

                Assert.False(setup.ViewModel.HasPendingInboxNotifications);
            });
        }

        [Fact]
        public void StopInboxNotifications_ActiveQuery_DiscardsItsResult()
        {
            ProfileTestApplication.Run(() =>
            {
                using InboxIndicatorBuilder setup = new InboxIndicatorBuilder();
                setup.Client.EnqueueBlocked(true);
                setup.ViewModel.RefreshInboxNotifications();
                setup.Client.WaitUntilBlocked();

                setup.ViewModel.StopInboxNotifications();
                setup.WaitForIdle(InboxIndicatorBuilder.FirstQuery);

                Assert.False(setup.ViewModel.HasPendingInboxNotifications);
            });
        }

        [Fact]
        public void StopInboxNotifications_ActiveQuery_CancelsClientOperation()
        {
            ProfileTestApplication.Run(() =>
            {
                using InboxIndicatorBuilder setup = new InboxIndicatorBuilder();
                setup.Client.EnqueueBlocked(true);
                setup.ViewModel.RefreshInboxNotifications();
                setup.Client.WaitUntilBlocked();

                setup.ViewModel.StopInboxNotifications();
                setup.WaitForIdle(InboxIndicatorBuilder.FirstQuery);

                Assert.True(setup.Client.CancellationObserved);
            });
        }

        [Fact]
        public void StopInboxNotifications_LaterActivation_DoesNotStartAnotherQuery()
        {
            ProfileTestApplication.Run(() =>
            {
                using InboxIndicatorBuilder setup = new InboxIndicatorBuilder();
                setup.ViewModel.StopInboxNotifications();

                setup.ViewModel.RefreshInboxNotifications();

                Assert.Equal(InboxIndicatorBuilder.NoQueries, setup.Client.Calls);
            });
        }

        [Theory]
        [InlineData(false, InboxIndicatorBuilder.NormalColor)]
        [InlineData(true, InboxIndicatorBuilder.PendingColor)]
        public void InboxButton_PendingState_UsesCorrespondingBackground(bool pending, string expectedColor)
        {
            ProfileTestApplication.Run(() =>
            {
                using InboxIndicatorBuilder setup = new InboxIndicatorBuilder();
                setup.Client.DefaultPending = pending;

                setup.CreateWindow();

                Assert.Equal(Color.Parse(expectedColor), setup.ReadButtonColor());
            });
        }

        [Fact]
        public void InboxButton_PendingItem_UsesHighlightClass()
        {
            ProfileTestApplication.Run(() =>
            {
                using InboxIndicatorBuilder setup = new InboxIndicatorBuilder();
                setup.Client.DefaultPending = true;

                setup.CreateWindow();

                Assert.Contains(InboxIndicatorBuilder.PendingClass, setup.InboxButton.Classes);
            });
        }

        [Fact]
        public void InboxDescription_PendingItem_UsesLocalizedNotice()
        {
            ProfileTestApplication.Run(() =>
            {
                using InboxIndicatorBuilder setup = new InboxIndicatorBuilder();
                setup.Client.Enqueue(true);

                setup.RefreshAndWait(InboxIndicatorBuilder.FirstQuery);

                Assert.Equal(Strings.InboxPendingNotifications, setup.ViewModel.InboxButtonDescription);
            });
        }

        [Fact]
        public void NotifyInboxChanged_CurrentAccountConnection_RaisesEvent()
        {
            ProfileTestApplication.Run(() =>
            {
                using InboxIndicatorBuilder setup = new InboxIndicatorBuilder();
                using AccountSessionConnection connection = setup.AttachLocalConnection();

                InboxIndicatorBuilder.NotifyConnection(connection);

                Assert.Equal(InboxIndicatorBuilder.FirstQuery, setup.InboxEventCount);
            });
        }

        [Fact]
        public void NotifyInboxChanged_PreviousAccountConnection_DoesNotRaiseEvent()
        {
            ProfileTestApplication.Run(() =>
            {
                using InboxIndicatorBuilder setup = new InboxIndicatorBuilder();
                using AccountSessionConnection connection = setup.AttachLocalConnection();
                InboxIndicatorBuilder.ReplaceSession();

                InboxIndicatorBuilder.NotifyConnection(connection);

                Assert.Equal(InboxIndicatorBuilder.NoQueries, setup.InboxEventCount);
            });
        }
    }
}
