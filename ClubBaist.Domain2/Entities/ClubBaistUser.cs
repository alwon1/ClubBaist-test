using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Identity;

namespace ClubBaist.Domain2.Entities;

public class ClubBaistUser : IdentityUser<Guid>
{
    [ProtectedPersonalData]
    [DisplayName("Date of Birth")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [DataType(DataType.Date)]
    [Range(typeof(DateTime), "1900-01-01", "2100-12-31", ErrorMessage = "Date of Birth must be between 1900-01-01 and 2100-12-31.")]
    public DateTime DateOfBirth { get; set; }
    [ProtectedPersonalData]
    [DisplayName("Alternate Phone Number")]
    [Phone]
    [MaxLength(20)]
    [DataType(DataType.PhoneNumber)]
    [DisplayFormat(NullDisplayText = "N/A", ApplyFormatInEditMode = true, DataFormatString = "{0:(###) ###-####}")]
    public string? AlternatePhoneNumber { get; set; }
    [ProtectedPersonalData]
    [DisplayName("Address Line 1")]
    [MaxLength(200)]
    public string? AddressLine1 { get; set; }
    [ProtectedPersonalData]
    [DisplayName("Address Line 2")]
    [MaxLength(200)]
    public string? AddressLine2 { get; set; }
    [ProtectedPersonalData]
    [DisplayName("City")]
    [MaxLength(100)]
    public string? City { get; set; }
    [ProtectedPersonalData]
    [DisplayName("Province")]
    [MaxLength(100)]
    public string? Province { get; set; }
    [ProtectedPersonalData]
    [DisplayName("Postal Code")]
    [MaxLength(20)]
    [DataType(DataType.PostalCode)]
    [RegularExpression(@"^[A-Za-z]\d[A-Za-z][ -]?\d[A-Za-z]\d$", ErrorMessage = "Postal Code must be in the format A1A 1A1.")]
    public string? PostalCode { get; set; }
    [ProtectedPersonalData]
    [MaxLength(100)]
    [DisplayName("First Name")]
    public string? FirstName { get; set; }
    [ProtectedPersonalData]
    [MaxLength(100)]
    [DisplayName("Last Name")]
    public string? LastName { get; set; }
    [ProtectedPersonalData]
    [DisplayName("Full Name")]
    public string FullName => $"{FirstName} {LastName}".Trim();
}
