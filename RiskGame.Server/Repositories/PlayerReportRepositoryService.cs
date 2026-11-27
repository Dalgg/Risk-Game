using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RiskGame.Exceptions;
using RiskGame.Models;

namespace RiskGame.Server.Services
{
    public class PlayerReportRepositoryService
    {
        private const int MinimumRegisteredIdentifier = 1;
        private const int MinimumReportThreshold = 1;
        private const string LockReportedUserSql =
            "SELECT * FROM dbo.[User] WITH (UPDLOCK, HOLDLOCK) WHERE id_user = {0}";
        private readonly Func<RiskGameContext> createContext;

        public PlayerReportRepositoryService(Func<RiskGameContext> createContext)
        {
            this.createContext = createContext;
        }

        public virtual PlayerReportResult RecordReport(PlayerReportSubmission submission)
        {
            ValidateSubmission(submission);
            try
            {
                using RiskGameContext databaseContext = createContext();
                using IDbContextTransaction transaction = databaseContext.Database
                    .BeginTransaction(IsolationLevel.Serializable);
                User reportedUser = LoadReportedUser(databaseContext, submission.ReportedId);
                ValidateReferences(databaseContext, submission);
                PlayerReportResult result = SaveReport(databaseContext, reportedUser, submission);
                transaction.Commit();
                return result;
            }
            catch (DbUpdateException databaseUpdateException)
            {
                throw new DataOperationException("Could not save the player report.", databaseUpdateException);
            }
            catch (DbException databaseException)
            {
                throw new DataOperationException("Could not save the player report.", databaseException);
            }
        }

        private static void ValidateSubmission(PlayerReportSubmission submission)
        {
            bool hasInvalidUsers = submission.ReporterId < MinimumRegisteredIdentifier
                || submission.ReportedId < MinimumRegisteredIdentifier;
            bool hasInvalidScope = submission.LobbyId < MinimumRegisteredIdentifier
                || submission.ReporterId == submission.ReportedId;
            if (hasInvalidUsers || hasInvalidScope)
            {
                throw new ReportNotAllowedException("Only distinct registered users can submit a lobby report.");
            }
            bool hasInvalidUtcDates = submission.ReportDateUtc.Kind != DateTimeKind.Utc
                || submission.BannedUntilUtc.Kind != DateTimeKind.Utc;
            bool hasInvalidSuspension = submission.BannedUntilUtc <= submission.ReportDateUtc
                || submission.RequiredDistinctReporters < MinimumReportThreshold;
            if (hasInvalidUtcDates || hasInvalidSuspension)
            {
                throw new ReportNotAllowedException("The report requires valid UTC dates and a positive threshold.");
            }
        }

        private static User LoadReportedUser(RiskGameContext databaseContext, int reportedId)
        {
            IQueryable<User> users = databaseContext.Users;
            if (databaseContext.Database.IsSqlServer())
            {
                users = databaseContext.Users.FromSqlRaw(LockReportedUserSql, reportedId);
            }
            User? reportedUser = users.SingleOrDefault(user => user.IdUser == reportedId);
            if (reportedUser == null)
            {
                throw new ProfileNotFoundException("The reported account does not exist.");
            }
            return reportedUser;
        }

        private static void ValidateReferences(RiskGameContext databaseContext, PlayerReportSubmission submission)
        {
            bool reporterExists = databaseContext.Users.Any(user => user.IdUser == submission.ReporterId);
            bool lobbyExists = databaseContext.Lobbies.Any(lobby => lobby.IdLobby == submission.LobbyId);
            bool hasRequiredReferences = reporterExists && lobbyExists;
            if (!hasRequiredReferences)
            {
                throw new ReportNotAllowedException("The reporter account or lobby does not exist.");
            }
        }

        private static PlayerReportResult SaveReport(RiskGameContext databaseContext, User reportedUser,
            PlayerReportSubmission submission)
        {
            bool duplicate = databaseContext.PlayerReports.Any(report => report.IdReporter == submission.ReporterId
                && report.IdReported == submission.ReportedId && report.IdLobby == submission.LobbyId);
            bool alreadySuspended = reportedUser.BannedUntil.HasValue
                && reportedUser.BannedUntil.Value > submission.ReportDateUtc;
            bool reportBlocked = duplicate || alreadySuspended;
            if (!reportBlocked)
            {
                databaseContext.PlayerReports.Add(CreateReport(submission));
                databaseContext.SaveChanges();
                ApplySuspension(databaseContext, reportedUser, submission);
            }
            return new PlayerReportResult
            {
                IsDuplicate = reportBlocked,
                BannedUntil = AsUtc(reportedUser.BannedUntil)
            };
        }

        private static PlayerReport CreateReport(PlayerReportSubmission submission)
        {
            return new PlayerReport
            {
                IdReporter = submission.ReporterId,
                IdReported = submission.ReportedId,
                IdLobby = submission.LobbyId,
                ReportDate = submission.ReportDateUtc
            };
        }

        private static void ApplySuspension(RiskGameContext databaseContext, User reportedUser,
            PlayerReportSubmission submission)
        {
            IQueryable<PlayerReport> pendingReports = databaseContext.PlayerReports
                .Where(report => report.IdReported == submission.ReportedId && !report.IsProcessed);
            int distinctReporters = pendingReports.Select(report => report.IdReporter).Distinct().Count();
            if (distinctReporters >= submission.RequiredDistinctReporters)
            {
                reportedUser.BannedUntil = submission.BannedUntilUtc;
                foreach (PlayerReport report in pendingReports.ToList())
                {
                    report.IsProcessed = true;
                }
                databaseContext.SaveChanges();
            }
        }

        private static DateTime? AsUtc(DateTime? dateTime)
        {
            DateTime? utcDateTime = dateTime;
            if (dateTime.HasValue)
            {
                utcDateTime = DateTime.SpecifyKind(dateTime.Value, DateTimeKind.Utc);
            }
            return utcDateTime;
        }
    }
}
