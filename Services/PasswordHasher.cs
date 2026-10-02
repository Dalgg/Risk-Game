using System;
using System.Security.Cryptography;
using System.Text;

namespace RiskGame.Services
{
    /// <summary>
    /// Hash de contraseñas con PBKDF2-HMAC-SHA256 (incluido en .NET, sin paquetes extra).
    /// Formato guardado: PBKDF2-SHA256$iteraciones$salt(base64)$hash(base64)
    /// </summary>
    public static class PasswordHasher
    {
        private const string Prefix = "PBKDF2-SHA256";
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 600_000;

        /// <summary>
        /// Genera un hash de la contraseña indicada usando PBKDF2-HMAC-SHA256 con un salt aleatorio.
        /// </summary>
        /// <param name="password">Contraseña en texto plano que se desea almacenar de forma segura.</param>
        /// <returns>
        /// La cadena con formato <c>PBKDF2-SHA256$iteraciones$salt(base64)$hash(base64)</c>.
        /// </returns>
        public static string Hash(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

            return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        /// <summary>
        /// Verifica la contraseña contra el valor almacenado. NeedsRehash es verdadero cuando el
        /// valor guardado debe actualizarse, ya sea porque corresponde a una contraseña antigua en
        /// texto plano o a un hash con menos iteraciones que las configuradas actualmente.
        /// </summary>
        /// <param name="password">Contraseña en texto plano que se desea verificar.</param>
        /// <param name="storedValue">Valor almacenado en la base de datos para la cuenta.</param>
        /// <returns>
        /// Una tupla que indica si la contraseña coincide y si el valor almacenado debe reescribirse.
        /// </returns>
        public static (bool IsValid, bool NeedsRehash) Verify(string password, string storedValue)
        {
            if (!storedValue.StartsWith(Prefix + "$", StringComparison.Ordinal))
            {
                // Usuario creado antes del hash: se compara en texto plano y se migra al iniciar sesión.
                bool legacyMatch = CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(password),
                    Encoding.UTF8.GetBytes(storedValue));

                return (legacyMatch, legacyMatch);
            }

            string[] parts = storedValue.Split('$');
            if (parts.Length != 4 || !int.TryParse(parts[1], out int iterations) || iterations <= 0)
            {
                return (false, false);
            }

            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return (false, false);
            }

            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expected.Length);

            bool isValid = CryptographicOperations.FixedTimeEquals(actual, expected);

            return (isValid, isValid && iterations < Iterations);
        }
    }
}