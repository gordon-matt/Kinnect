using Extenso.Data.Entity;
using Hangfire;
using Hangfire.Storage.SQLite;
using LogVue.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kinnect.Data.Sqlite;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddKinnectSqlite(IConfiguration configuration)
        {
            string connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=kinnect.db";

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(connectionString, sqlite =>
                    sqlite.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name));
                options.ConfigureWarnings(w => w.Ignore(SqliteEventId.SchemaConfiguredWarning));
            });

            services.AddScoped<ApplicationDbContextBase>(sp => sp.GetRequiredService<ApplicationDbContext>());
            services.AddSingleton<IDbContextFactory, ApplicationDbContextFactory>();
            services.AddLogVueSqlite(connectionString);
            return services;
        }

        public IServiceCollection AddKinnectSqliteHangfire(string? hangfireDbPath = null)
        {
            string path = hangfireDbPath ?? "hangfire.db";
            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSQLiteStorage(path));
            return services;
        }
    }
}