extern alias Client;

using System;
using System.Globalization;
using Avalonia.Controls;
using RiskGame.Server.Tests.Builders;
using Xunit;
using LobbyStatus = Client::RiskGame.Contracts.Lobby.LobbyStatus;
using PlayerReportViewModel = Client::RiskGame.ViewModels.PlayerReportViewModel;
using AccountSuspensionMessage = Client::RiskGame.Services.AccountSuspensionMessage;
using AccountSessionConnection = Client::RiskGame.Services.AccountSessionConnection;
using SessionService = Client::RiskGame.Services.SessionService;
using Strings = Client::RiskGame.Resources.Strings;

namespace RiskGame.Server.Tests
{
    [CollectionDefinition(CollectionName, DisableParallelization = true)]
    public sealed class PlayerReportClientCollection
    {
        public const string CollectionName = "Player reports client";
    }

    [Collection(PlayerReportClientCollection.CollectionName)]
    public sealed class PlayerReportClientTests
    {
        private const long NoExpiration = 0;
        private const long InvalidExpiration = -1;
        private const string LocalDateFormat = "g";
        private const string SpanishSuspensionPrefix = "Tu cuenta está suspendida hasta ";
        private const string EnglishSuspensionPrefix = "Your account is suspended until ";

        [Fact]
        public void CanReport_RegisteredOpponentInWaitingLobby_Allows()
        {
            using PlayerReportClientBuilder setup = new PlayerReportClientBuilder();

            bool allowed = PlayerReportViewModel.CanReport(PlayerReportClientBuilder.Player(
                PlayerReportClientBuilder.ReportedId), LobbyStatus.Waiting);

            Assert.True(allowed);
        }

        [Fact]
        public void CanReport_GuestReporter_Rejects()
        {
            using PlayerReportClientBuilder setup = new PlayerReportClientBuilder();
            setup.UseGuest();

            bool allowed = PlayerReportViewModel.CanReport(PlayerReportClientBuilder.Player(
                PlayerReportClientBuilder.ReportedId), LobbyStatus.Waiting);

            Assert.False(allowed);
        }

        [Fact]
        public void CanReport_NoSession_Rejects()
        {
            using PlayerReportClientBuilder setup = new PlayerReportClientBuilder();
            setup.ClearSession();

            bool allowed = PlayerReportViewModel.CanReport(PlayerReportClientBuilder.Player(
                PlayerReportClientBuilder.ReportedId), LobbyStatus.Waiting);

            Assert.False(allowed);
        }

        [Fact]
        public void CanReport_Self_Rejects()
        {
            using PlayerReportClientBuilder setup = new PlayerReportClientBuilder();

            bool allowed = PlayerReportViewModel.CanReport(PlayerReportClientBuilder.Player(
                PlayerReportClientBuilder.ReporterId), LobbyStatus.Waiting);

            Assert.False(allowed);
        }

        [Fact]
        public void CanReport_GuestOpponent_Rejects()
        {
            using PlayerReportClientBuilder setup = new PlayerReportClientBuilder();

            bool allowed = PlayerReportViewModel.CanReport(PlayerReportClientBuilder.Player(
                PlayerReportClientBuilder.GuestId, true), LobbyStatus.Waiting);

            Assert.False(allowed);
        }

        [Fact]
        public void CanReport_NegativeIdentifierWithoutGuestFlag_Rejects()
        {
            using PlayerReportClientBuilder setup = new PlayerReportClientBuilder();

            bool allowed = PlayerReportViewModel.CanReport(PlayerReportClientBuilder.Player(
                PlayerReportClientBuilder.GuestId), LobbyStatus.Waiting);

            Assert.False(allowed);
        }

        [Theory]
        [InlineData(LobbyStatus.InMatch)]
        [InlineData(LobbyStatus.Starting)]
        [InlineData(LobbyStatus.Closed)]
        public void CanReport_LobbyNotWaiting_Rejects(LobbyStatus status)
        {
            using PlayerReportClientBuilder setup = new PlayerReportClientBuilder();

            bool allowed = PlayerReportViewModel.CanReport(PlayerReportClientBuilder.Player(
                PlayerReportClientBuilder.ReportedId), status);

            Assert.False(allowed);
        }

