namespace Shared.Requests;

public sealed class ResetPasswordRequest : IRequest {
    [Required, EmailAddress] public required string Email{ get; set; }

    [Required] public required string ResetCode{ get; set; }

    [Required] public required string NewPassword{ get; set; }

    public void Sanitize() {
    }
}