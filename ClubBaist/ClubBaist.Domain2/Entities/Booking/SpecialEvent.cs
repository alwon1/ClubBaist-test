using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClubBaist.Domain2;

public class SpecialEvent
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; init; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = default!;

    [Required]
    public DateTime Start { get; init; }

    [Required]
    public DateTime End { get; init; }
}
