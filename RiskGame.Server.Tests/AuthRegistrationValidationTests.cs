using RiskGame.Models;
using RiskGame.Server.Services;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class AuthRegistrationValidationTests
    {
        private const string ValidEmail = "player.one@example.com";
        private const string ValidUsername = "player_one";
        private const string ValidPassword = "StrongPassword1!";
        private const string WhitespaceInput = "   ";
        private const string InvalidEmail = "not-an-email";
        private const string OversizedEmailDomain = "@example.com";
        private const char FillerCharacter = 'a';
        private const int OversizedLocalPartLength = 95;
        private const string ShortUsername = "ab";
        private const string InvalidUsername = "bad player!";
        private const string WeakPassword = "weak";

        [Fact]
        public void ValidateRegistration_EmptyEmail_ReturnsEmptyFields()
        {
            AuthRepositoryService repositoryService = new AuthRepositoryService();

            RegisterResult result = repositoryService.ValidateRegistration(WhitespaceInput, ValidUsername, ValidPassword);

            Assert.Equal(RegisterResult.EmptyFields, result);
        }

        [Fact]
        public void ValidateRegistration_EmptyUsername_ReturnsEmptyFields()
        {
            AuthRepositoryService repositoryService = new AuthRepositoryService();

            RegisterResult result = repositoryService.ValidateRegistration(ValidEmail, WhitespaceInput, ValidPassword);

            Assert.Equal(RegisterResult.EmptyFields, result);
        }

        [Fact]
        public void ValidateRegistration_EmptyPassword_ReturnsEmptyFields()
        {
            AuthRepositoryService repositoryService = new AuthRepositoryService();

            RegisterResult result = repositoryService.ValidateRegistration(ValidEmail, ValidUsername, string.Empty);

            Assert.Equal(RegisterResult.EmptyFields, result);
        }

        [Fact]
        public void ValidateRegistration_InvalidEmailFormat_ReturnsInvalidEmail()
        {
            AuthRepositoryService repositoryService = new AuthRepositoryService();

            RegisterResult result = repositoryService.ValidateRegistration(InvalidEmail, ValidUsername, ValidPassword);

            Assert.Equal(RegisterResult.InvalidEmail, result);
        }

        [Fact]
        public void ValidateRegistration_EmailExceedingMaximumLength_ReturnsInvalidEmail()
        {
            AuthRepositoryService repositoryService = new AuthRepositoryService();
            string oversizedEmail = new string(FillerCharacter, OversizedLocalPartLength) + OversizedEmailDomain;

            RegisterResult result = repositoryService.ValidateRegistration(oversizedEmail, ValidUsername, ValidPassword);

            Assert.Equal(RegisterResult.InvalidEmail, result);
        }

        [Fact]
        public void ValidateRegistration_ShortUsername_ReturnsInvalidUsername()
        {
            AuthRepositoryService repositoryService = new AuthRepositoryService();

            RegisterResult result = repositoryService.ValidateRegistration(ValidEmail, ShortUsername, ValidPassword);

            Assert.Equal(RegisterResult.InvalidUsername, result);
        }

        [Fact]
        public void ValidateRegistration_UsernameWithInvalidCharacters_ReturnsInvalidUsername()
        {
            AuthRepositoryService repositoryService = new AuthRepositoryService();

            RegisterResult result = repositoryService.ValidateRegistration(ValidEmail, InvalidUsername, ValidPassword);

            Assert.Equal(RegisterResult.InvalidUsername, result);
        }

        [Fact]
        public void ValidateRegistration_WeakPassword_ReturnsWeakPassword()
        {
            AuthRepositoryService repositoryService = new AuthRepositoryService();

            RegisterResult result = repositoryService.ValidateRegistration(ValidEmail, ValidUsername, WeakPassword);

            Assert.Equal(RegisterResult.WeakPassword, result);
        }
    }
}
