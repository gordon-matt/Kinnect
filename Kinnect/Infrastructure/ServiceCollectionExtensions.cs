using System.Net;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Kinnect.Infrastructure;

internal static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public void KinnectAddAuthentication(IConfiguration configuration, bool useKeycloak)
        {
            if (useKeycloak)
            {
                string? keycloakAuthority = configuration["Authentication:Keycloak:Authority"];
                string? keycloakBackchannelBaseUrl = configuration["Authentication:Keycloak:BackchannelBaseUrl"];

                services.AddSingleton<IAuthProviderService>(new AuthProviderService(AuthProvider.Keycloak, keycloakAuthority));

                services.AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.LoginPath = "/auth/login";
                    options.AccessDeniedPath = "/Home/AccessDenied";
                })
                .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
                {
                    options.Authority = keycloakAuthority;

                    if (!string.IsNullOrWhiteSpace(keycloakBackchannelBaseUrl) && !string.IsNullOrWhiteSpace(keycloakAuthority))
                    {
                        options.BackchannelHttpHandler = new KeycloakBackchannelHandler(keycloakAuthority, keycloakBackchannelBaseUrl);
                    }

                    options.ClientId = configuration["Authentication:Keycloak:ClientId"];
                    options.ClientSecret = configuration["Authentication:Keycloak:ClientSecret"];
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.SaveTokens = true;
                    options.GetClaimsFromUserInfoEndpoint = true;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        NameClaimType = "preferred_username"
                    };

                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");

                    options.RequireHttpsMetadata = false;
                    options.CorrelationCookie.SameSite = SameSiteMode.None;
                    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.NonceCookie.SameSite = SameSiteMode.None;
                    options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;

                    string? publicOrigin = configuration["Authentication:Keycloak:PublicOrigin"];
                    bool useBackchannel = !string.IsNullOrWhiteSpace(keycloakBackchannelBaseUrl);
                    if (!string.IsNullOrWhiteSpace(publicOrigin) || useBackchannel)
                    {
                        publicOrigin = publicOrigin?.TrimEnd('/');
                        options.Events.OnRedirectToIdentityProvider = context =>
                        {
                            if (!string.IsNullOrWhiteSpace(publicOrigin))
                            {
                                context.ProtocolMessage.RedirectUri = publicOrigin + "/signin-oidc";
                            }

                            if (useBackchannel && !string.IsNullOrEmpty(keycloakAuthority))
                            {
                                string fullUrl = context.ProtocolMessage.CreateAuthenticationRequestUrl();
                                int queryIndex = fullUrl.IndexOf('?');
                                string query = queryIndex >= 0 ? fullUrl[queryIndex..] : string.Empty;
                                string authUrl = keycloakAuthority.TrimEnd('/') + "/protocol/openid-connect/auth" + query;
                                context.Response.Redirect(authUrl);
                                context.HandleResponse();
                            }
                            return Task.CompletedTask;
                        };

                        if (!string.IsNullOrWhiteSpace(publicOrigin))
                        {
                            options.Events.OnRedirectToIdentityProviderForSignOut = context =>
                            {
                                context.ProtocolMessage.PostLogoutRedirectUri = publicOrigin + "/signout-callback-oidc";
                                return Task.CompletedTask;
                            };
                        }
                    }

                    options.Events.OnRemoteFailure = context =>
                    {
                        context.Response.Redirect("/Home/Error");
                        context.HandleResponse();
                        return Task.CompletedTask;
                    };
                });

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
            else
            {
                services.AddSingleton<IAuthProviderService>(new AuthProviderService(AuthProvider.Identity, null));

                services.AddIdentity<ApplicationUser, ApplicationRole>()
                    .AddEntityFrameworkStores<Data.ApplicationDbContext>()
                    .AddDefaultTokenProviders()
                    .AddDefaultUI();
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
            services.AddSingleton<IFileStorageService, FileStorageService>();
            services.AddScoped<IVideoProcessingService, VideoProcessingService>();
        }

        public void KinnectAddImageProcessing(IConfiguration configuration)
        {
            services.Configure<ImageProcessingOptions>(configuration.GetSection("ImageProcessing"));
            services.Configure<DocumentProcessingOptions>(configuration.GetSection("DocumentProcessing"));
            services.Configure<VideoProcessingOptions>(configuration.GetSection("VideoProcessing"));
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