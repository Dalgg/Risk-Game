using System;
using System.Data.Common;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public sealed class FriendRepositoryService
    {
        private const string PendingStatus = "pending";
        private const string RejectedStatus = "rejected";

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