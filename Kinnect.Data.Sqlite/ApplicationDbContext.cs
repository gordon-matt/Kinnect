using Microsoft.EntityFrameworkCore;

namespace Kinnect.Data.Sqlite;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : ApplicationDbContextBase(options);