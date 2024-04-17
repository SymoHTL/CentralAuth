namespace Model.Entities;

[Table("CorsOrigins")]
public class CorsOrigin {
    [Key] [StringLength(100)] [Required] public string Origin{ get; set; } = null!;
}