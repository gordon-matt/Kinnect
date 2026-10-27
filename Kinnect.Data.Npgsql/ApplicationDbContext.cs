using Microsoft.EntityFrameworkCore;

namespace Kinnect.Data.Npgsql;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : ApplicationDbContextBase(options);