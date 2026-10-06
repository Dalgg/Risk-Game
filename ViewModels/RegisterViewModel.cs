using System;
using System.Linq;
using System.Text.RegularExpressions;
using RiskGame.Models;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public class RegisterViewModel : ViewModelBase
    {
        private const int MaxEmailLength = 100;
        private static readonly Regex EmailRegex = new Regex( @"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$"
            , RegexOptions.Compiled);
 
        private static readonly Regex UsernameRegex = new Regex( @"^[A-Za-z0-9._\-]{3,50}$"
            , RegexOptions.Compiled);
 
        public RegisterResult RegisterUser(string email, string username, string password)
        {
            email = email.Trim();
            username = username.Trim();
 
            if (email.Length == 0 || username.Length == 0 || password.Length == 0)
            {
                return RegisterResult.EmptyFields;
            }
 
            if (email.Length > MaxEmailLength || !EmailRegex.IsMatch(email))
            {
                return RegisterResult.InvalidEmail;
            }
 
            if (!UsernameRegex.IsMatch(username))
            {
                return RegisterResult.InvalidUsername;
            }
 
            if (!PasswordPolicy.PasswordIsValid(password))
            {
                return RegisterResult.WeakPassword;
            }
 
            using (RiskGameContext context = new RiskGameContext())
            {
                if (context.Users.Any(u => u.Username == username))
                {
                    return RegisterResult.UsernameExists;
                }
 
                if (context.Users.Any(u => u.Email == email))
                {
                    return RegisterResult.EmailExists;
                }
 
                User newUser = new User
                {
                    Email = email, Username = username, PasswordHash = PasswordHasher.Hash(password), Nickname 
                        = username, RegistrationDate = DateTime.Now
                };
                context.Users.Add(newUser);
                context.SaveChanges();
 
                return RegisterResult.Success;
            }
        }
    }
}