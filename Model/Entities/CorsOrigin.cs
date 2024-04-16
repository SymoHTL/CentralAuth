namespace Model.Entities;

[Table("CorsOrigins")]
public class CorsOrigin {
    [StringLength(100)] [Required] public string Origin{ get; set; } = null!;
}