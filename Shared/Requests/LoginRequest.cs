namespace Shared.Requests;

public sealed class LoginRequest : IRequest {
    [EmailAddress, Required] public required string Email{ get; set; }
    [Required] public required string Password{ get; set; }

    public string? TwoFactorCode{ get; set; }

    public string? TwoFactorRecoveryCode{ get; set; }

    public void Sanitize() {
        Email = Email.Trim();
        Password = Password.Trim();
        TwoFactorCode = TwoFactorCode?.Trim();
        TwoFactorRecoveryCode = TwoFactorRecoveryCode?.Trim();
    }
}