namespace RiskGame.Exceptions
{
    public sealed class GuestNotAllowedException : DataOperationException
    {
        public GuestNotAllowedException(string message) : base(message)
        {
        }
    }
}
