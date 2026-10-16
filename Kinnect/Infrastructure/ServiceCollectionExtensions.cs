using System.Net;
using Hangfire;
using Hangfire.PostgreSql;
using Heroicsoft.NetForge.Authentication;
using Kinnect.Services;
using Kinnect.Services.Jobs;
using Microsoft.AspNetCore.HttpOverrides;

namespace Kinnect.Infrastructure;

internal static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public void KinnectAddAuthentication(IConfiguration configuration, bool useKeycloak)
        {
            services.AddNetForgeAuthentication<ApplicationUser, ApplicationRole, Data.ApplicationDbContext>(
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
                    options.AccessDeniedPath = "/Home/AccessDenied";
                    options.RemoteFailurePath = "/Home/Error";
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

        public void KinnectAddHangfire(string connectionString)
        {
            services.AddHangfire(configuration => configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString),
                new PostgreSqlStorageOptions
                {
                    QueuePollInterval = TimeSpan.FromSeconds(15)
                }));

            services.AddHangfireServer(options =>
            {
                options.ServerName = "main";
                options.Queues = ["default"];
            });
        }

        public void KinnectAddServices()
        {
            services.AddHttpContextAccessor();
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
            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddSingleton<IFileStorageService, FileStorageService>();
            services.AddScoped<IVideoProcessingService, VideoProcessingService>();
            services.AddScoped<IPersonBackupService, PersonBackupService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<PersonBackupJob>();
            services.AddScoped<VideoTranscodeJob>();
            services.AddScoped<UnreadMessageEmailJob>();
        }

        public void KinnectAddImageProcessing(IConfiguration configuration)
        {
            services.Configure<ImageProcessingOptions>(configuration.GetSection("ImageProcessing"));
            services.Configure<DocumentProcessingOptions>(configuration.GetSection("DocumentProcessing"));
            services.Configure<VideoProcessingOptions>(configuration.GetSection("VideoProcessing"));
            services.Configure<SmtpOptions>(configuration.GetSection("Smtp"));
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
}