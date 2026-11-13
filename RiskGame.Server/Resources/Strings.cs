using System.Resources;

namespace RiskGame.Server.Resources
{
    public static class Strings
    {
        private const string ResourceBaseName = "RiskGame.Server.Resources.Strings";
        private static readonly ResourceManager ResourceManager =
            new ResourceManager(ResourceBaseName, typeof(Strings).Assembly);

        public static string VerificationSubject => ResourceManager.GetString(nameof(VerificationSubject))!;

        public static string VerificationBody => ResourceManager.GetString(nameof(VerificationBody))!;
    }
}
