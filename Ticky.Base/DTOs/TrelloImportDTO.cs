using Ticky.Base.DTOs.Trello;

namespace Ticky.Base.DTOs;

public class TrelloImportDTO
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("desc")]
    public required string Description { get; set; }

    [JsonPropertyName("starred")]
    public required bool Starred { get; set; }

    [JsonPropertyName("prefs")]
    public required TrelloPreferencesDTO Preferences { get; set; }

    [JsonPropertyName("labelNames")]
    public required TrelloLabelNamesDTO LabelNames { get; set; }

    [JsonPropertyName("lists")]
    public required List<TrelloListDTO> Lists { get; set; }

    [JsonPropertyName("cards")]
    public required List<TrelloCardDTO> Cards { get; set; }

    [JsonPropertyName("checklists")]
    public required List<TrelloChecklistDTO> Checklists { get; set; }

    [JsonPropertyName("labels")]
    public required List<TrelloLabelDTO> Labels { get; set; }
}
