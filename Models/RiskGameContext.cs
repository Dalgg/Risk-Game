using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using RiskGame.Services;

namespace RiskGame.Models
{
    public partial class RiskGameContext : DbContext
    {
        public RiskGameContext()
        {
        }

        public RiskGameContext(DbContextOptions<RiskGameContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Friendship> Friendships { get; set; }

        public virtual DbSet<Lobby> Lobbies { get; set; }

        public virtual DbSet<LobbyInvitation> LobbyInvitations { get; set; }

        public virtual DbSet<Match> Matches { get; set; }

        public virtual DbSet<MatchResult> MatchResults { get; set; }

        public virtual DbSet<MatchState> MatchStates { get; set; }

        public virtual DbSet<PasswordRecovery> PasswordRecoveries { get; set; }

        public virtual DbSet<User> Users { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(DatabaseConfig.GetConnectionString());
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Friendship>(entity =>
            {
                entity.HasKey(e => e.IdFriendship).HasName("PK__Friendsh__CA7B9A4E09CC2E80");

                entity.ToTable("Friendship");

                entity.HasIndex(e => new { e.IdUser1, e.IdUser2 }, "UQ_Friendship_Pair").IsUnique();

                entity.Property(e => e.IdFriendship).HasColumnName("id_friendship");
                entity.Property(e => e.AcceptanceDate)
                    .HasColumnType("datetime")
                    .HasColumnName("acceptance_date");
                entity.Property(e => e.IdUser1).HasColumnName("id_user_1");
                entity.Property(e => e.IdUser2).HasColumnName("id_user_2");
                entity.Property(e => e.RequestDate)
                    .HasDefaultValueSql("(getdate())")
                    .HasColumnType("datetime")
                    .HasColumnName("request_date");
                entity.Property(e => e.Status)
                    .HasMaxLength(20)
                    .IsUnicode(false)
                    .HasDefaultValue("pending")
                    .HasColumnName("status");

                entity.HasOne(d => d.IdUser1Navigation).WithMany(p => p.FriendshipIdUser1Navigations)
                    .HasForeignKey(d => d.IdUser1)
                    .HasConstraintName("FK_Friendship_User1");

                entity.HasOne(d => d.IdUser2Navigation).WithMany(p => p.FriendshipIdUser2Navigations)
                    .HasForeignKey(d => d.IdUser2)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Friendship_User2");
            });

            modelBuilder.Entity<Lobby>(entity =>
            {
                entity.HasKey(e => e.IdLobby).HasName("PK__Lobby__1E093429F51AFA22");

                entity.ToTable("Lobby");

                entity.HasIndex(e => e.LobbyCode, "UQ_Lobby_ActiveCode")
                    .IsUnique()
                    .HasFilter("([closure_date] IS NULL)");

                entity.Property(e => e.IdLobby).HasColumnName("id_lobby");
                entity.Property(e => e.BoardColor)
                    .HasMaxLength(20)
                    .IsUnicode(false)
                    .HasDefaultValue("White")
                    .HasColumnName("board_color");
                entity.Property(e => e.ClosureDate)
                    .HasColumnType("datetime")
                    .HasColumnName("closure_date");
                entity.Property(e => e.CreationDate)
                    .HasDefaultValueSql("(getdate())")
                    .HasColumnType("datetime")
                    .HasColumnName("creation_date");
                entity.Property(e => e.LobbyCode)
                    .HasMaxLength(10)
                    .IsUnicode(false)
                    .HasColumnName("lobby_code");
                entity.Property(e => e.RulesVariant)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasDefaultValue("Standard")
                    .HasColumnName("rules_variant");
                entity.Property(e => e.TurnDuration)
                    .HasDefaultValue(60)
                    .HasColumnName("turn_duration");
                entity.Property(e => e.VisualTheme)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasDefaultValue("Classic")
                    .HasColumnName("visual_theme");
            });

            modelBuilder.Entity<LobbyInvitation>(entity =>
            {
                entity.HasKey(e => e.IdInvitation).HasName("PK__LobbyInv__A9817351CF24FC9B");

                entity.ToTable("LobbyInvitation");

                entity.Property(e => e.IdInvitation).HasColumnName("id_invitation");
                entity.Property(e => e.ExpirationDate)
                    .HasColumnType("datetime")
                    .HasColumnName("expiration_date");
                entity.Property(e => e.IdLobby).HasColumnName("id_lobby");
                entity.Property(e => e.IdRecipient).HasColumnName("id_recipient");
                entity.Property(e => e.IdSender).HasColumnName("id_sender");
                entity.Property(e => e.InvitationCode)
                    .HasMaxLength(20)
                    .IsUnicode(false)
                    .HasColumnName("invitation_code");
                entity.Property(e => e.RecipientEmail)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("recipient_email");
                entity.Property(e => e.SentDate)
                    .HasDefaultValueSql("(getdate())")
                    .HasColumnType("datetime")
                    .HasColumnName("sent_date");
                entity.Property(e => e.Status)
                    .HasMaxLength(20)
                    .IsUnicode(false)
                    .HasDefaultValue("pending")
                    .HasColumnName("status");

                entity.HasOne(d => d.IdLobbyNavigation).WithMany(p => p.LobbyInvitations)
                    .HasForeignKey(d => d.IdLobby)
                    .HasConstraintName("FK_Invitation_Lobby");

                entity.HasOne(d => d.IdRecipientNavigation).WithMany(p => p.LobbyInvitationIdRecipientNavigations)
                    .HasForeignKey(d => d.IdRecipient)
                    .HasConstraintName("FK_Invitation_Recipient");

                entity.HasOne(d => d.IdSenderNavigation).WithMany(p => p.LobbyInvitationIdSenderNavigations)
                    .HasForeignKey(d => d.IdSender)
                    .HasConstraintName("FK_Invitation_Sender");
            });

            modelBuilder.Entity<Match>(entity =>
            {
                entity.HasKey(e => e.IdMatch).HasName("PK__Match__7E9B03CBE70191B6");

                entity.ToTable("Match");

                entity.Property(e => e.IdMatch).HasColumnName("id_match");
                entity.Property(e => e.EndDate)
                    .HasColumnType("datetime")
                    .HasColumnName("end_date");
                entity.Property(e => e.EndReason)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("end_reason");
                entity.Property(e => e.IdLobby).HasColumnName("id_lobby");
                entity.Property(e => e.StartDate)
                    .HasDefaultValueSql("(getdate())")
                    .HasColumnType("datetime")
                    .HasColumnName("start_date");

                entity.HasOne(d => d.IdLobbyNavigation).WithMany(p => p.Matches)
                    .HasForeignKey(d => d.IdLobby)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Match_Lobby");
            });

            modelBuilder.Entity<MatchResult>(entity =>
            {
                entity.HasKey(e => e.IdMatchResult).HasName("PK__MatchRes__76211B0F1DA8EA9C");

                entity.ToTable("MatchResult");

                entity.HasIndex(e => new { e.IdMatch, e.IdUser }, "UQ_MatchResult_Seat").IsUnique();

                entity.Property(e => e.IdMatchResult).HasColumnName("id_match_result");
                entity.Property(e => e.FinalPosition).HasColumnName("final_position");
                entity.Property(e => e.FinalScore).HasColumnName("final_score");
                entity.Property(e => e.IdMatch).HasColumnName("id_match");
                entity.Property(e => e.IdUser).HasColumnName("id_user");

                entity.HasOne(d => d.IdMatchNavigation).WithMany(p => p.MatchResults)
                    .HasForeignKey(d => d.IdMatch)
                    .HasConstraintName("FK_MatchResult_Match");

                entity.HasOne(d => d.IdUserNavigation).WithMany(p => p.MatchResults)
                    .HasForeignKey(d => d.IdUser)
                    .HasConstraintName("FK_MatchResult_User");
            });

            modelBuilder.Entity<MatchState>(entity =>
            {
                entity.HasKey(e => e.IdMatch).HasName("PK__MatchSta__7E9B03CB144C5C14");

                entity.ToTable("MatchState");

                entity.Property(e => e.IdMatch)
                    .ValueGeneratedNever()
                    .HasColumnName("id_match");
                entity.Property(e => e.LastUpdated)
                    .HasDefaultValueSql("(getdate())")
                    .HasColumnType("datetime")
                    .HasColumnName("last_updated");
                entity.Property(e => e.StateData).HasColumnName("state_data");

                entity.HasOne(d => d.IdMatchNavigation).WithOne(p => p.MatchState)
                    .HasForeignKey<MatchState>(d => d.IdMatch)
                    .HasConstraintName("FK_MatchState_Match");
            });

            modelBuilder.Entity<PasswordRecovery>(entity =>
            {
                entity.HasKey(e => e.IdRecovery).HasName("PK__Password__3E78CED76E2271FF");

                entity.ToTable("PasswordRecovery");

                entity.Property(e => e.IdRecovery).HasColumnName("id_recovery");
                entity.Property(e => e.CreationDate)
                    .HasDefaultValueSql("(getdate())")
                    .HasColumnType("datetime")
                    .HasColumnName("creation_date");
                entity.Property(e => e.ExpirationDate)
                    .HasColumnType("datetime")
                    .HasColumnName("expiration_date");
                entity.Property(e => e.IdUser).HasColumnName("id_user");
                entity.Property(e => e.RecoveryCode)
                    .HasMaxLength(10)
                    .IsUnicode(false)
                    .HasColumnName("recovery_code");

                entity.HasOne(d => d.IdUserNavigation).WithMany(p => p.PasswordRecoveries)
                    .HasForeignKey(d => d.IdUser)
                    .HasConstraintName("FK_PasswordRecovery_User");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.IdUser).HasName("PK__User__D2D1463774A8AE84");

                entity.ToTable("User");

                entity.HasIndex(e => e.Email, "UQ__User__AB6E6164C70A471A").IsUnique();

                entity.HasIndex(e => e.Username, "UQ__User__F3DBC5728651C725").IsUnique();

                entity.Property(e => e.IdUser).HasColumnName("id_user");
                entity.Property(e => e.AvatarReference)
                    .HasMaxLength(255)
                    .IsUnicode(false)
                    .HasColumnName("avatar_reference");
                entity.Property(e => e.Email)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("email");
                entity.Property(e => e.Nickname)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("nickname");
                entity.Property(e => e.PasswordHash)
                    .HasMaxLength(255)
                    .IsUnicode(false)
                    .HasColumnName("password_hash");
                entity.Property(e => e.RegistrationDate)
                    .HasDefaultValueSql("(getdate())")
                    .HasColumnType("datetime")
                    .HasColumnName("registration_date");
                entity.Property(e => e.Username)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("username");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }

}
