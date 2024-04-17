namespace Shared.Dtos;

public sealed class UserInfoDto {
    public required bool IsEmailConfirmed{ get; init; }
    public required List<ClaimDto> ClaimDtos{ get; init; }

    // client-side computed properties
    private List<Claim>? _claims;

    public List<Claim> Claims => _claims ??=
        ClaimDtos.Select(c => new Claim(c.Type, c.Value)).ToList();


    public string GetUsername() => GetClaimValueOrDefault(ClaimTypes.Name) ?? GetEmail();
    public string GetEmail() => GetClaimValueOrDefault(ClaimTypes.Email) ?? "Unknown";

    private string? GetClaimValueOrDefault(string claimType) =>
        Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
}