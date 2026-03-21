using Autofac;
using Autofac.Extensions.DependencyInjection;
using Extenso.Data.Entity;
using Hangfire;
using Kinnect.Data;
using Kinnect.Data.Entities;
using Kinnect.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sejil;
using Serilog;
using Serilog.Sinks.PostgreSQL.ColumnWriters;

var builder = WebApplication.CreateBuilder(args);

string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException(
        "This application requires a connection string. Configure ConnectionStrings:DefaultConnection via User Secrets (local), " +
        "appsettings, or environment variable ConnectionStrings__DefaultConnection (e.g. in Docker).");
}

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.PostgreSQL(
        connectionString: connectionString!,
        tableName: "Log",
        columnOptions: new Dictionary<string, ColumnWriterBase>
        {
            { "message", new RenderedMessageColumnWriter() },
            { "message_template", new MessageTemplateColumnWriter() },
            { "level", new LevelColumnWriter() },
            { "timestamp", new TimestampColumnWriter() },
            { "exception", new ExceptionColumnWriter() },
            { "properties", new LogEventSerializedColumnWriter() }
        },
        needAutoCreateTable: true)
    .CreateLogger();

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.UseSerilog();
builder.Host.UseSejil(writeToProviders: true);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

string authProviderName = builder.Configuration.GetValue<string>("Authentication:Provider") ?? "Identity";
bool useKeycloak = authProviderName.Equals("Keycloak", StringComparison.OrdinalIgnoreCase);

builder.Services.KinnectAddAuthentication(builder.Configuration, useKeycloak);

if (useKeycloak)
{
    builder.Services.ConfigureSejil(options =>
        options.AuthenticationScheme = CookieAuthenticationDefaults.AuthenticationScheme);
}
else
{
    builder.Services.ConfigureSejil(options =>
        options.AuthenticationScheme = IdentityConstants.ApplicationScheme);
}

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddEntityFrameworkRepository();

builder.Services.KinnectAddHangfire(connectionString);
builder.Services.KinnectAddServices();
builder.Services.KinnectAddImageProcessing(builder.Configuration);
builder.Services.KinnectAddUserInfoService(builder.Configuration);

builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
{
    containerBuilder.RegisterType<ApplicationDbContextFactory>().As<IDbContextFactory>().SingleInstance();

    containerBuilder.RegisterGeneric(typeof(EntityFrameworkRepository<>))
        .As(typeof(IRepository<>))
        .InstancePerLifetimeScope();
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
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!useKeycloak)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

// Serve uploaded files
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

app.UseSerilogRequestLogging();
app.UseSejil();

app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireAuthorizationFilter()]
});

if (app.Environment.IsDevelopment())
{
    app.MapStaticAssets();
}

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

var defaultRoute = app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
if (app.Environment.IsDevelopment())
{
    defaultRoute.WithStaticAssets();
}

var razorPages = app.MapRazorPages();
if (app.Environment.IsDevelopment())
{
    razorPages.WithStaticAssets();
}

using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;
try
{
    var context = services.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();

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