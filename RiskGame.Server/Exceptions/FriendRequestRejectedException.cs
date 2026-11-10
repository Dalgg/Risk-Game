namespace RiskGame.Exceptions
{
    public sealed class FriendRequestRejectedException : DataOperationException
    {
        public FriendRequestRejectedException(string message) : base(message)
        {
        }
    }
}
