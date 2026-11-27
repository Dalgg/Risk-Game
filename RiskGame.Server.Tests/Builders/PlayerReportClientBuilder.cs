extern alias Client;

using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SessionService = Client::RiskGame.Services.SessionService;
using SessionUser = Client::RiskGame.Services.SessionUser;
using Strings = Client::RiskGame.Resources.Strings;
using LobbyWindow = Client::RiskGame.Views.LobbyWindow;
using LobbyPlayer = Client::RiskGame.Contracts.Lobby.LobbyPlayer;
using LobbyDetails = Client::RiskGame.Contracts.Lobby.LobbyDetails;
using LobbyRules = Client::RiskGame.Contracts.Lobby.LobbyRules;
using LobbyStatus = Client::RiskGame.Contracts.Lobby.LobbyStatus;
using PlayerSummary = Client::RiskGame.Contracts.Common.PlayerSummary;
using AccountSessionConnection = Client::RiskGame.Services.AccountSessionConnection;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class PlayerReportClientBuilder : IDisposable
    {
        public const int ReporterId = 41;
        public const int ReportedId = 43;
        public const int GuestId = -41;
        public const string SpanishCulture = "es-MX";
        public const string EnglishCulture = "en-US";
        public const string ExpirationText = "2030-04-05T12:30:00Z";
        public const string ReportedNickname = "Reported player";
        public const string ConfirmationOverlayName = "ReportConfirmationOverlay";
        public const string ConfirmationTextName = "ReportConfirmationTextBlock";
        public const string PlayersPanelName = "PlayersStackPanel";
        public const string ReplacementToken = "replacement-report-token";
        private const string Username = "report.test";
        private const string SessionToken = "report-client-test-token";
        private const string ApplySnapshotMethod = "ApplyLobbyDetails";
        private const string RenderPlayersMethod = "RenderPlayers";
        private const string SessionConnectionField = "accountConnection";
        private const string ExpireSessionMethod = "ExpireSession";
        private const int LobbyId = 18;
        private const int TurnSeconds = 60;
        private const string LobbyCode = "REPORT1";
        private readonly SessionUser? previousSession = SessionService.CurrentUser;
        private readonly CultureInfo? previousCulture = Strings.Culture;
        private LobbyWindow? window;

        public PlayerReportClientBuilder(string cultureName = SpanishCulture)
        {
            Strings.Culture = new CultureInfo(cultureName);
            Session = new SessionUser(ReporterId, Username, Username) { SessionToken = SessionToken };
            SessionService.Login(Session);
        }

        public SessionUser Session { get; }

        public static DateTimeOffset Expiration => DateTimeOffset.Parse(ExpirationText, CultureInfo.InvariantCulture);

        public static long ExpirationMilliseconds => Expiration.ToUnixTimeMilliseconds();

        public LobbyWindow Window
        {
            get
            {
                if (window == null)
                {
                    throw new InvalidOperationException("Create the lobby window before accessing it.");
                }
                return window;
            }
        }

        public void UseGuest()
        {
            SessionService.Login(new SessionUser(GuestId, Username, Username));
        }

        public void ClearSession()
        {
            UseGuest();
            SessionService.Logout();
        }

        public static LobbyPlayer Player(int userId, bool isGuest = false)
        {
            return new LobbyPlayer
            {
                Player = new PlayerSummary { UserId = userId, Nickname = ReportedNickname, IsGuest = isGuest }
            };
        }

        public void CreateLobbyWindow()
        {
            UseGuest();
            window = new LobbyWindow();
            LobbyDetails details = new LobbyDetails
            {
                LobbyId = LobbyId, LobbyCode = LobbyCode, HostUserId = ReportedId,
                Status = LobbyStatus.Waiting, CanStartMatch = true,
                Rules = new LobbyRules { TurnDurationSeconds = TurnSeconds }
            };
            details.Players.Add(Player(ReporterId));
            details.Players.Add(Player(ReportedId));
            details.Players.Add(Player(GuestId, true));
            InvokeWindowMethod(ApplySnapshotMethod, details);
            SessionService.Login(Session);
            InvokeWindowMethod(RenderPlayersMethod, details.Players);
            Window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        public void OpenConfirmation()
        {
            Button reportButton = Window.FindControl<StackPanel>(PlayersPanelName)!
                .GetVisualDescendants().OfType<Button>()
                .Single(button => button.Tag is int targetUserId && targetUserId == ReportedId);
            reportButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
        }

        public void PressEscape()
        {
            Window.FindControl<Border>(ConfirmationOverlayName)!.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape
            });
        }

        public AccountSessionConnection AttachLocalConnection()
        {
            AccountSessionConnection connection = new AccountSessionConnection(Session, UnexpectedExpiry);
            FieldInfo connectionField = typeof(SessionService).GetField(SessionConnectionField,
                BindingFlags.Static | BindingFlags.NonPublic)!;
            connectionField.SetValue(null, connection);
            return connection;
        }

        public static void ReplaceLogin()
        {
            SessionService.Login(new SessionUser(ReporterId, Username, Username) { SessionToken = ReplacementToken });
        }

        public static void ExpireConnection(AccountSessionConnection connection)
        {
            MethodInfo expireMethod = typeof(SessionService).GetMethod(ExpireSessionMethod,
                BindingFlags.Static | BindingFlags.NonPublic)!;
            expireMethod.Invoke(null, new object[] { connection });
        }

        private static void UnexpectedExpiry(AccountSessionConnection connection)
        {
            throw new InvalidOperationException("The test must not start a real session monitor.");
        }

        private void InvokeWindowMethod(string methodName, object argument)
        {
            MethodInfo windowMethod = typeof(LobbyWindow).GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            windowMethod.Invoke(Window, new[] { argument });
        }

        public void Dispose()
        {
            try
            {
                ClearSession();
                if (window != null)
                {
                    window.Close();
                    Dispatcher.UIThread.RunJobs();
                }
            }
            finally
            {
                ClearSession();
                Strings.Culture = previousCulture!;
                if (previousSession != null)
                {
                    SessionService.Login(previousSession);
                }
            }
        }
    }
}
