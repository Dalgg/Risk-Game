extern alias Client;

using System.Linq;
using Avalonia.Media;
using Avalonia.Threading;
using RiskGame.Server.Tests.Builders;
using Xunit;
using SessionService = Client::RiskGame.Services.SessionService;
using SessionUser = Client::RiskGame.Services.SessionUser;
using UserProfile = Client::RiskGame.Contracts.Profile.UserProfile;
using MainViewModel = Client::RiskGame.ViewModels.MainViewModel;
using ProfileAvatarCatalog = Client::RiskGame.Services.ProfileAvatarCatalog;

namespace RiskGame.Server.Tests
{
    public sealed class ProfileWindowTests
    {
        private const int CatalogSize = 5;
        private const int AnotherUserId = 43;
        private const string LegacyAvatarReference = "legacy-avatar.png";
        private const string WhitespaceNickname = "   ";
        private const string ReplacementSessionToken = "replacement-session";

        [Fact]
        public void AvatarCatalog_AllChoices_LoadImages()
        {
            ProfileTestApplication.Run(() =>
            {
                IImage[] images = ProfileAvatarCatalog.CreateOptions().Select(option => option.Image).ToArray();
    
                Assert.Equal(CatalogSize, images.Length);
            });
        }

        [Fact]
        public void AvatarCatalog_UnknownReference_UsesDefault()
        {
            ProfileTestApplication.Run(() =>
            {
                IImage image = ProfileAvatarCatalog.GetImage(LegacyAvatarReference);
    
                Assert.Same(ProfileAvatarCatalog.GetImage(ProfileAvatarCatalog.DefaultAvatarReference), image);
            });
        }

        [Fact]
        public void ChooseAvatar_OpensCatalog()
        {
            ProfileTestApplication.Run(() =>
            {
                using ProfileWindowBuilder setup = new ProfileWindowBuilder();
    
                setup.ViewModel.ChooseAvatarCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
    
                Assert.True(setup.ViewModel.IsChoosingAvatar);
            });
        }

        [Fact]
        public void EditNickname_ClosesCatalog()
        {
            ProfileTestApplication.Run(() =>
            {
                using ProfileWindowBuilder setup = new ProfileWindowBuilder();
                setup.ViewModel.ChooseAvatarCommand.Execute(null);
    
                setup.ViewModel.EditNicknameCommand.Execute(null);
    
                Assert.False(setup.ViewModel.IsChoosingAvatar);
            });
        }

        [Fact]
        public void SaveNickname_Blank_ShowsValidation()
        {
            ProfileTestApplication.Run(() =>
            {
                using ProfileWindowBuilder setup = new ProfileWindowBuilder();
                setup.ViewModel.EditedNickname = WhitespaceNickname;
    
                setup.ViewModel.SaveNicknameCommand.Execute(null);
    
                Assert.Equal(Client::RiskGame.Resources.Strings.ProfileNicknameInvalid, setup.ViewModel.StatusMessage);
            });
        }

        [Fact]
        public void CancelEdit_KeepsOriginalNickname()
        {
            ProfileTestApplication.Run(() =>
            {
                using ProfileWindowBuilder setup = new ProfileWindowBuilder();
                setup.ViewModel.EditNicknameCommand.Execute(null);
                setup.ViewModel.EditedNickname = ProfileWindowBuilder.ChangedNickname;
    
                setup.ViewModel.CancelEditCommand.Execute(null);
    
                Assert.Equal(ProfileWindowBuilder.Nickname, setup.ViewModel.Nickname);
            });
        }

        [Fact]
        public void UpdateSession_SameSession_RefreshesMenuNickname()
        {
            ProfileTestApplication.Run(() =>
            {
                using ProfileWindowBuilder setup = new ProfileWindowBuilder();
    
                SessionService.UpdateProfile(setup.Session, ProfileWindowBuilder.UpdatedProfile());
    
                Assert.Equal(ProfileWindowBuilder.ChangedNickname, new MainViewModel().LoggedNickname);
            });
        }

        [Fact]
        public void UpdateSession_SameSession_RefreshesMenuAvatar()
        {
            ProfileTestApplication.Run(() =>
            {
                using ProfileWindowBuilder setup = new ProfileWindowBuilder();
    
                SessionService.UpdateProfile(setup.Session, ProfileWindowBuilder.UpdatedProfile());
    
                Assert.Same(ProfileAvatarCatalog.GetImage(ProfileAvatarCatalog.TowerAvatarReference), new MainViewModel().AvatarImage);
            });
        }

        [Fact]
        public void UpdateSession_SameSession_PreservesToken()
        {
            ProfileTestApplication.Run(() =>
            {
                using ProfileWindowBuilder setup = new ProfileWindowBuilder();
    
                SessionService.UpdateProfile(setup.Session, ProfileWindowBuilder.UpdatedProfile());
    
                Assert.Equal(ProfileWindowBuilder.Token, SessionService.GetOrCreateCurrentUser().SessionToken);
            });
        }

        [Fact]
        public void UpdateSession_DifferentAccount_DoesNotOverwrite()
        {
            ProfileTestApplication.Run(() =>
            {
                using ProfileWindowBuilder setup = new ProfileWindowBuilder();
                UserProfile profile = ProfileWindowBuilder.UpdatedProfile();
                profile.UserId = AnotherUserId;
    
                bool updated = SessionService.UpdateProfile(setup.Session, profile);
    
                Assert.False(updated);
            });
        }

        [Fact]
        public void UpdateSession_ReplacedSession_DoesNotOverwrite()
        {
            ProfileTestApplication.Run(() =>
            {
                using ProfileWindowBuilder setup = new ProfileWindowBuilder();
                SessionService.Login(new SessionUser(ProfileWindowBuilder.UserId, ProfileWindowBuilder.Username,
                    ProfileWindowBuilder.Nickname) { SessionToken = ReplacementSessionToken });
    
                bool updated = SessionService.UpdateProfile(setup.Session, ProfileWindowBuilder.UpdatedProfile());
    
                Assert.False(updated);
            });
        }
    }
}
