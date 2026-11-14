namespace Ticky.Base.DTOs.Api;

/// <summary>
/// Response for minting a new API token. <see cref="Token"/> is shown to the user once at creation
/// and never returned again — the server only stores a hash of it.
/// </summary>
public record ApiTokenMintResponse(int Id, string Name, string Token, DateTime CreatedAt);
