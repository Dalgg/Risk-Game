using System.Text.RegularExpressions;
using Grpc.Core;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Resources;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public class RegisterViewModel : ViewModelBase
    {
        private const int MaxEmailLength = 100;
        private static readonly Regex EmailRegex = new Regex(
            @"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$",
            RegexOptions.Compiled);
        private static readonly Regex UsernameRegex = new Regex(
            @"^[A-Za-z0-9._\-]{3,50}$",
            RegexOptions.Compiled);

        public RegisterResult RegisterUser(string email, string username, string password)
        {
            string normalizedEmail = email.Trim();
            string normalizedUsername = username.Trim();

            if (normalizedEmail.Length == 0 || normalizedUsername.Length == 0 || password.Length == 0)
            {
                return RegisterResult.EmptyFields;
            }

            if (normalizedEmail.Length > MaxEmailLength || !EmailRegex.IsMatch(normalizedEmail))
            {
                return RegisterResult.InvalidEmail;
            }

            if (!UsernameRegex.IsMatch(normalizedUsername))
            {
                return RegisterResult.InvalidUsername;
            }

            if (!PasswordPolicy.PasswordIsValid(password))
            {
                return RegisterResult.WeakPassword;
            }

            try
            {
                _ = GrpcChannelProvider.GetChannel();
                return RegisterResult.Success;
            }
            catch (RpcException ex)
            {
                JsonLogger.Error(ex, "gRPC communication error (RpcException) while registering user {0}.", normalizedUsername);
                throw new DataOperationException(Strings.RegisterErrorConnectionFailed, ex);
            }
        }
    }
}
