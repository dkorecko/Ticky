namespace Ticky.Base.DTOs.Trello;

public class TrelloPreferencesDTO
{
    [JsonPropertyName("selfJoin")]
    public required bool SelfJoin { get; set; }
}
