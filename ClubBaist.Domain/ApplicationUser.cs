using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ClubBaist.Domain;

public class ApplicationUser : IdentityUser<Guid>
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    public string Phone { get; set; } = string.Empty;
}
