using System;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public class AuthRepositoryService
    {
        private const int MaxEmailLength = 100;
        private static readonly Regex EmailRegex = new Regex(
            @"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$", RegexOptions.Compiled);
        private static readonly Regex UsernameRegex = new Regex(
            @"^[A-Za-z0-9._\-]{3,50}$",
            RegexOptions.Compiled);

        public User AuthenticateUser(string username, string password)
        {
            try
            {
                using (RiskGameContext databaseContext = new RiskGameContext())
                {
                    User? user = databaseContext.Users.FirstOrDefault(
                        candidateUser => candidateUser.Username == username);
                    if (user == null)
                    {
                        throw new InvalidCredentialsException("Invalid username or password.");
                    }

                    PasswordVerificationResult verificationResult = PasswordHasher.Verify(password, user.PasswordHash);
                    if (!verificationResult.IsValid)
                    {
                        throw new InvalidCredentialsException("Invalid username or password.");
                    }

                    if (verificationResult.NeedsRehash)
                    {
                        user.PasswordHash = PasswordHasher.Hash(password);
                        databaseContext.SaveChanges();
                    }

                    return user;
                }
            }
            catch (DbUpdateException databaseUpdateException)
            {
                JsonLogger.Error(databaseUpdateException, "Persistence error (DbUpdateException) during login for user {0}.", username);
                throw new DataOperationException("Database error occurred while updating user credentials.", databaseUpdateException);
            }
            catch (DbException databaseException)
            {
                JsonLogger.Error(databaseException, "Connection error (DbException) during login for user {0}.", username);
                throw new DataOperationException("Database connection error occurred during login.", databaseException);
            }
        }

        public RegisterResult ValidateRegistration(string email, string username, string password)
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
                using (RiskGameContext context = new RiskGameContext())
                {
                    if (context.Users.Any(user => user.Username == normalizedUsername))
                    {
                        return RegisterResult.UsernameExists;
                    }

                    if (context.Users.Any(user => user.Email == normalizedEmail))
                    {
                        return RegisterResult.EmailExists;
                    }

                    return RegisterResult.Success;
                }
            }
            catch (DbException databaseException)
            {
                JsonLogger.Error(databaseException, "Connection error (DbException) while registering user {0}.", normalizedUsername);
                throw new DataOperationException("Could not connect to the database while registering the account.", databaseException);
            }
        }

        public RegisterResult CreateVerifiedUser(User verifiedUser)
        {
            try
            {
                using RiskGameContext databaseContext = new RiskGameContext();
                if (databaseContext.Users.Any(user => user.Username == verifiedUser.Username))
                {
                    return RegisterResult.UsernameExists;
                }
                if (databaseContext.Users.Any(user => user.Email == verifiedUser.Email))
                {
                    return RegisterResult.EmailExists;
                }
                databaseContext.Users.Add(verifiedUser);
                databaseContext.SaveChanges();
                return RegisterResult.Success;
            }
            catch (DbUpdateException databaseUpdateException)
            {
                JsonLogger.Error(databaseUpdateException, "Could not persist the verified account.");
                throw new DataOperationException("Could not persist the verified account.", databaseUpdateException);
            }
            catch (DbException databaseException)
            {
                JsonLogger.Error(databaseException, "Could not connect while saving the verified account.");
                throw new DataOperationException("Could not connect to save the verified account.", databaseException);
            }
        }
    }
}
