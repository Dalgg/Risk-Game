using RiskGame.Contracts.Match;

namespace RiskGame.Callbacks
{
    public interface IMatchCallback
    {
        void MatchUpdated(BoardState boardState);

        void MatchStreamStopped(string statusMessage);
    }
}
