using System;
using System.Linq;
using RiskGame.Models;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    /// <summary>
    /// Modelo de vista responsable de crear cuentas de jugador en la base de datos.
    /// </summary>
    public class RegisterViewModel : ViewModelBase
    {
        /// <summary>
        /// Registra un nuevo jugador, siempre que ni su correo ni su nombre de usuario estén
        /// previamente asociados a otra cuenta.
        /// </summary>
        /// <param name="email">Correo electrónico único del jugador.</param>
        /// <param name="username">Nombre de usuario único elegido por el jugador.</param>
        /// <param name="password">Contraseña en texto plano, que se almacenaría hasheada.</param>
        /// <returns>
        /// El resultado del registro: un valor booleano que indica si la cuenta se creó y el mensaje
        /// destinado al usuario que describe el motivo de un eventual rechazo.
        /// </returns>
        public (bool Success, string Message) RegisterUser(string email, string username, string password)
        {
            using (RiskGameContext context = new RiskGameContext())
            {
                bool userExists = context.Users.Any(u => u.Email == email || u.Username == username);

                if (userExists)
                {
                    return (false, "El usuario o correo ya está registrado.");
                }

                User newUser = new User
                {
                    Email = email,
                    Username = username,
                    PasswordHash = PasswordHasher.Hash(password),
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