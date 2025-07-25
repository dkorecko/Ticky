namespace Ticky.Base.DTOs.Trello
{
    public class TrelloLabelDTO
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("color")]
        public required string Color { get; set; }
    }
}
