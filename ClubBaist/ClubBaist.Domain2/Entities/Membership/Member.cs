using System.ComponentModel.DataAnnotations;
using ClubBaist.Domain2.Entities;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Identity;

namespace ClubBaist.Domain2;

[Index(nameof(MembershipNumber), IsUnique = true)]
[Index(nameof(User), IsUnique = true)]
public class MemberShipInfo
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Required]
    [Range(1000, int.MaxValue)]
    public int Id { get; init; }
    [PersonalData]
    public string MembershipNumber => $"{MembershipLevel.ShortCode}-{Id:D4}";
    public required ClubBaistUser User { get; init; }
    public required MembershipLevel MembershipLevel { get; set; }
}