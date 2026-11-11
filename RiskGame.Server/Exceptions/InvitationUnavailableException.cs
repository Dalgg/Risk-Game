namespace RiskGame.Exceptions
{
    public sealed class InvitationUnavailableException : DataOperationException
    {
        public InvitationUnavailableException(string message) : base(message)
        {
        }
    }
}
