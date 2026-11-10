using System;
using System.Data.Common;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Services;
using RiskGame.Contracts.Friend;

namespace RiskGame.Server.Services
{
    public sealed class FriendRepositoryService
    {
        private const string PendingStatus = "pending";
        private const string RejectedStatus = "rejected";
        private const string AcceptedStatus = "accepted";
        private const int NoUpdatedRequests = 0;

        public PendingFriendRequestList GetPendingRequests(int userId)
        {
            try
            {
                using RiskGameContext databaseContext = new RiskGameContext();
                PendingFriendRequestList pendingRequests = new PendingFriendRequestList();
                pendingRequests.Requests.AddRange(databaseContext.Friendships.AsNoTracking()
                    .Where(friendship => friendship.IdUser2 == userId && friendship.Status == PendingStatus)
                    .OrderBy(friendship => friendship.RequestDate)
                    .ThenBy(friendship => friendship.IdFriendship)
                    .Select(friendship => new PendingFriendRequest
                    {
                        RequestId = friendship.IdFriendship,
                        SenderUsername = friendship.IdUser1Navigation.Username
                    }).ToList());
                return pendingRequests;
            }
            catch (DbException databaseException)
            {
                JsonLogger.Error(databaseException, "Could not query pending friendships for user {0}.", userId);
                throw new DataOperationException("Could not query pending friendships.", databaseException);
            }
        }

        public void RespondToRequest(int userId, int requestId, bool accept)
        {
            string friendshipStatus = RejectedStatus;
            DateTime? acceptanceDate = null;
            if (accept)
            {
                friendshipStatus = AcceptedStatus;
                acceptanceDate = DateTime.Now;
            }
            try
            {
                using RiskGameContext databaseContext = new RiskGameContext();
                int updatedRequests = databaseContext.Friendships
                    .Where(friendship => friendship.IdFriendship == requestId
                        && friendship.IdUser2 == userId && friendship.Status == PendingStatus)
                    .ExecuteUpdate(properties => properties
                        .SetProperty(friendship => friendship.Status, friendshipStatus)
                        .SetProperty(friendship => friendship.AcceptanceDate, acceptanceDate));
                if (updatedRequests == NoUpdatedRequests)
                {
                    throw new FriendRequestRejectedException("The pending request is no longer available.");
                }
            }
            catch (DbException databaseException)
            {
                JsonLogger.Error(databaseException, "Could not respond to friendship request {0}.", requestId);
                throw new DataOperationException("Could not respond to the friendship request.", databaseException);
            }
        }

        public void SendFriendRequest(int userId, string targetUsername)
        {
            try
            {
                using RiskGameContext databaseContext = new RiskGameContext();
                User? targetUser = databaseContext.Users.AsNoTracking()
                    .FirstOrDefault(user => user.Username == targetUsername);
                if (targetUser == null || targetUser.IdUser == userId)
                {
                    throw new FriendRequestRejectedException("The requested friend is not a valid recipient.");
                }

                SaveFriendRequest(databaseContext, userId, targetUser.IdUser);
            }
            catch (DbUpdateException databaseUpdateException)
            {
                JsonLogger.Error(databaseUpdateException, "Could not persist a friend request for user {0}.", userId);
                throw new DataOperationException("Could not persist the friend request.", databaseUpdateException);
            }
            catch (DbException databaseException)
            {
                JsonLogger.Error(databaseException, "Could not access friendships for user {0}.", userId);
                throw new DataOperationException("Could not access friendships.", databaseException);
            }
        }

        private void SaveFriendRequest(RiskGameContext databaseContext, int userId, int targetUserId)
        {
            Friendship? friendship = databaseContext.Friendships.FirstOrDefault(friendshipRecord =>
                (friendshipRecord.IdUser1 == userId && friendshipRecord.IdUser2 == targetUserId)
                || (friendshipRecord.IdUser1 == targetUserId && friendshipRecord.IdUser2 == userId));
            if (friendship != null)
            {
                if (friendship.Status != RejectedStatus)
                {
                    throw new FriendRequestRejectedException("A friendship or pending request already exists.");
                }
            }
            else
            {
                friendship = new Friendship();
                databaseContext.Friendships.Add(friendship);
            }

            friendship.IdUser1 = userId;
            friendship.IdUser2 = targetUserId;
            friendship.Status = PendingStatus;
            friendship.RequestDate = DateTime.Now;
            friendship.AcceptanceDate = null;
            databaseContext.SaveChanges();
        }
    }
}
