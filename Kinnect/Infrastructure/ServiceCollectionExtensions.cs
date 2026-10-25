using System.Net;
using EasyChatz;
using EasyChatz.Data.MySql;
using EasyChatz.Data.Npgsql;
using EasyChatz.Data.Sql;
using EasyChatz.Data.Sqlite;
using EasyChatz.Services;
using Hangfire;
using Heroicsoft.NetForge.Abstractions.Data;
using Heroicsoft.NetForge.Authentication;
using Kinnect.Data.MySql;
using Kinnect.Data.Npgsql;
using Kinnect.Data.Sql;
using Kinnect.Data.Sqlite;
using Kinnect.Services.Jobs;
using Microsoft.AspNetCore.HttpOverrides;

namespace Kinnect.Infrastructure;

internal static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddKinnectDatabase(IConfiguration configuration)
        {
            string provider = DatabaseProviders.Resolve(
                configuration[DatabaseProviders.ConfigurationKey],
                DatabaseProviders.Npgsql,
                [.. DatabaseProviders.All]);

            return provider switch
            {
                DatabaseProviders.Sqlite => services.AddKinnectSqlite(configuration),
                DatabaseProviders.SqlServer => services.AddKinnectSqlServer(configuration),
                DatabaseProviders.Npgsql => services.AddKinnectNpgsql(configuration),
                DatabaseProviders.MySql => services.AddKinnectMySql(configuration),
                _ => throw new InvalidOperationException($"Unknown {DatabaseProviders.ConfigurationKey} '{provider}'."),
            };
        }

        /// <summary>
        /// EasyChatz shares the application database on servers that honour schemas.
        /// SQLite ignores schemas, so chat gets its own file and the table names do not collide.
        /// </summary>
        public IServiceCollection AddKinnectChat(IConfiguration configuration)
        {
            services.AddScoped<IChatUserDirectory, KinnectChatUserDirectory>();

            string provider = DatabaseProviders.Resolve(
                configuration[DatabaseProviders.ConfigurationKey],
                DatabaseProviders.Npgsql,
                [.. DatabaseProviders.All]);
            string? connectionString = configuration.GetConnectionString("DefaultConnection");

            switch (provider)
            {
                case DatabaseProviders.Sqlite:
                    services.AddEasyChatzSqlite(configuration["EasyChatz:Sqlite"] ?? "Data Source=kinnect-chat.db");
                    break;
                case DatabaseProviders.SqlServer:
                    services.AddEasyChatzSqlServer(Required(connectionString, provider));
                    break;
                case DatabaseProviders.Npgsql:
                    services.AddEasyChatzNpgsql(Required(connectionString, provider));
                    break;
                case DatabaseProviders.MySql:
                    services.AddEasyChatzMySql(Required(connectionString, provider));
                    break;
            }

            services.AddEasyChatz();
            services.AddHostedService<ChatHistoryImporter>();
            return services;
        }

        public IServiceCollection AddKinnectHangfire(IConfiguration configuration)
        {
            string provider = DatabaseProviders.Resolve(
                configuration[DatabaseProviders.ConfigurationKey],
                DatabaseProviders.Npgsql,
                [.. DatabaseProviders.All]);
            string? hangfireExplicit = configuration.GetConnectionString("Hangfire");
            string? connectionString = string.IsNullOrWhiteSpace(hangfireExplicit)
                ? configuration.GetConnectionString("DefaultConnection")
                : hangfireExplicit;

            switch (provider)
            {
                case DatabaseProviders.Sqlite:
                    services.AddKinnectSqliteHangfire(configuration["Hangfire:SqlitePath"]);
                    break;
                case DatabaseProviders.SqlServer:
                    services.AddKinnectSqlServerHangfire(Required(connectionString, provider));
                    break;
                case DatabaseProviders.Npgsql:
                    services.AddKinnectNpgsqlHangfire(Required(connectionString, provider));
                    break;
                case DatabaseProviders.MySql:
                    services.AddKinnectMySqlHangfire(Required(connectionString, provider));
                    break;
            }

            services.AddHangfireServer(options =>
            {
                options.ServerName = "main";
                options.Queues = ["default"];
            });

            return services;
        }

        public void KinnectAddAuthentication(IConfiguration configuration, bool useKeycloak)
        {
            services.AddNetForgeAuthentication<ApplicationUser, ApplicationRole, Data.ApplicationDbContextBase>(
                configuration,
                options =>
                {
                    if (useKeycloak)
                    {
                        options.Provider = AuthProviderNames.Keycloak;
                    }

                    options.IncludeAuthController = false;
                    options.RegisterUserContext = false;
                    options.Identity.UseRelaxedPasswordPolicy = false;
                    options.AccessDeniedPath = "/access-denied";
                    options.RemoteFailurePath = "/error";
                    options.Keycloak.RequireHttpsMetadata = false;
                    options.Keycloak.CorrelationCookieSameSite = SameSiteMode.None;
                    options.Keycloak.CorrelationCookieSecurePolicy = CookieSecurePolicy.Always;
                    options.Keycloak.RedirectBrowserToPublicAuthority = true;
                });

            if (useKeycloak)
            {
                services.Configure<ForwardedHeadersOptions>(options =>
                {
                    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                        | ForwardedHeaders.XForwardedProto
                        | ForwardedHeaders.XForwardedHost;
                    options.KnownProxies.Clear();
                    options.KnownProxies.Add(IPAddress.Parse("127.0.0.1"));
                    options.KnownProxies.Add(IPAddress.Parse("::1"));
                    options.KnownProxies.Add(IPAddress.Parse("172.17.0.1"));
                });
            }
        }

        public void KinnectAddServices()
        {
            services.AddHttpContextAccessor();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddScoped<IUserContextService, UserContextService>();
            services.AddScoped<IPersonService, PersonService>();
            services.AddScoped<IPersonEventService, PersonEventService>();
            services.AddScoped<IPostService, PostService>();
            services.AddScoped<IPhotoService, PhotoService>();
            services.AddScoped<IVideoService, VideoService>();
            services.AddScoped<IDocumentService, DocumentService>();
            services.AddScoped<ITagService, TagService>();
            services.AddScoped<IFeedService, FeedService>();
            services.AddScoped<IGedcomService, GedcomService>();
            services.AddSingleton<IFileStorageService, FileStorageService>();
            services.AddScoped<IVideoProcessingService, VideoProcessingService>();
            services.AddScoped<IPersonBackupService, PersonBackupService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<KinnectFamilyGraph>();
            services.AddScoped<PersonBackupJob>();
            services.AddScoped<VideoTranscodeJob>();
            services.AddScoped<UnreadMessageEmailJob>();
            services.AddHttpClient("nominatim", client =>
            {
                client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Kinnect/1.0 (family archive)");
                client.Timeout = TimeSpan.FromSeconds(8);
            });
        }


        public void KinnectAddUserInfoService(IConfiguration configuration)
        {
            string authProvider = configuration.GetValue<string>("Authentication:Provider") ?? "Identity";
            bool useKeycloak = authProvider.Equals("Keycloak", StringComparison.OrdinalIgnoreCase);

            if (useKeycloak)
            {
                services.AddScoped<IUserInfoService, KeycloakUserInfoService>();
            }
            else
            {
                services.AddScoped<IUserInfoService, AspNetIdentityUserInfoService>();
            }
        }
    }

    private static string Required(string? connectionString, string provider) =>
        string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException($"Hangfire and chat require a connection string when using {provider}.")
            : connectionString;
}
