using System.Linq;
using RiskGame.Models;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        public User? AuthenticateUser(string username, string password)
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
    }
}