using System.Linq;
using RiskGame.Models;

namespace RiskGame.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        public bool AuthenticateUser(string username, string password)
        {
            using (RiskGameContext context = new RiskGameContext())
            {
                User? user = context.Users.FirstOrDefault(u => 
                    u.Username == username && 
                    u.PasswordHash == password); 
                return user != null;
            }
        }
    }
}
