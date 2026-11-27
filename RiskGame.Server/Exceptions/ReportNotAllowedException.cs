namespace RiskGame.Exceptions
{
    public sealed class ReportNotAllowedException : DataOperationException
    {
        public ReportNotAllowedException(string message) : base(message)
        {
        }
    }
}
