using Autofac;
using Autofac.Extensions.DependencyInjection;
using EasyChatz;
using Hangfire;
using Heroicsoft.NetForge.Abstractions.Navigation;
using Heroicsoft.NetForge.Components;
using Heroicsoft.NetForge.Hangfire;
using Kinnect.Components;
using Kinnect.Data;
using Kinnect.Infrastructure;
using Kinnect.Services.Jobs;
using Kinnect.Services.Mapping;
using LogVue;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Serilog;
using ChatMessage = EasyChatz.Data.Entities.ChatMessage;
using ChatRoom = EasyChatz.Data.Entities.ChatRoom;
using Constants = Kinnect.Constants;
using MessageNotification = EasyChatz.Data.Entities.MessageNotification;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = Constants.MaxUploadBytes);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = Constants.MaxUploadBytes);

builder.Services.AddLogVue(options =>
    builder.Configuration.GetSection(LogVueOptions.SectionName).Bind(options));

builder.Services.AddSerilog((services, configuration) => configuration
    .MinimumLevel.Information()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.LogVue(services));

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());

builder.Services.AddKinnectDatabase(builder.Configuration);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

string authProviderName = builder.Configuration.GetValue<string>("Authentication:Provider") ?? "Identity";
bool useKeycloak = authProviderName.Equals("Keycloak", StringComparison.OrdinalIgnoreCase);
Constants.SetUseKeyCloak(useKeycloak);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();
builder.Services.AddNetForgeComponents(ui =>
{
    ui.ThemeStorageKey = "kinnect-theme";
    ui.DefaultColorScheme = "system";
    ui.AccentColor = "#0f766e";
    ui.AccentColorDark = "#2dd4bf";
});

builder.Services.ConfigureNetForgeToastSounds(sounds => sounds.EnableSounds = true);

builder.Services.Configure<ForgeShellOptions>(shell =>
{
    shell.Brand = "Kinnect";
    shell.LogoUrl = "img/Logo.png";
    shell.LogoDarkUrl = "img/Logo.png";
    shell.SearchPlaceholder = "Search people";
    shell.NavigationPlacement = ForgeNavigationPlacement.Topbar;
    shell.UserMenuItems.Add(new ForgeNavItem { Title = "My profile", Href = "/me", Icon = "bi-person" });
});

builder.Services.AddScoped<IShellNavigation, Kinnect.Components.Layout.KinnectNavigation>();

builder.Services.KinnectAddAuthentication(builder.Configuration, useKeycloak);
builder.Services.AddKinnectChat(builder.Configuration);
builder.Services.AddEntityFrameworkRepository();
KinnectMappingRegistration.Register();
builder.Services.AddExtensoMapperEntityFrameworkRepository();
builder.Services.AddKinnectHangfire(builder.Configuration);
builder.Services.KinnectAddServices();
builder.Services.KinnectAddUserInfoService(builder.Configuration);

builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
{
    containerBuilder.RegisterGeneric(typeof(EntityFrameworkRepository<>))
        .As(typeof(IRepository<>))
        .InstancePerLifetimeScope();

    containerBuilder.Register(c => new EntityFrameworkRepository<ChatRoom>(c.Resolve<EasyChatz.Data.IEasyChatzDbContextFactory>()))
        .As<IRepository<ChatRoom>>()
        .SingleInstance();
    containerBuilder.Register(c => new EntityFrameworkRepository<ChatMessage>(c.Resolve<EasyChatz.Data.IEasyChatzDbContextFactory>()))
        .As<IRepository<ChatMessage>>()
        .SingleInstance();
    containerBuilder.Register(c => new EntityFrameworkRepository<MessageNotification>(c.Resolve<EasyChatz.Data.IEasyChatzDbContextFactory>()))
        .As<IRepository<MessageNotification>>()
        .SingleInstance();
});

var app = builder.Build();

if (useKeycloak)
{
    app.UseForwardedHeaders();
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

if (!useKeycloak)
{
    app.UseHttpsRedirection();
}

// The original GEDCOM uploads live under the uploads folder but are private family data: never serve them.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments($"/uploads/{Constants.FileStorage.GedcomOriginals}", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});

app.UseStaticFiles();

{
    string uploadPath = app.Configuration.GetValue<string>("FileStorage:BasePath")
        ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
    Directory.CreateDirectory(uploadPath);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadPath),
        RequestPath = "/uploads"
    });
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseSerilogRequestLogging();

app.MapLogVue().RequireAuthorization(new AuthorizeAttribute
{
    Roles = Constants.Roles.Administrator
});

app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new AdministratorDashboardAuthorizationFilter()]
});

RecurringJob.AddOrUpdate<PersonBackupJob>(
    "weekly-person-tree-backup",
    job => job.ExecuteAsync(CancellationToken.None),
    Cron.Weekly(DayOfWeek.Sunday, 3));

RecurringJob.RemoveIfExists("daily-person-backup-retention");

RecurringJob.AddOrUpdate<EventGeocodeJob>(
    "daily-event-geocoding",
    job => job.ExecuteAsync(CancellationToken.None),
    Cron.Daily(4));

RecurringJob.AddOrUpdate<UnreadMessageEmailJob>(
    "hourly-unread-message-emails",
    job => job.ExecuteAsync(CancellationToken.None),
    Cron.Hourly());

app.MapStaticAssets();
app.MapEasyChatzApi().RequireAuthorization();
app.MapEasyChatzHub().RequireAuthorization();
app.MapControllers();
app.MapRazorPages();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .WithStaticAssets();

using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;
try
{
    var context = services.GetRequiredService<ApplicationDbContextBase>();
    await context.Database.MigrateAsync();

    // After the migration, because the video settings (tools path) now live in the database.
    var videoSettings = await services.GetRequiredService<ISettingsService>().GetAsync<VideoSettings>();
    await FfmpegBootstrapper.EnsureConfiguredAsync(
        videoSettings.ToolsPath,
        services.GetRequiredService<IHostEnvironment>(),
        services.GetRequiredService<ILoggerFactory>().CreateLogger("Ffmpeg"));

    if (!useKeycloak)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        await DbInitializer.InitializeAsync(context, userManager, roleManager, configuration);
    }
}
catch (Exception ex)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    if (logger.IsEnabled(LogLevel.Error))
    {
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

app.Run();