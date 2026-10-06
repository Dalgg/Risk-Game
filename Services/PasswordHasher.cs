using System;
using System.Security.Cryptography;
using System.Text;

namespace RiskGame.Services
{
    public static class PasswordHasher
    {
        private const string Prefix = "PBKDF2-SHA256";
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 600_000;

        public static string Hash(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

            return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static (bool IsValid, bool NeedsRehash) Verify(string password, string storedValue)
        {
            if (!storedValue.StartsWith(Prefix + "$", StringComparison.Ordinal))
            {
                bool legacyMatch = CryptographicOperations.FixedTimeEquals( Encoding.UTF8.GetBytes(password), Encoding
                    .UTF8.GetBytes(storedValue));
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

            byte[] actual = Rfc2898DeriveBytes.Pbkdf2( password, salt, iterations, HashAlgorithmName.SHA256
                , expected.Length);

            bool isValid = CryptographicOperations.FixedTimeEquals(actual, expected);

            return (isValid, isValid && iterations < Iterations);
        }
    }
}