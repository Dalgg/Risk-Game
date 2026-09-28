using RiskGame.Models;
using System.Collections.Generic;
using System.Linq;

namespace RiskGame.ViewModels
{
    // Estructura auxiliar para mostrar en la vista
    public class PlayerScoreView
    {
        public string Username { get; set; }
        public int TotalScore { get; set; }
    }

    public class LeaderboardViewModel : ViewModelBase
    {
        public List<PlayerScoreView> GetTopPlayers(int limit = 10)
        {
            using (var context = new RiskGameContext())
            {
                var topPlayers = context.Users.Select(u => new PlayerScoreView
                    {
                        Username = u.Username,
                        TotalScore = u.MatchResults.Sum(m => m.FinalScore ?? 0) 
                    })
                    .OrderByDescending(p => p.TotalScore)
                    .Take(limit)
                    .ToList();
                return topPlayers;
            }
        }
    }
}