using RiskGame.Services;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class PasswordHasherTests
    {
        private const string ExpectedHashPrefix = "PBKDF2-SHA256$";
        private const string SampleInput = "Alpha123!";
        private const string DifferentInput = "Beta987?";
        private const string MalformedSegmentHash = "PBKDF2-SHA256$600000$onlyThreeParts";

        [Fact]
        public void Hash_ValidInput_StartsWithExpectedPrefix()
        {
            string hashedValue = PasswordHasher.Hash(SampleInput);

            Assert.StartsWith(ExpectedHashPrefix, hashedValue);
        }

        [Fact]
        public void Verify_MatchingInput_ReturnsValid()
        {
            string hashedValue = PasswordHasher.Hash(SampleInput);

            PasswordVerificationResult verificationResult = PasswordHasher.Verify(SampleInput, hashedValue);

            Assert.True(verificationResult.IsValid);
        }

        [Fact]
        public void Verify_DifferentInput_ReturnsInvalid()
        {
            string hashedValue = PasswordHasher.Hash(SampleInput);

            PasswordVerificationResult verificationResult = PasswordHasher.Verify(DifferentInput, hashedValue);

            Assert.False(verificationResult.IsValid);
        }

        [Fact]
        public void Verify_MalformedSegmentCount_ReturnsInvalid()
        {
            PasswordVerificationResult verificationResult = PasswordHasher.Verify(SampleInput, MalformedSegmentHash);

            Assert.False(verificationResult.IsValid);
        }
    }
}
