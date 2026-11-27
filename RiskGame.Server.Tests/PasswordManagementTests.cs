extern alias Client;

using System;
using Grpc.Core;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Server.Tests.Builders;
using Xunit;
using ClientForgotPasswordViewModel = Client::RiskGame.ViewModels.ForgotPasswordViewModel;
using ClientStrings = Client::RiskGame.Resources.Strings;

namespace RiskGame.Server.Tests
{
    public sealed class PasswordManagementTests
    {
        private const string UppercaseEmail = "PASSWORD.TESTER@EXAMPLE.COM";
        private const string UnknownEmail = "missing.user@example.com";
        private const string MalformedEmail = "not-an-email";
        private const string WrongCurrentPassword = "WrongPassword9!";
        private const string MismatchedPassword = "OtherPassword3#";
        private const string ValidSampleCode = "123456";
        private const string AlternatePassword = "ThirdPassword3#";
        private const int ExpectedResendCount = 2;

        [Fact]
        public void UpdatePassword_ValidCredentials_ReplacesHashAndAuthenticatesWithNewPassword()
        {
            using PasswordManagementBuilder setup = new PasswordManagementBuilder();

            setup.UpdatePasswordViaGrpc(
                PasswordManagementBuilder.UserId,
                setup.Token,
                PasswordManagementBuilder.InitialPassword,
                PasswordManagementBuilder.ReplacementPassword);

            User authenticated = setup.AuthRepository.AuthenticateUser(
                PasswordManagementBuilder.Username,
                PasswordManagementBuilder.ReplacementPassword);
            Assert.Equal(PasswordManagementBuilder.UserId, authenticated.IdUser);
            Assert.Throws<InvalidCredentialsException>(() =>
                setup.AuthRepository.AuthenticateUser(
                    PasswordManagementBuilder.Username,
                    PasswordManagementBuilder.InitialPassword));
        }

        [Fact]
        public void UpdatePassword_WrongCurrentPassword_RejectsWithFailedPrecondition()
        {
            using PasswordManagementBuilder setup = new PasswordManagementBuilder();

            RpcException exception = Assert.Throws<RpcException>(() =>
                setup.UpdatePasswordViaGrpc(
                    PasswordManagementBuilder.UserId,
                    setup.Token,
                    WrongCurrentPassword,
                    PasswordManagementBuilder.ReplacementPassword));

            Assert.Equal(StatusCode.FailedPrecondition, exception.StatusCode);
        }

        [Fact]
        public void UpdatePassword_WeakNewPassword_RejectsWithInvalidArgument()
        {
            using PasswordManagementBuilder setup = new PasswordManagementBuilder();

            RpcException exception = Assert.Throws<RpcException>(() =>
                setup.UpdatePasswordViaGrpc(
                    PasswordManagementBuilder.UserId,
                    setup.Token,
                    PasswordManagementBuilder.InitialPassword,
                    PasswordManagementBuilder.WeakPassword));

            Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
        }

        [Fact]
        public void UpdatePassword_SameAsCurrentPassword_RejectsWithInvalidArgument()
        {
            using PasswordManagementBuilder setup = new PasswordManagementBuilder();

            RpcException exception = Assert.Throws<RpcException>(() =>
                setup.UpdatePasswordViaGrpc(
                    PasswordManagementBuilder.UserId,
                    setup.Token,
                    PasswordManagementBuilder.InitialPassword,
                    PasswordManagementBuilder.InitialPassword));

            Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
        }

        [Fact]
        public void UpdatePassword_GuestSession_RejectsWithPermissionDenied()
        {
            using PasswordManagementBuilder setup = new PasswordManagementBuilder();

            RpcException exception = Assert.Throws<RpcException>(() =>
                setup.UpdatePasswordViaGrpc(
                    PasswordManagementBuilder.GuestId,
                    setup.GuestToken,
                    PasswordManagementBuilder.InitialPassword,
                    PasswordManagementBuilder.ReplacementPassword));

            Assert.Equal(StatusCode.PermissionDenied, exception.StatusCode);
        }

