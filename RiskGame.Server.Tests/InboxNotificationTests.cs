using System;
using RiskGame.Exceptions;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class InboxNotificationTests
    {
        private const int InitialMessageCount = 1;
        private const int InitialAndChangeMessages = 2;
        private const int SuspensionHours = 24;
        private const long NoChanges = 0;
        private const long OneChange = 1;
        private const long TwoChanges = 2;
        private const string MissingToken = "missing-inbox-session";

        [Fact]
        public void Subscribe_InitialState_RequestsInboxSynchronization()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();

            SessionConnectionBuilder.ConnectionProbe connection = setup.Subscribe();

            Assert.True(connection.InitialState.InboxChanged);
        }

        [Fact]
        public void Subscribe_UnchangedInbox_SendsOnlyInitialState()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            SessionConnectionBuilder.ConnectionProbe connection = setup.Subscribe();

            SessionConnectionBuilder.WaitThroughConnectionCheck();

            Assert.Equal(InitialMessageCount, connection.MessageCount);
        }

        [Fact]
        public void MarkInboxChanged_ConnectedRecipient_SendsInboxChange()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            SessionConnectionBuilder.ConnectionProbe connection = setup.Subscribe();

            setup.Session.Registry.MarkInboxChanged(SingleSessionBuilder.UserId);
            connection.ReadInitialState();

            Assert.True(connection.InitialState.InboxChanged);
        }

        [Fact]
        public void MarkInboxChanged_ConnectedRecipient_PreservesAuthentication()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            SessionConnectionBuilder.ConnectionProbe connection = setup.Subscribe();

            setup.Session.Registry.MarkInboxChanged(SingleSessionBuilder.UserId);
            connection.ReadInitialState();

            Assert.True(connection.InitialState.IsAuthenticated);
        }

        [Fact]
        public void MarkInboxChanged_ConnectedRecipient_KeepsStreamOpen()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            SessionConnectionBuilder.ConnectionProbe connection = setup.Subscribe();

            setup.Session.Registry.MarkInboxChanged(SingleSessionBuilder.UserId);
            connection.ReadInitialState();

            Assert.False(connection.IsCompleted);
        }

        [Fact]
        public void MarkInboxChanged_ConsumedChange_DoesNotRepeatNotification()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            SessionConnectionBuilder.ConnectionProbe connection = setup.Subscribe();
            setup.Session.Registry.MarkInboxChanged(SingleSessionBuilder.UserId);
            connection.ReadInitialState();

            SessionConnectionBuilder.WaitThroughConnectionCheck();

            Assert.Equal(InitialAndChangeMessages, connection.MessageCount);
        }

        [Fact]
        public void MarkInboxChanged_OtherAccount_DoesNotNotifyConnectedRecipient()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            setup.Session.Registry.Register(SingleSessionBuilder.OtherUserId);
            SessionConnectionBuilder.ConnectionProbe connection = setup.Subscribe();

            setup.Session.Registry.MarkInboxChanged(SingleSessionBuilder.OtherUserId);
            SessionConnectionBuilder.WaitThroughConnectionCheck();

            Assert.Equal(InitialMessageCount, connection.MessageCount);
        }

        [Fact]
        public void GetInboxRevision_KnownRecipient_TracksPendingChange()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();

            setup.Registry.MarkInboxChanged(SingleSessionBuilder.UserId);

            Assert.Equal(OneChange, setup.Registry.GetInboxRevision(setup.Token));
        }

        [Fact]
        public void GetInboxRevision_TwoChanges_AdvancesTwice()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();

            setup.Registry.MarkInboxChanged(SingleSessionBuilder.UserId);
            setup.Registry.MarkInboxChanged(SingleSessionBuilder.UserId);

            Assert.Equal(TwoChanges, setup.Registry.GetInboxRevision(setup.Token));
        }

        [Fact]
        public void GetInboxRevision_UnknownToken_ReturnsNoChanges()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();

            Assert.Equal(NoChanges, setup.Registry.GetInboxRevision(MissingToken));
        }

        [Fact]
        public void MarkInboxChanged_GuestIdentifier_DoesNotChangeGuestSession()
        {
            SingleSessionBuilder setup = new SingleSessionBuilder();
            string guestToken = setup.Registry.Register(SingleSessionBuilder.GuestUserId);

            setup.Registry.MarkInboxChanged(SingleSessionBuilder.GuestUserId);

            Assert.Equal(NoChanges, setup.Registry.GetInboxRevision(guestToken));
        }

        [Fact]
        public void Subscribe_ReconnectedAfterInboxChange_RequestsSynchronization()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            SessionConnectionBuilder.ConnectionProbe previous = setup.Subscribe();
            previous.CancelAndWait();
            setup.Session.Registry.MarkInboxChanged(SingleSessionBuilder.UserId);

            SessionConnectionBuilder.ConnectionProbe replacement = setup.Subscribe();

            Assert.True(replacement.InitialState.InboxChanged);
        }

        [Fact]
        public void Subscribe_InboxChangeThenSuspension_PreservesSuspensionNotice()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            SessionConnectionBuilder.ConnectionProbe connection = setup.Subscribe();
            setup.Session.Registry.MarkInboxChanged(SingleSessionBuilder.UserId);
            connection.ReadInitialState();

            setup.Session.Registry.SuspendUser(SingleSessionBuilder.UserId,
                setup.Session.Time.GetUtcNow().AddHours(SuspensionHours).UtcDateTime);
            connection.ReadInitialState();

            Assert.True(connection.InitialState.IsSuspended);
        }

        [Fact]
        public void Subscribe_InboxChangeThenRevocation_EndsWithInvalidSession()
        {
            using SessionConnectionBuilder setup = new SessionConnectionBuilder();
            SessionConnectionBuilder.ConnectionProbe connection = setup.Subscribe();
            setup.Session.Registry.MarkInboxChanged(SingleSessionBuilder.UserId);
            connection.ReadInitialState();

            setup.Session.Registry.RemoveSession(setup.Session.Token);

            Assert.IsType<InvalidSessionException>(connection.WaitForCompletion());
        }

        [Fact]
        public void SendFriendRequest_StoredRequest_NotifiesRecipient()
        {
            using InboxNotificationBuilder setup = new InboxNotificationBuilder();

            setup.SendRequest();

            Assert.Equal(InboxNotificationBuilder.OneChange,
                setup.Registry.GetInboxRevision(setup.RecipientToken));
        }

        [Fact]
        public void SendFriendRequest_StoredRequest_DoesNotNotifySender()
        {
            using InboxNotificationBuilder setup = new InboxNotificationBuilder();

            setup.SendRequest();

            Assert.Equal(InboxNotificationBuilder.NoChanges,
                setup.Registry.GetInboxRevision(setup.SenderToken));
        }

        [Fact]
        public void SendFriendRequest_StoredRequest_DoesNotNotifyUnrelatedAccount()
        {
            using InboxNotificationBuilder setup = new InboxNotificationBuilder();

            setup.SendRequest();

            Assert.Equal(InboxNotificationBuilder.NoChanges,
                setup.Registry.GetInboxRevision(setup.OtherToken));
        }

        [Fact]
        public void SendFriendRequest_MissingRecipient_DoesNotNotify()
        {
            using InboxNotificationBuilder setup = new InboxNotificationBuilder();

            Record.Exception(() => setup.Manager.SendFriendRequest(
                setup.Request(InboxNotificationBuilder.MissingUsername)));

            Assert.Equal(InboxNotificationBuilder.NoChanges,
                setup.Registry.GetInboxRevision(setup.RecipientToken));
        }

        [Fact]
        public void SendFriendRequest_DuplicateRequest_DoesNotNotifyAgain()
        {
            using InboxNotificationBuilder setup = new InboxNotificationBuilder();
            setup.SendRequest();

            Record.Exception(setup.SendRequest);

            Assert.Equal(InboxNotificationBuilder.OneChange,
                setup.Registry.GetInboxRevision(setup.RecipientToken));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RespondToRequest_PendingRequest_NotifiesRecipient(bool accept)
        {
            using InboxNotificationBuilder setup = new InboxNotificationBuilder();
            setup.SendRequest();

            setup.Respond(accept);

            Assert.Equal(InboxNotificationBuilder.TwoChanges,
                setup.Registry.GetInboxRevision(setup.RecipientToken));
        }

        [Fact]
        public void RespondToRequest_UnknownRequest_DoesNotNotify()
        {
            using InboxNotificationBuilder setup = new InboxNotificationBuilder();

            Record.Exception(() => setup.Manager.RespondToRequest(setup.InvalidReply()));

            Assert.Equal(InboxNotificationBuilder.NoChanges,
                setup.Registry.GetInboxRevision(setup.RecipientToken));
        }

        [Fact]
        public void RespondToRequest_CrossedSession_DoesNotNotify()
        {
            using InboxNotificationBuilder setup = new InboxNotificationBuilder();
            setup.SendRequest();

            Record.Exception(() => setup.Manager.RespondToRequest(setup.CrossedReply()));

            Assert.Equal(InboxNotificationBuilder.OneChange,
                setup.Registry.GetInboxRevision(setup.RecipientToken));
        }

        [Fact]
        public void RejectInvitation_StoredInvitation_NotifiesRecipient()
        {
            using InboxNotificationBuilder setup = new InboxNotificationBuilder();
            setup.SeedInvitation();

            setup.Invitations.RejectInvitation(setup.Action());

            Assert.Equal(InboxNotificationBuilder.OneChange,
                setup.Registry.GetInboxRevision(setup.RecipientToken));
        }

        [Fact]
        public void RejectInvitation_StoredInvitation_RemovesPendingInvitation()
        {
            using InboxNotificationBuilder setup = new InboxNotificationBuilder();
            setup.SeedInvitation();

            setup.Invitations.RejectInvitation(setup.Action());

            Assert.Empty(setup.InvitationRepository.GetPendingInvitations(
                InboxNotificationBuilder.RecipientId).Invitations);
        }

        [Fact]
        public void RejectInvitation_UnknownInvitation_DoesNotNotify()
        {
            using InboxNotificationBuilder setup = new InboxNotificationBuilder();

            Record.Exception(() => setup.Invitations.RejectInvitation(
                setup.Action(InboxNotificationBuilder.UnknownRequestId)));

            Assert.Equal(InboxNotificationBuilder.NoChanges,
                setup.Registry.GetInboxRevision(setup.RecipientToken));
        }

        [Fact]
        public void AcceptInvitation_AvailableLobby_NotifiesRecipient()
        {
            using InboxNotificationBuilder setup = new InboxNotificationBuilder();
            setup.SeedInvitation();
            setup.OpenInvitedLobby();

            setup.Invitations.AcceptInvitation(setup.Action());

            Assert.Equal(InboxNotificationBuilder.OneChange,
                setup.Registry.GetInboxRevision(setup.RecipientToken));
        }

        [Fact]
        public void AcceptInvitation_UnavailableLobby_DoesNotNotify()
        {
            using InboxNotificationBuilder setup = new InboxNotificationBuilder();
            setup.SeedInvitation();

            Record.Exception(() => setup.Invitations.AcceptInvitation(setup.Action()));

            Assert.Equal(InboxNotificationBuilder.NoChanges,
                setup.Registry.GetInboxRevision(setup.RecipientToken));
        }
    }
}
