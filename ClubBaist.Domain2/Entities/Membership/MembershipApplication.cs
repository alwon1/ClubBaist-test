using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using ClubBaist.Domain2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ClubBaist.Domain2.Entities.Membership;

public class MembershipApplication
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Editable(false)]
    public int Id { get; set; }
    [DisplayName("First Name")]
    [Required(ErrorMessage = "First name is required.")]
    [MaxLength(50, ErrorMessage = "First name cannot exceed 50 characters.")]
    public required string FirstName { get; set; }
    [Required(ErrorMessage = "Last name is required.")]
    [MaxLength(50, ErrorMessage = "Last name cannot exceed 50 characters.")]
    [DisplayName("Last Name")]
    public required string LastName { get; set; }
    [Required(ErrorMessage = "Occupation is required.")]
    [MaxLength(100, ErrorMessage = "Occupation cannot exceed 100 characters.")]
    [DisplayName("Occupation")]
    public required string Occupation { get; set; }
    [Required(ErrorMessage = "Company name is required.")]
    [MaxLength(100, ErrorMessage = "Company name cannot exceed 100 characters.")]
    [DisplayName("Company Name")]
    public required string CompanyName { get; set; }
    [Required(ErrorMessage = "Address is required.")]
    [MaxLength(200, ErrorMessage = "Address cannot exceed 200 characters.")]
    [DisplayName("Address")]
    public required string Address { get; set; }
    [DisplayName("Postal Code")]
    [Required(ErrorMessage = "Postal code is required.")]
    [StringLength(7, MinimumLength = 6, ErrorMessage = "Postal code must be 6 or 7 characters.")]
    [RegularExpression(@"^[A-Za-z]\d[A-Za-z][ -]?\d[A-Za-z]\d$", ErrorMessage = "Invalid postal code format.")]
    public required string PostalCode { get; set; }
    [Required(ErrorMessage = "Phone number is required.")]
    [Phone(ErrorMessage = "Invalid phone number format.")]
    [DisplayName("Phone Number")]
    public required string Phone { get; set; }
    [Phone(ErrorMessage = "Invalid alternate phone number format.")]
    [DisplayName("Alternate Phone")]
    public string? AlternatePhone { get; set; }
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    [Required(ErrorMessage = "Email is required.")]
    [DisplayName("Email Address")]
    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters.")]
    public required string Email { get; set; }
    [DisplayName("Date of Birth")]
    [Required(ErrorMessage = "Date of birth is required.")]
    [DataType(DataType.Date)]

    public DateTime DateOfBirth { get; set; }
    public int Sponsor1MemberId { get; set; }
    public int Sponsor2MemberId { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;
    [Required]
    [ForeignKey(nameof(RequestedMembershipLevel))]
    public int RequestedMembershipLevelId { get; set; }
    public MembershipLevel RequestedMembershipLevel { get; set; } = default!;
}
