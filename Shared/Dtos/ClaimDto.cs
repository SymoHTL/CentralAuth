namespace Shared.Dtos;

public sealed class ClaimDto {
    public required string Type{ get; init; }
    public required string Value{ get; init; }
}