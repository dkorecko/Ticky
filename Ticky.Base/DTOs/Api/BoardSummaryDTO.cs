namespace Ticky.Base.DTOs.Api;

/// <summary>Shape returned by <c>GET /api/v1/boards</c>. Scoped, flat, no nested collections.</summary>
public record BoardSummaryDTO(
    int Id,
    string Name,
    string Description,
    string Code,
    int ProjectId,
    string ProjectName,
    bool IsAdmin
);
