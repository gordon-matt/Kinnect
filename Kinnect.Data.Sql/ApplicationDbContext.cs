using Kinnect.Data;
using Microsoft.EntityFrameworkCore;

namespace Kinnect.Data.Sql;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : ApplicationDbContextBase(options);
