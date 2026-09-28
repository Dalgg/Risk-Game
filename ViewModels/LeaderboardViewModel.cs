using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RiskGame.Models;

namespace RiskGame.ViewModels
{
    // Estructura de datos para la tabla
    public class PlayerScoreView
    {
        public int Rank { get; set; }
        public string Username { get; set; } = string.Empty;
        public int TotalScore { get; set; }
    }

    public class LeaderboardViewModel : ViewModelBase
    {
        // Colección observable enlazada a la tabla XAML
        public ObservableCollection<PlayerScoreView> Players { get; } = new ObservableCollection<PlayerScoreView>();
        
        private bool _isDescending = true;

        public LeaderboardViewModel()
        {
            LoadGlobalScores();
        }

        public void LoadGlobalScores()
        {
            using (RiskGameContext context = new RiskGameContext())
            {
                // 1. Consulta SQL Server a través de EF Core
                IQueryable<PlayerScoreView> query = context.Users
                    .Select(u => new PlayerScoreView
                    {
                        Username = u.Username,
                        TotalScore = u.MatchResults.Sum(m => m.FinalScore ?? 0)
                    });

                // 2. Aplica el filtro de ordenamiento
                if (_isDescending)
                    query = query.OrderByDescending(p => p.TotalScore);
                else
                    query = query.OrderBy(p => p.TotalScore);

                // 3. Limita los resultados a los 10 mejores
                List<PlayerScoreView> result = query.Take(10).ToList();

                // 4. Actualiza la colección de la interfaz asignando los rangos
                Players.Clear();
                for (int i = 0; i < result.Count; i++)
                {
                    result[i].Rank = i + 1;
                    Players.Add(result[i]);
                }
            }
        }

        public void ToggleSortDirection()
        {
            _isDescending = !_isDescending;
            LoadGlobalScores();
        }
    }
}
