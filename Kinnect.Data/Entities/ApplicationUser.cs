using Microsoft.AspNetCore.Identity;

namespace Kinnect.Data.Entities;

public class ApplicationUser : IdentityUser
{
    public virtual ICollection<ApplicationRole> Roles { get; set; } = [];
}