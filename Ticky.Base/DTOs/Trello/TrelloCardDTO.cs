namespace Ticky.Base.DTOs.Trello;

public class TrelloCardDTO
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("desc")]
    public required string Description { get; set; }

    [JsonPropertyName("closed")]
    public required bool Closed { get; set; }

    [JsonPropertyName("due")]
    public required DateTime? Due { get; set; }

    [JsonPropertyName("idList")]
    public required string IdList { get; set; }

    [JsonPropertyName("idLabels")]
    public required List<string> IdLabels { get; set; }

    [JsonPropertyName("mirrorSourceId")]
    public required string MirrorSourceId { get; set; }

    [JsonPropertyName("dueComplete")]
    public required bool DueComplete { get; set; }

    [JsonPropertyName("dateCompleted")]
    public required DateTime? DateCompleted { get; set; }

    [JsonPropertyName("dueReminder")]
    public required int? DueReminder { get; set; }

    [JsonPropertyName("idChecklists")]
    public required List<string> IdChecklists { get; set; }

    [JsonPropertyName("checkItemStates")]
    public required List<TrelloCheckItemStateDTO> CheckItemStates { get; set; }
}
