using System;

namespace RiskGame.Models
{
    public sealed class PlayerReport
    {
        public int IdPlayerReport { get; set; }

        public int IdReporter { get; set; }

        public int IdReported { get; set; }

        public int IdLobby { get; set; }

        public DateTime ReportDate { get; set; }

        public bool IsProcessed { get; set; }

        public User Reporter { get; set; } = null!;

        public User Reported { get; set; } = null!;

        public Lobby Lobby { get; set; } = null!;
    }
}
