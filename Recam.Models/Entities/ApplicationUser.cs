using Microsoft.AspNetCore.Identity;

namespace Recam.Models.Entities;

public class ApplicationUser : IdentityUser
{
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } =  DateTime.UtcNow;
}

