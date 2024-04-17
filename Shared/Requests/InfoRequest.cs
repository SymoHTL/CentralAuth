namespace Shared.Requests;

public sealed class InfoRequest {
    [EmailAddress] public string? NewEmail{ get; init; }
    public string? NewPassword{ get; init; }

    public string? OldPassword{ get; init; }
}