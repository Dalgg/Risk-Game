namespace RiskGame.Exceptions
{
    public sealed class RegistrationVerificationException : DataOperationException
    {
        public RegistrationVerificationException(string message) : base(message)
        {
        }
    }
}
