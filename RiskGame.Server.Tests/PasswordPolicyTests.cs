using RiskGame.Services;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class PasswordPolicyTests
    {
        private const string StrongSampleInput = "Alpha123!";
        private const string ExactMinimumLengthInput = "Abcd123!";
        private const string TooShortInput = "Abc12!";
        private const string MissingUppercaseInput = "alpha123!";
        private const string MissingLowercaseInput = "ALPHA123!";
        private const string MissingDigitInput = "AlphaTest!";
        private const string MissingSymbolInput = "Alpha1234";
        private const string WhitespaceOnlySymbolInput = "Alpha 1234";

        [Fact]
        public void PasswordIsValid_StrongSample_ReturnsTrue()
        {
            bool isValid = PasswordPolicy.PasswordIsValid(StrongSampleInput);

            Assert.True(isValid);
        }

        [Fact]
        public void PasswordIsValid_ExactMinimumLength_ReturnsTrue()
        {
            bool isValid = PasswordPolicy.PasswordIsValid(ExactMinimumLengthInput);

            Assert.True(isValid);
        }

        [Fact]
        public void PasswordIsValid_ShorterThanMinimum_ReturnsFalse()
        {
            bool isValid = PasswordPolicy.PasswordIsValid(TooShortInput);

            Assert.False(isValid);
        }

        [Fact]
        public void PasswordIsValid_MissingUppercase_ReturnsFalse()
        {
            bool isValid = PasswordPolicy.PasswordIsValid(MissingUppercaseInput);

            Assert.False(isValid);
        }

        [Fact]
        public void PasswordIsValid_MissingLowercase_ReturnsFalse()
        {
            bool isValid = PasswordPolicy.PasswordIsValid(MissingLowercaseInput);

            Assert.False(isValid);
        }

        [Fact]
        public void PasswordIsValid_MissingDigit_ReturnsFalse()
        {
            bool isValid = PasswordPolicy.PasswordIsValid(MissingDigitInput);

            Assert.False(isValid);
        }

        [Fact]
        public void PasswordIsValid_MissingSpecialCharacter_ReturnsFalse()
        {
            bool isValid = PasswordPolicy.PasswordIsValid(MissingSymbolInput);

            Assert.False(isValid);
        }

        [Fact]
        public void PasswordIsValid_WhitespaceInsteadOfSymbol_ReturnsFalse()
        {
            bool isValid = PasswordPolicy.PasswordIsValid(WhitespaceOnlySymbolInput);

            Assert.False(isValid);
        }
    }
}
