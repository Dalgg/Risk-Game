using RiskGame.Models;
using System;
using System.Linq;

namespace RiskGame.ViewModels
{
    public class RegisterViewModel : ViewModelBase
    {
        public (bool Success, string Message) RegisterUser(string email, string username, string password)
        {
            using (var context = new RiskGameContext())
            {
                bool userExists = context.Users.Any(u => u.Email == email || u.Username == username);
                
                if (userExists)
                {
                    return (false, "El usuario o correo ya está registrado.");
                }
                var newUser = new User
                {
                    Email = email,
                    Username = username,
                    PasswordHash = password, 
                    Nickname = username, 
                    RegistrationDate = DateTime.Now
                };
                context.Users.Add(newUser);
                context.SaveChanges();

                return (true, "Cuenta creada exitosamente.");
            }
        }
    }
}