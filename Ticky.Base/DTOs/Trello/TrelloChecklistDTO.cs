namespace Ticky.Base.DTOs.Trello;

public class TrelloChecklistDTO
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("idCard")]
    public required string IdCard { get; set; }

    [JsonPropertyName("checkItems")]
    public required List<TrelloCheckItemDTO> CheckItems { get; set; }
}