        [Fact]
        public void UpdatePassword_ForeignUserId_RejectsWithUnauthenticated()
        {
            using PasswordManagementBuilder setup = new PasswordManagementBuilder();

            RpcException exception = Assert.Throws<RpcException>(() =>
                setup.UpdatePasswordViaGrpc(
                    PasswordManagementBuilder.OtherUserId,
                    setup.Token,
                    PasswordManagementBuilder.InitialPassword,
                    PasswordManagementBuilder.ReplacementPassword));

            Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
        }

        [Fact]
        public void PasswordReset_ValidFlow_UpdatesPasswordAndAllowsLogin()
        {
            using PasswordManagementBuilder setup = new PasswordManagementBuilder();

            string resetId = setup.ResetManager.BeginReset(PasswordManagementBuilder.Email);
            setup.ResetManager.ConfirmReset(
                resetId,
                setup.EmailDelivery.LatestResetCode,
                PasswordManagementBuilder.ReplacementPassword);

            User authenticated = setup.AuthRepository.AuthenticateUser(
                PasswordManagementBuilder.Username,
                PasswordManagementBuilder.ReplacementPassword);
            Assert.Equal(PasswordManagementBuilder.UserId, authenticated.IdUser);
            Assert.Throws<InvalidCredentialsException>(() =>
                setup.AuthRepository.AuthenticateUser(
                    PasswordManagementBuilder.Username,
                    PasswordManagementBuilder.InitialPassword));
        }

        [Fact]
        public void PasswordReset_UppercaseEmail_FindsAccountAndDeliversCode()
        {
            using PasswordManagementBuilder setup = new PasswordManagementBuilder();

            string resetId = setup.ResetManager.BeginReset(UppercaseEmail);

            Assert.False(string.IsNullOrWhiteSpace(resetId));
            Assert.Equal(PasswordManagementBuilder.Email, setup.EmailDelivery.LatestRecipient);
        }

        [Fact]
        public void PasswordReset_MalformedEmail_ThrowsArgumentException()
        {
            using PasswordManagementBuilder setup = new PasswordManagementBuilder();

            Assert.Throws<ArgumentException>(() => setup.ResetManager.BeginReset(MalformedEmail));
        }

        [Fact]
        public void PasswordReset_UnknownEmail_ThrowsProfileNotFoundException()
        {
            using PasswordManagementBuilder setup = new PasswordManagementBuilder();

            Assert.Throws<ProfileNotFoundException>(() => setup.ResetManager.BeginReset(UnknownEmail));
        }

        [Fact]
        public void PasswordReset_WeakNewPassword_DoesNotConsumePendingReset()
        {
            using PasswordManagementBuilder setup = new PasswordManagementBuilder();
            string resetId = setup.ResetManager.BeginReset(PasswordManagementBuilder.Email);
            string deliveredCode = setup.EmailDelivery.LatestResetCode;

            Assert.Throws<ArgumentException>(() =>
                setup.ResetManager.ConfirmReset(resetId, deliveredCode, PasswordManagementBuilder.WeakPassword));

            setup.ResetManager.ConfirmReset(resetId, deliveredCode, PasswordManagementBuilder.ReplacementPassword);
            User authenticated = setup.AuthRepository.AuthenticateUser(
                PasswordManagementBuilder.Username,
                PasswordManagementBuilder.ReplacementPassword);
            Assert.Equal(PasswordManagementBuilder.UserId, authenticated.IdUser);
        }

        [Fact]
        public void PasswordReset_ExpiredCode_RejectsConfirmation()
        {
            using PasswordManagementBuilder setup = new PasswordManagementBuilder();
            string resetId = setup.ResetManager.BeginReset(PasswordManagementBuilder.Email);
            string deliveredCode = setup.EmailDelivery.LatestResetCode;
            setup.Time.Advance(TimeSpan.FromMinutes(PasswordManagementBuilder.ResetExpiryMinutes));

            Assert.Throws<RegistrationVerificationException>(() =>
                setup.ResetManager.ConfirmReset(resetId, deliveredCode, PasswordManagementBuilder.ReplacementPassword));
        }

