using System.Linq;
using RiskGame.Models;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    /// <summary>
    /// Modelo de vista responsable de autenticar a un jugador contra la base de datos.
    /// </summary>
    public class LoginViewModel : ViewModelBase
    {
        /// <summary>
        /// Valida las credenciales indicadas y devuelve el usuario correspondiente.
        /// Si la contraseña almacenada usa el formato actual de hash pero tiene menos iteraciones
        /// que las configuradas, se reescribe de forma transparente durante este proceso.
        /// </summary>
        /// <param name="username">Nombre de usuario con el que se intenta iniciar sesión.</param>
        /// <param name="password">Contraseña en texto plano tecleada por el jugador.</param>
        /// <returns>
        /// El usuario autenticado, o <c>null</c> si el nombre de usuario no existe o la contraseña no coincide.
        /// </returns>
        public User? AuthenticateUser(string username, string password)
        {
            using (RiskGameContext context = new RiskGameContext())
            {
                User? user = context.Users.FirstOrDefault(u => u.Username == username);
                if (user == null)
                {
                    return null;
                }

                (bool isValid, bool needsRehash) = PasswordHasher.Verify(password, user.PasswordHash);
                if (!isValid)
                {
                    return null;
                }

                // Las cuentas creadas antes de adoptar el hash se migran al formato vigente
                // para que el resto del sistema nunca tenga que comparar contraseñas en texto plano.
                if (needsRehash)
                {
                    user.PasswordHash = PasswordHasher.Hash(password);
                    context.SaveChanges();
                }

                return user;
            }
        }
    }
}