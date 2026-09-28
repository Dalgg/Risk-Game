using RiskGame.Models;
using System.Linq;

namespace RiskGame.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        public bool AuthenticateUser(string username, string password)
        {
            using (var context = new RiskGameContext())
            {
                var user = context.Users.FirstOrDefault(u => 
                    u.Username == username && 
                    u.PasswordHash == password); 
                return user != null;
            }
        }
    }
}