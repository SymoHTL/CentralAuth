namespace Shared.Requests;

public sealed class TwoFactorRequest : IRequest {
    public bool? Enable{ get; set; }

    public string? TwoFactorCode{ get; set; }

    public bool ResetSharedKey{ get; set; }

    public bool ResetRecoveryCodes{ get; set; }

    public bool ForgetMachine{ get; set; }

    public void Sanitize() {
        TwoFactorCode = TwoFactorCode?.Trim();
    }
}