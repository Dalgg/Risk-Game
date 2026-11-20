using System;
using Grpc.Core;
using RiskGame.Server.Services;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class ProfileCustomizationTests
    {
        private const string NewNickname = "Diego León";
        private const string PaddedNickname = "  Diego León  ";
        private const string SelectedAvatar = "tower";
        private const int ExcessiveLength = 51;

        [Fact]
        public void UpdateNickname_ValidNickname_Persists()
        {
            using ProfileCustomizationBuilder setup = new ProfileCustomizationBuilder();

            setup.UpdateNickname(ProfileCustomizationBuilder.UserId, setup.Token, NewNickname);

            Assert.Equal(NewNickname, setup.ReadUser().Nickname);
        }

        [Fact]
        public void UpdateNickname_Whitespace_Trims()
        {
            using ProfileCustomizationBuilder setup = new ProfileCustomizationBuilder();

            setup.UpdateNickname(ProfileCustomizationBuilder.UserId, setup.Token, PaddedNickname);

            Assert.Equal(NewNickname, setup.Repository.GetProfile(ProfileCustomizationBuilder.UserId).Nickname);
        }

        [Fact]
        public void UpdateNickname_ValidNickname_PreservesSignInUsername()
        {
            using ProfileCustomizationBuilder setup = new ProfileCustomizationBuilder();

            setup.UpdateNickname(ProfileCustomizationBuilder.UserId, setup.Token, NewNickname);

            Assert.Equal(ProfileCustomizationBuilder.Username, setup.ReadUser().Username);
        }

        [Fact]
        public void UpdateNickname_ValidNickname_PreservesPassword()
        {
            using ProfileCustomizationBuilder setup = new ProfileCustomizationBuilder();

            setup.UpdateNickname(ProfileCustomizationBuilder.UserId, setup.Token, NewNickname);

            Assert.Equal(ProfileCustomizationBuilder.PasswordHash, setup.ReadUser().PasswordHash);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Diego\nLeón")]
        [InlineData("Diego\tLeón")]
        public void NormalizeNickname_InvalidContent_Rejects(string nickname)
        {
            Assert.Throws<ArgumentException>(() => ProfileCustomizationRules.NormalizeNickname(nickname));
        }

        [Fact]
        public void NormalizeNickname_TooLong_Rejects()
        {
            string nickname = new string('A', ExcessiveLength);

            Assert.Throws<ArgumentException>(() => ProfileCustomizationRules.NormalizeNickname(nickname));
        }

        [Theory]
        [InlineData("default")]
        [InlineData("explorer")]
        [InlineData("tower")]
        [InlineData("crown")]
        [InlineData("dice")]
        public void UpdateAvatar_CatalogChoice_Persists(string reference)
        {
            using ProfileCustomizationBuilder setup = new ProfileCustomizationBuilder();

            setup.UpdateAvatar(ProfileCustomizationBuilder.UserId, setup.Token, reference);

            Assert.Equal(reference, setup.Repository.GetProfile(ProfileCustomizationBuilder.UserId).AvatarReference);
        }

        [Fact]
        public void UpdateAvatar_ValidChoice_PreservesNickname()
        {
            using ProfileCustomizationBuilder setup = new ProfileCustomizationBuilder();

            setup.UpdateAvatar(ProfileCustomizationBuilder.UserId, setup.Token, SelectedAvatar);

            Assert.Equal(ProfileCustomizationBuilder.OriginalNickname, setup.ReadUser().Nickname);
        }

        [Theory]
        [InlineData("../outside.png")]
        [InlineData("https://example.com/avatar.png")]
        [InlineData("unknown")]
        [InlineData("")]
        public void UpdateAvatar_UnknownReference_Rejects(string reference)
        {
            using ProfileCustomizationBuilder setup = new ProfileCustomizationBuilder();

            RpcException exception = (RpcException)Record.Exception(() =>
                setup.UpdateAvatar(ProfileCustomizationBuilder.UserId, setup.Token, reference))!;

            Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
        }

        [Fact]
        public void UpdateNickname_TokenOfAnotherUser_Rejects()
        {
            using ProfileCustomizationBuilder setup = new ProfileCustomizationBuilder();

            RpcException exception = (RpcException)Record.Exception(() =>
                setup.UpdateNickname(ProfileCustomizationBuilder.OtherUserId, setup.Token, NewNickname))!;

            Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
        }

        [Fact]
        public void UpdateAvatar_ExpiredSession_Rejects()
        {
            using ProfileCustomizationBuilder setup = new ProfileCustomizationBuilder();
            setup.ExpireSession();

            RpcException exception = (RpcException)Record.Exception(() =>
                setup.UpdateAvatar(ProfileCustomizationBuilder.UserId, setup.Token, SelectedAvatar))!;

            Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
        }

        [Fact]
        public void UpdateNickname_Guest_Rejects()
        {
            using ProfileCustomizationBuilder setup = new ProfileCustomizationBuilder();

            RpcException exception = (RpcException)Record.Exception(() =>
                setup.UpdateNickname(ProfileCustomizationBuilder.GuestId, setup.GuestToken, NewNickname))!;

            Assert.Equal(StatusCode.PermissionDenied, exception.StatusCode);
        }

        [Fact]
        public void UpdateAvatar_Guest_Rejects()
        {
            using ProfileCustomizationBuilder setup = new ProfileCustomizationBuilder();

            RpcException exception = (RpcException)Record.Exception(() =>
                setup.UpdateAvatar(ProfileCustomizationBuilder.GuestId, setup.GuestToken, SelectedAvatar))!;

            Assert.Equal(StatusCode.PermissionDenied, exception.StatusCode);
        }
    }
}
