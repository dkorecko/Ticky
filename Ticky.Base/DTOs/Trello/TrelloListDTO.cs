namespace Ticky.Base.DTOs.Trello;

public class TrelloListDTO
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("closed")]
    public bool Closed { get; set; }

    [JsonPropertyName("softLimit")]
    public int? SoftLimit { get; set; }
}
