using System;
using System.Security.Cryptography;
using System.Text;

namespace RiskGame.Services
{
    public sealed record PasswordVerificationResult(bool IsValid, bool NeedsRehash);

    public static class PasswordHasher
    {
        private const string Prefix = "PBKDF2-SHA256";
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 600_000;
        private const char Delimiter = '$';
        private const int ExpectedSegmentCount = 4;

        public static string Hash(string password)
        {
            byte[] saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] derivedHashBytes = Rfc2898DeriveBytes.Pbkdf2(
                password,
                saltBytes,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            return $"{Prefix}${Iterations}${Convert.ToBase64String(saltBytes)}${Convert.ToBase64String(derivedHashBytes)}";
        }

        public static PasswordVerificationResult Verify(string password, string storedValue)
        {
            if (!storedValue.StartsWith(Prefix + Delimiter, StringComparison.Ordinal))
            {
                bool legacyMatch = CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(password),
                    Encoding.UTF8.GetBytes(storedValue));
                return new PasswordVerificationResult(legacyMatch, legacyMatch);
            }

            string[] hashSegments = storedValue.Split(Delimiter);
            if (hashSegments.Length != ExpectedSegmentCount)
            {
                return new PasswordVerificationResult(false, false);
            }

            int parsedIterations = ParseIterationsOrInvalid(hashSegments[1]);
            if (parsedIterations <= 0)
            {
                return new PasswordVerificationResult(false, false);
            }

            byte[] saltBytes;
            byte[] expectedHashBytes;
            try
            {
                saltBytes = Convert.FromBase64String(hashSegments[2]);
                expectedHashBytes = Convert.FromBase64String(hashSegments[3]);
            }
            catch (FormatException formatException)
            {
                JsonLogger.Error(formatException, "Invalid Base64 segment in stored password hash.");
                return new PasswordVerificationResult(false, false);
            }

            byte[] actualHashBytes = Rfc2898DeriveBytes.Pbkdf2(
                password,
                saltBytes,
                parsedIterations,
                HashAlgorithmName.SHA256,
                expectedHashBytes.Length);
            bool isPasswordValid = CryptographicOperations.FixedTimeEquals(actualHashBytes, expectedHashBytes);
            bool requiresRehash = isPasswordValid && parsedIterations < Iterations;
            return new PasswordVerificationResult(isPasswordValid, requiresRehash);
        }

        private static int ParseIterationsOrInvalid(string iterationsSegment)
        {
            try
            {
                return int.Parse(iterationsSegment);
            }
            catch (FormatException formatException)
            {
                JsonLogger.Error(formatException, "Invalid iteration count format in password hash.");
                return -1;
            }
            catch (OverflowException overflowException)
            {
                JsonLogger.Error(overflowException, "Iteration count overflow in password hash.");
                return -1;
            }
        }
    }
}