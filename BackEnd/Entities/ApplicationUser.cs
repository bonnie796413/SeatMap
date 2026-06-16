using Microsoft.AspNetCore.Identity;

namespace BackEnd.Entities;

public class ApplicationUser : IdentityUser
{
    public Guid? EmployeeId { get; set; }
    public DateTime CreatedAt { get; set; }
}
