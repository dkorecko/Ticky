namespace Ticky.Base.DTOs.Trello;

public class TrelloMemberDTO
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("fullName")]
    public required string FullName { get; set; }

    [JsonPropertyName("username")]
    public required string Username { get; set; }
}
