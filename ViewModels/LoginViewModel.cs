using System.Data.Common;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Resources;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        public User? AuthenticateUser(string username, string password)
        {
            try
            {
                using (RiskGameContext context = new RiskGameContext())
                {
                    User? user = context.Users.FirstOrDefault(u => u.Username == username);
                    if (user == null)
                    {
                        return null;
                    }
    
                    (bool isValid, bool needsRehash) = PasswordHasher.Verify(password, user.PasswordHash);
                    if (!isValid)
                    {
                        return null;
                    }
    
                    if (needsRehash)
                    {
                        user.PasswordHash = PasswordHasher.Hash(password);
                        context.SaveChanges();
                    }
    
                    return user;
                }
            }
            catch (DbUpdateException ex)
            {
                JsonLogger.Error(ex, "Persistence error (DbUpdateException) during login for user {0}.", username);
                throw new DataOperationException(Strings.LoginErrorDatabase, ex);
            }
            catch (DbException ex)
            {
                JsonLogger.Error(ex, "Connection error (DbException) during login for user {0}.", username);
                throw new DataOperationException(Strings.LoginErrorDatabase, ex);
            }
        }
    }
}
