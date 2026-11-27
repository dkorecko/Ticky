using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ticky.Web.Controllers.Api;

/// <summary>
/// Ticky stores server-local times without an offset. A time sent with an offset or as UTC ("Z") is converted to it,
/// and every time comes out unmarked, as times read from the database do, so responses never carry an offset, not even
/// for a value the server just set itself, like a new attachment's <c>createdAt</c>.
/// </summary>
public sealed class ServerTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        ToServerTime(reader.GetDateTime());

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(ToServerTime(value));

    private static DateTime ToServerTime(DateTime value) =>
        DateTime.SpecifyKind(value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value, DateTimeKind.Unspecified);
}
