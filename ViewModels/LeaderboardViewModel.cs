using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Resources;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public sealed record PlayerScoreView
    {
        public int Rank { get; init; }

        public string Username { get; init; } = string.Empty;

        public int TotalScore { get; init; }
    }

    public class LeaderboardViewModel : ViewModelBase
    {
        private const int TopPlayersLimit = 10;

        private bool _isDescending;


        public LeaderboardViewModel()
        {
            // Initial load is now handled safely by the view or caught internally.
        }

        public ObservableCollection<PlayerScoreView> Players { get; } = new ObservableCollection<PlayerScoreView>();

        public void LoadGlobalScores()
        {
            try
            {
                using (RiskGameContext context = new RiskGameContext())
                {
                    IQueryable<PlayerScoreView> query = context.Users.Select(user => new PlayerScoreView
                        {
                            Username = user.Username,
                            TotalScore = user.MatchResults.Sum(matchResult => matchResult.FinalScore ?? 0)
                        });
    
                    query = _isDescending ? query.OrderByDescending(score => score.TotalScore) 
                        : query.OrderBy(score => score.TotalScore);
    
                    List<PlayerScoreView> topScores = query.Take(TopPlayersLimit).ToList();
    
                    Players.Clear();
                    for (int index = 0; index < topScores.Count; index++)
                    {
                        topScores[index] = topScores[index] with { Rank = index + 1 };
                        Players.Add(topScores[index]);
                    }
                }
            }
            catch (DbException ex)
            {
                JsonLogger.Error(ex, "Connection error (DbException) while loading leaderboard.");
                throw new DataOperationException(Strings.LeaderboardErrorDatabase, ex);
            }
        }

        public void ToggleSortDirection()
        {
            _isDescending = !_isDescending;
            LoadGlobalScores();
        }
    }
}