        [Fact]
        public void PasswordReset_FiveFailedAttempts_LocksOutValidCode()
        {
            using PasswordManagementBuilder setup = new PasswordManagementBuilder();
            string resetId = setup.ResetManager.BeginReset(PasswordManagementBuilder.Email);
            string deliveredCode = setup.EmailDelivery.LatestResetCode;

            for (int attempt = 0; attempt < PasswordManagementBuilder.MaximumFailedAttempts; attempt++)
            {
                Assert.Throws<RegistrationVerificationException>(() =>
                    setup.ResetManager.ConfirmReset(
                        resetId,
                        PasswordManagementBuilder.InvalidCode,
                        PasswordManagementBuilder.ReplacementPassword));
            }

            Assert.Throws<RegistrationVerificationException>(() =>
                setup.ResetManager.ConfirmReset(resetId, deliveredCode, PasswordManagementBuilder.ReplacementPassword));
        }

        [Fact]
        public void PasswordReset_ResendDuringCooldown_RejectsRequest()
        {
            using PasswordManagementBuilder setup = new PasswordManagementBuilder();
            string resetId = setup.ResetManager.BeginReset(PasswordManagementBuilder.Email);
            setup.Time.Advance(TimeSpan.FromSeconds(PasswordManagementBuilder.BeforeCooldownSeconds));

            Assert.Throws<RegistrationVerificationException>(() => setup.ResetManager.ResendReset(resetId));
        }

        [Fact]
        public void PasswordReset_ResendAfterCooldown_IssuesReplacementCode()
        {
            using PasswordManagementBuilder setup = new PasswordManagementBuilder();
            string resetId = setup.ResetManager.BeginReset(PasswordManagementBuilder.Email);
            setup.Time.Advance(TimeSpan.FromSeconds(PasswordManagementBuilder.ResendCooldownSeconds));

            setup.ResetManager.ResendReset(resetId);

            Assert.Equal(ExpectedResendCount, setup.EmailDelivery.SentResetCount);
            setup.ResetManager.ConfirmReset(
                resetId,
                setup.EmailDelivery.LatestResetCode,
                AlternatePassword);
            User authenticated = setup.AuthRepository.AuthenticateUser(
                PasswordManagementBuilder.Username,
                AlternatePassword);
            Assert.Equal(PasswordManagementBuilder.UserId, authenticated.IdUser);
        }

        [Fact]
        public void PasswordReset_ActiveRequestAlreadyExists_RejectsDuplicate()
        {
            using PasswordManagementBuilder setup = new PasswordManagementBuilder();
            setup.ResetManager.BeginReset(PasswordManagementBuilder.Email);

            Assert.Throws<RegistrationVerificationException>(() =>
                setup.ResetManager.BeginReset(PasswordManagementBuilder.Email));
        }

        [Fact]
        public void PasswordReset_AfterExpiration_AllowsNewRequestForSameEmail()
        {
            using PasswordManagementBuilder setup = new PasswordManagementBuilder();
            setup.ResetManager.BeginReset(PasswordManagementBuilder.Email);
            setup.Time.Advance(TimeSpan.FromMinutes(PasswordManagementBuilder.ResetExpiryMinutes));

            string secondResetId = setup.ResetManager.BeginReset(PasswordManagementBuilder.Email);

            Assert.False(string.IsNullOrWhiteSpace(secondResetId));
            Assert.Equal(ExpectedResendCount, setup.EmailDelivery.SentResetCount);
        }