        [Theory]
        [InlineData(PlayerReportClientBuilder.SpanishCulture, SpanishSuspensionPrefix)]
        [InlineData(PlayerReportClientBuilder.EnglishCulture, EnglishSuspensionPrefix)]
        public void SuspensionMessage_UsesSelectedLanguage(string cultureName, string expectedPrefix)
        {
            using PlayerReportClientBuilder setup = new PlayerReportClientBuilder(cultureName);

            string message = AccountSuspensionMessage.Format(PlayerReportClientBuilder.ExpirationMilliseconds);

            Assert.StartsWith(expectedPrefix, message);
        }

        [Theory]
        [InlineData(PlayerReportClientBuilder.SpanishCulture)]
        [InlineData(PlayerReportClientBuilder.EnglishCulture)]
        public void SuspensionMessage_UsesLocalExpiration(string cultureName)
        {
            using PlayerReportClientBuilder setup = new PlayerReportClientBuilder(cultureName);
            DateTime localExpiration = TimeZoneInfo.ConvertTime(PlayerReportClientBuilder.Expiration,
                TimeZoneInfo.Local).DateTime;

            string message = AccountSuspensionMessage.Format(PlayerReportClientBuilder.ExpirationMilliseconds);

            Assert.Contains(localExpiration.ToString(LocalDateFormat, new CultureInfo(cultureName)), message);
        }

        [Theory]
        [InlineData(NoExpiration)]
        [InlineData(InvalidExpiration)]
        [InlineData(long.MaxValue)]
        public void SuspensionMessage_InvalidExpiration_ShowsSuspensionWithoutDate(long expiration)
        {
            using PlayerReportClientBuilder setup = new PlayerReportClientBuilder();

            string message = AccountSuspensionMessage.Format(expiration);

            Assert.Equal(Strings.AccountSuspendedMessage, message);
        }

        [Fact]
        public void ReportButton_OpensConfirmation()
        {
            ProfileTestApplication.Run(() =>
            {
                using PlayerReportClientBuilder setup = new PlayerReportClientBuilder();
                setup.CreateLobbyWindow();

                setup.OpenConfirmation();

                Assert.True(setup.Window.FindControl<Border>(PlayerReportClientBuilder.ConfirmationOverlayName)!
                    .IsVisible);
            });
        }

        [Fact]
        public void Confirmation_ShowsReportedNickname()
        {
            ProfileTestApplication.Run(() =>
            {
                using PlayerReportClientBuilder setup = new PlayerReportClientBuilder();
                setup.CreateLobbyWindow();

                setup.OpenConfirmation();

                Assert.Contains(PlayerReportClientBuilder.ReportedNickname,
                    setup.Window.FindControl<TextBlock>(PlayerReportClientBuilder.ConfirmationTextName)!.Text!);
            });
        }

        [Fact]
        public void Confirmation_DisablesBackgroundControls()
        {
            ProfileTestApplication.Run(() =>
            {
                using PlayerReportClientBuilder setup = new PlayerReportClientBuilder();
                setup.CreateLobbyWindow();

                setup.OpenConfirmation();

                Assert.False(setup.Window.FindControl<StackPanel>(PlayerReportClientBuilder.PlayersPanelName)!
                    .IsEffectivelyEnabled);
            });
        }

        [Fact]
        public void Confirmation_Escape_ClosesWithoutSubmitting()
        {
            ProfileTestApplication.Run(() =>
            {
                using PlayerReportClientBuilder setup = new PlayerReportClientBuilder();
                setup.CreateLobbyWindow();
                setup.OpenConfirmation();

                setup.PressEscape();

                Assert.False(setup.Window.FindControl<Border>(PlayerReportClientBuilder.ConfirmationOverlayName)!
                    .IsVisible);
            });
        }

        [Fact]
        public void SessionExpiry_OldConnection_DoesNotClearNewLogin()
        {
            using PlayerReportClientBuilder setup = new PlayerReportClientBuilder();
            using AccountSessionConnection previousConnection = setup.AttachLocalConnection();
            PlayerReportClientBuilder.ReplaceLogin();

            PlayerReportClientBuilder.ExpireConnection(previousConnection);

            Assert.Equal(PlayerReportClientBuilder.ReplacementToken,
                SessionService.GetOrCreateCurrentUser().SessionToken);
        }

        [Fact]
        public void SessionExpiry_CurrentConnection_ClearsCurrentLogin()
        {
            using PlayerReportClientBuilder setup = new PlayerReportClientBuilder();
            using AccountSessionConnection connection = setup.AttachLocalConnection();

            PlayerReportClientBuilder.ExpireConnection(connection);

            Assert.Null(SessionService.CurrentUser);
        }
    }
}
