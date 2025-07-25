namespace Ticky.Base.DTOs.Trello;

public class TrelloCheckItemDTO
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("idChecklist")]
    public required string IdChecklist { get; set; }

    [JsonPropertyName("state")]
    public required string State { get; set; }
}
