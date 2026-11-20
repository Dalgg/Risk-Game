extern alias Client;

using System;
using Avalonia.Threading;
using SessionService = Client::RiskGame.Services.SessionService;
using SessionUser = Client::RiskGame.Services.SessionUser;
using ProfileAvatarCatalog = Client::RiskGame.Services.ProfileAvatarCatalog;
using UserProfile = Client::RiskGame.Contracts.Profile.UserProfile;
using ProfileViewModel = Client::RiskGame.ViewModels.ProfileViewModel;
using ProfileWindow = Client::RiskGame.Views.ProfileWindow;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class ProfileWindowBuilder : IDisposable
    {
        public const int UserId = 41;
        public const string Nickname = "Original";
        public const string ChangedNickname = "Diego León";
        public const string Username = "sign.in.user";
        public const string Token = "profile-test-session";
        private const int GuestId = -41;
        private readonly SessionUser? previousSession = SessionService.CurrentUser;

        public ProfileWindowBuilder()
        {
            Session = new SessionUser(UserId, Username, Nickname) { SessionToken = Token };
            SessionService.Login(Session);
            ViewModel = new ProfileViewModel(Session);
            Window = new ProfileWindow { DataContext = ViewModel };
            Window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        public SessionUser Session { get; }

        public ProfileViewModel ViewModel { get; }

        public ProfileWindow Window { get; }

        public static UserProfile UpdatedProfile()
        {
            return new UserProfile
            {
                UserId = UserId,
                Username = Username,
                Nickname = ChangedNickname,
                AvatarReference = ProfileAvatarCatalog.TowerAvatarReference
            };
        }

        public void Dispose()
        {
            Window.Close();
            if (previousSession != null)
            {
                SessionService.Login(previousSession);
            }
            else
            {
                SessionService.Login(new SessionUser(GuestId, string.Empty, string.Empty));
                SessionService.Logout();
            }
        }
    }
}
