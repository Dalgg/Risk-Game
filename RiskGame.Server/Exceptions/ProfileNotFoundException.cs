namespace RiskGame.Exceptions
{
    public sealed class ProfileNotFoundException : DataOperationException
    {
        public ProfileNotFoundException(string message) : base(message)
        {
        }
    }
}
