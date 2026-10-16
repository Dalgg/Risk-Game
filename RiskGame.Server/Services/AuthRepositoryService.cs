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
            @"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$",
            RegexOptions.Compiled);
        private static readonly Regex UsernameRegex = new Regex(
            @"^[A-Za-z0-9._\-]{3,50}$",
            RegexOptions.Compiled);

        public User? AuthenticateUser(string username, string password)
        {
            try
            {
                using (RiskGameContext context = new RiskGameContext())
                {
                    User? user = context.Users.FirstOrDefault(candidate => candidate.Username == username);
                    if (user == null)
                    {
                        return null;
                    }

                    (bool isValid, bool needsRehash) = PasswordHasher.Verify(password, user.PasswordHash);
                    if (!isValid)
                    {
                        return null;
                    }

                    if (needsRehash)
                    {
                        user.PasswordHash = PasswordHasher.Hash(password);
                        context.SaveChanges();
                    }

                    return user;
                }
            }
            catch (DbUpdateException ex)
            {
                JsonLogger.Error(ex, "Persistence error (DbUpdateException) during login for user {0}.", username);
                throw new DataOperationException("Database error occurred while updating user credentials.", ex);
            }
            catch (DbException ex)
            {
                JsonLogger.Error(ex, "Connection error (DbException) during login for user {0}.", username);
                throw new DataOperationException("Database connection error occurred during login.", ex);
            }
        }

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

                    User newUser = new User
                    {
                        Email = normalizedEmail,
                        Username = normalizedUsername,
                        PasswordHash = PasswordHasher.Hash(password),
                        Nickname = normalizedUsername,
                        RegistrationDate = DateTime.Now
                    };

                    context.Users.Add(newUser);
                    context.SaveChanges();

                    return RegisterResult.Success;
                }
            }
            catch (DbUpdateException ex)
            {
                JsonLogger.Error(ex, "Persistence error (DbUpdateException) while registering user {0}.", normalizedUsername);
                throw new DataOperationException("A data conflict occurred while registering the account.", ex);
            }
            catch (DbException ex)
            {
                JsonLogger.Error(ex, "Connection error (DbException) while registering user {0}.", normalizedUsername);
                throw new DataOperationException("Could not connect to the database while registering the account.", ex);
            }
        }
    }
}