        [Fact]
        public void ProfileViewModel_EditPassword_ClosesOtherEditorsAndClearsOnCancel()
        {
            ProfileTestApplication.Run(() =>
            {
                using ProfileWindowBuilder setup = new ProfileWindowBuilder();
                setup.ViewModel.ChooseAvatarCommand.Execute(null);

                setup.ViewModel.EditPasswordCommand.Execute(null);
                setup.ViewModel.CurrentPassword = PasswordManagementBuilder.InitialPassword;
                setup.ViewModel.NewPassword = PasswordManagementBuilder.ReplacementPassword;
                setup.ViewModel.ConfirmNewPassword = PasswordManagementBuilder.ReplacementPassword;

                Assert.True(setup.ViewModel.IsEditingPassword);
                Assert.False(setup.ViewModel.IsChoosingAvatar);

                setup.ViewModel.CancelEditCommand.Execute(null);

                Assert.False(setup.ViewModel.IsEditingPassword);
                Assert.Equal(string.Empty, setup.ViewModel.CurrentPassword);
                Assert.Equal(string.Empty, setup.ViewModel.NewPassword);
                Assert.Equal(string.Empty, setup.ViewModel.ConfirmNewPassword);
            });
        }

        [Fact]
        public void ProfileViewModel_SavePassword_ValidatesInputsBeforeRpc()
        {
            ProfileTestApplication.Run(() =>
            {
                using ProfileWindowBuilder setup = new ProfileWindowBuilder();

                setup.ViewModel.SavePasswordCommand.Execute(null);
                Assert.Equal(ClientStrings.ProfilePasswordEmptyError, setup.ViewModel.StatusMessage);

                setup.ViewModel.CurrentPassword = PasswordManagementBuilder.InitialPassword;
                setup.ViewModel.NewPassword = PasswordManagementBuilder.ReplacementPassword;
                setup.ViewModel.ConfirmNewPassword = MismatchedPassword;
                setup.ViewModel.SavePasswordCommand.Execute(null);
                Assert.Equal(ClientStrings.ProfilePasswordMismatchError, setup.ViewModel.StatusMessage);

                setup.ViewModel.NewPassword = PasswordManagementBuilder.WeakPassword;
                setup.ViewModel.ConfirmNewPassword = PasswordManagementBuilder.WeakPassword;
                setup.ViewModel.SavePasswordCommand.Execute(null);
                Assert.Equal(ClientStrings.ProfilePasswordPolicyError, setup.ViewModel.StatusMessage);

                setup.ViewModel.NewPassword = PasswordManagementBuilder.InitialPassword;
                setup.ViewModel.ConfirmNewPassword = PasswordManagementBuilder.InitialPassword;
                setup.ViewModel.SavePasswordCommand.Execute(null);
                Assert.Equal(ClientStrings.ProfilePasswordSameError, setup.ViewModel.StatusMessage);
            });
        }

        [Fact]
        public void ForgotPasswordViewModel_ValidatesEmailAndResetInputs()
        {
            Assert.False(ClientForgotPasswordViewModel.IsValidEmail(MalformedEmail));
            Assert.True(ClientForgotPasswordViewModel.IsValidEmail(PasswordManagementBuilder.Email));

            Assert.Equal(
                ClientStrings.ProfilePasswordEmptyError,
                ClientForgotPasswordViewModel.ValidateResetInputs(string.Empty, PasswordManagementBuilder.ReplacementPassword, PasswordManagementBuilder.ReplacementPassword));
            Assert.Equal(
                ClientStrings.ProfilePasswordMismatchError,
                ClientForgotPasswordViewModel.ValidateResetInputs(ValidSampleCode, PasswordManagementBuilder.ReplacementPassword, MismatchedPassword));
            Assert.Equal(
                ClientStrings.ProfilePasswordPolicyError,
                ClientForgotPasswordViewModel.ValidateResetInputs(ValidSampleCode, PasswordManagementBuilder.WeakPassword, PasswordManagementBuilder.WeakPassword));
            Assert.Equal(
                string.Empty,
                ClientForgotPasswordViewModel.ValidateResetInputs(ValidSampleCode, PasswordManagementBuilder.ReplacementPassword, PasswordManagementBuilder.ReplacementPassword));
        }
    }
}
