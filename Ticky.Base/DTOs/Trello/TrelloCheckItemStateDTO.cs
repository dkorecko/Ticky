namespace Ticky.Base.DTOs.Trello;

public class TrelloCheckItemStateDTO
{
    [JsonPropertyName("idCheckItem")]
    public required string IdCheckItem { get; set; }

    [JsonPropertyName("state")]
    public required string State { get; set; }
}
