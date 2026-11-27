using System;
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
        private const int KeepAlivePingDelaySeconds = 10;
        private const int KeepAlivePingTimeoutSeconds = 5;

        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.Limits.Http2.KeepAlivePingDelay = TimeSpan.FromSeconds(KeepAlivePingDelaySeconds);
                serverOptions.Limits.Http2.KeepAlivePingTimeout = TimeSpan.FromSeconds(KeepAlivePingTimeoutSeconds);
                serverOptions.ListenAnyIP(DefaultGrpcPort, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http2;
                });
            });

            builder.Services.AddGrpc();
            builder.Services.AddSingleton<EmailService>();
            builder.Services.AddSingleton<IEmailDeliveryService>(
                serviceProvider => serviceProvider.GetRequiredService<EmailService>());
            builder.Services.AddSingleton<AuthSessionManager>();
            builder.Services.AddSingleton<PasswordResetManager>();
            builder.Services.AddSingleton<InvitationRepositoryService>();
            builder.Services.AddSingleton<InvitationSessionManager>();
            builder.Services.AddSingleton<FriendRepositoryService>();
            builder.Services.AddSingleton<FriendSessionManager>();
            builder.Services.AddSingleton<ProfileSessionRegistry>();
            builder.Services.AddSingleton<SessionConnectionManager>();
            builder.Services.AddScoped<IProfileRepository, ProfileRepositoryService>();
            builder.Services.AddSingleton<AuthRepositoryService>();
            builder.Services.AddSingleton<ICompletedMatchRepository>(
                new MatchResultsRepositoryService(CreateDatabaseContext));
            builder.Services.AddSingleton<IScoreboardRepository>(
                new ScoreboardRepositoryService(CreateDatabaseContext));
            builder.Services.AddSingleton<GrpcSessionAuthenticator>();
            builder.Services.AddSingleton<LobbyRepositoryService>();
            builder.Services.AddSingleton<MatchSessionManager>();
            builder.Services.AddSingleton<System.TimeProvider>(System.TimeProvider.System);
            builder.Services.AddHostedService<MatchTurnTimerService>();
            builder.Services.AddHostedService<SessionExpirationService>();
            builder.Services.AddSingleton<LobbySessionManager>();
            builder.Services.AddSingleton<PlayerReportRepositoryService>(
                new PlayerReportRepositoryService(CreateDatabaseContext));
            builder.Services.AddSingleton<ReportSessionManager>();

            WebApplication app = builder.Build();

            app.MapGrpcService<AuthGrpcService>();
            app.MapGrpcService<InvitationGrpcService>();
            app.MapGrpcService<FriendGrpcService>();
            app.MapGrpcService<ProfileGrpcService>();
            app.MapGrpcService<LobbyGrpcService>();
            app.MapGrpcService<LobbySettingsGrpcService>();
            app.MapGrpcService<ChatGrpcService>();
            app.MapGrpcService<MatchGrpcService>();
            app.MapGrpcService<ScoreboardGrpcService>();
            app.MapGrpcService<PlayerReportGrpcService>();

            app.Run();
        }

        private static RiskGameContext CreateDatabaseContext()
        {
            return new RiskGameContext();
        }
    }
}
