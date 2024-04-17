namespace Shared.Requests;

public sealed class RefreshRequest : IRequest {
    [Required] public required string RefreshToken{ get; set; }

    public void Sanitize() {
        RefreshToken = RefreshToken.Trim();
    }
}