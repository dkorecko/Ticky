namespace Ticky.Base.DTOs.Api;

public record BoardDetailDTO(
    int Id,
    string Name,
    string Description,
    string Code,
    int ProjectId,
    string ProjectName,
    bool IsAdmin,
    IReadOnlyList<ColumnDTO> Columns,
    IReadOnlyList<LabelDTO> Labels
);

public record ColumnDTO(
    int Id,
    string Name,
    int Index,
    bool Finished,
    int CardCount
);

public record LabelDTO(
    int Id,
    string Name,
    string BackgroundColor,
    string TextColor
);
