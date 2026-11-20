using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using RiskGame.Models;
using RiskGame.Server.Services;

namespace RiskGame.Server
{
    public static class Program
    {
        private const int DefaultGrpcPort = 5050;

        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.ListenAnyIP(DefaultGrpcPort, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http2;
                });
            });

            builder.Services.AddGrpc();
            builder.Services.AddSingleton<EmailService>();
            builder.Services.AddSingleton<AuthSessionManager>();
            builder.Services.AddSingleton<InvitationRepositoryService>();
            builder.Services.AddSingleton<InvitationSessionManager>();
            builder.Services.AddSingleton<FriendRepositoryService>();
            builder.Services.AddSingleton<FriendSessionManager>();
            builder.Services.AddSingleton<ProfileSessionRegistry>();
            builder.Services.AddScoped<IProfileRepository, ProfileRepositoryService>();
            builder.Services.AddSingleton<AuthRepositoryService>();
            builder.Services.AddSingleton<ICompletedMatchRepository>(
                new MatchResultsRepositoryService(CreateDatabaseContext));
            builder.Services.AddSingleton<IScoreboardRepository>(
                new ScoreboardRepositoryService(CreateDatabaseContext));
            builder.Services.AddSingleton<GrpcSessionAuthenticator>();
            builder.Services.AddSingleton<LobbyRepositoryService>();
            builder.Services.AddSingleton<MatchSessionManager>();
            builder.Services.AddSingleton<LobbySessionManager>();

            WebApplication app = builder.Build();

            app.MapGrpcService<AuthGrpcService>();
            app.MapGrpcService<InvitationGrpcService>();
            app.MapGrpcService<FriendGrpcService>();
            app.MapGrpcService<ProfileGrpcService>();
            app.MapGrpcService<LobbyGrpcService>();
            app.MapGrpcService<ChatGrpcService>();
            app.MapGrpcService<MatchGrpcService>();
            app.MapGrpcService<ScoreboardGrpcService>();

            app.Run();
        }

        private static RiskGameContext CreateDatabaseContext()
        {
            return new RiskGameContext();
        }
    }
}
