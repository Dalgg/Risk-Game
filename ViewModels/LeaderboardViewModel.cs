using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RiskGame.Models;

namespace RiskGame.ViewModels
{
    /// <summary>
    /// Fila inmutable de la tabla de clasificación, creada a partir del usuario y su puntuación acumulada.
    /// </summary>
    public sealed record PlayerScoreView
    {
        /// <summary>
        /// Posición en la tabla de clasificación. El primer lugar corresponde al índice 1.
        /// </summary>
        public int Rank { get; init; }

        /// <summary>
        /// Nombre de usuario del jugador clasificado.
        /// </summary>
        public string Username { get; init; } = string.Empty;

        /// <summary>
        /// Suma de las puntuaciones finales obtenidas por el jugador en todas sus partidas.
        /// </summary>
        public int TotalScore { get; init; }
    }

    /// <summary>
    /// Modelo de vista de la ventana de clasificación global. Consulta las partidas finalizadas
    /// y expone únicamente los diez mejores resultados a la interfaz.
    /// </summary>
    public class LeaderboardViewModel : ViewModelBase
    {
        /// <summary>
        /// Cantidad máxima de jugadores que se muestran en la tabla de clasificación.
        /// </summary>
        private const int TopPlayersLimit = 10;

        /// <summary>
        /// Indica si la tabla se ordena de mayor a menor puntuación cuando el valor es <c>true</c>.
        /// </summary>
        private bool _isDescending;

        /// <summary>
        /// Crea el modelo de vista y carga de inmediato la clasificación global.
        /// </summary>
        public LeaderboardViewModel()
        {
            LoadGlobalScores();
        }

        /// <summary>
        /// Colección observable enlazada a la tabla de la ventana de clasificación.
        /// </summary>
        public ObservableCollection<PlayerScoreView> Players { get; } = new ObservableCollection<PlayerScoreView>();

        /// <summary>
        /// Consulta la clasificación global, la ordena según la dirección activa y vuelve a publicar
        /// las diez primeras filas en <see cref="Players"/>.
        /// </summary>
        public void LoadGlobalScores()
        {
            using (RiskGameContext context = new RiskGameContext())
            {
                IQueryable<PlayerScoreView> query = context.Users
                    .Select(user => new PlayerScoreView
                    {
                        Username = user.Username,
                        TotalScore = user.MatchResults.Sum(matchResult => matchResult.FinalScore ?? 0)
                    });

                query = _isDescending
                    ? query.OrderByDescending(score => score.TotalScore)
                    : query.OrderBy(score => score.TotalScore);

                List<PlayerScoreView> topScores = query.Take(TopPlayersLimit).ToList();

                // El rango se asigna en memoria porque depende de la posición final y no de la consulta.
                Players.Clear();
                for (int index = 0; index < topScores.Count; index++)
                {
                    topScores[index] = topScores[index] with { Rank = index + 1 };
                    Players.Add(topScores[index]);
                }
            }
        }

        /// <summary>
        /// Invierte la dirección del ordenamiento y recarga la tabla de clasificación.
        /// </summary>
        public void ToggleSortDirection()
        {
            _isDescending = !_isDescending;
            LoadGlobalScores();
        }
    }
}