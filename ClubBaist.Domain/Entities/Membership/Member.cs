using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClubBaist.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Domain;

[Index(nameof(UserId), IsUnique = true)]
public class MemberShipInfo
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Required]
    [Range(1000, int.MaxValue)]
    public int Id { get; init; }

    [NotMapped]
    [PersonalData]
    public string MembershipNumber => $"{MembershipLevel.ShortCode}-{Id:D4}";

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public required ClubBaistUser User { get; init; }

    [Required]
    public int MembershipLevelId { get; set; }

    [ForeignKey(nameof(MembershipLevelId))]
    public required MembershipLevel MembershipLevel { get; set; }
}