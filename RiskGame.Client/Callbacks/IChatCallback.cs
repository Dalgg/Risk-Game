using RiskGame.Contracts.Chat;

namespace RiskGame.Callbacks
{
    public interface IChatCallback
    {
        void MessageReceived(ChatMessage incomingMessage);
    }
}
