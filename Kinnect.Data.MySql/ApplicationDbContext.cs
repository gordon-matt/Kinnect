using Kinnect.Data;
using Microsoft.EntityFrameworkCore;

namespace Kinnect.Data.MySql;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : ApplicationDbContextBase(options);
