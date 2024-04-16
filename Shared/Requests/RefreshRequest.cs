namespace Shared.Requests;

public class RefreshRequest : IRequest {
    [Required]
    public required string RefreshToken { get; set; }

    public void Sanitize() {
        RefreshToken = RefreshToken.Trim();
    }
}