using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using System.Text.Json.Serialization.Metadata;

namespace Ticky.Internal.Helpers;

/// <summary>
/// Serializes a board and everything loaded on it for the board export. Wherever a user appears in that graph
/// (members, assignees, authors of comments, activities and time records, project members), only
/// <see cref="ExportedUserProperties"/> are written. The user entity is an Identity user, so everything else on it
/// is account data that must never leave the server: the password hash, security and concurrency stamps, phone
/// number, lockout state, API tokens and verification codes.
/// </summary>
public static class BoardExportHelper
{
    /// <summary>The only properties of a user that end up in the export.</summary>
    public static readonly IReadOnlySet<string> ExportedUserProperties = new HashSet<string>
    {
        nameof(User.Id),
        nameof(User.DisplayName),
        nameof(User.Email),
    };

    private static readonly JsonSerializerOptions Options = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { RestrictUserProperties } },
    };

    /// <summary>
    /// Serializes the board (or a part of it) as the export file's JSON, with users reduced to
    /// <see cref="ExportedUserProperties"/>.
    /// </summary>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    /// <summary>
    /// An allow-list rather than a block-list: a property added to the user later stays out of the export until
    /// someone decides it belongs there. It matches the Identity base type, not just <see cref="User"/>, because
    /// the serializer goes by a property's declared type: a navigation declared as <c>IdentityUser&lt;int&gt;</c>
    /// would otherwise be written out in full.
    /// </summary>
    private static void RestrictUserProperties(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object || !typeof(IdentityUser<int>).IsAssignableFrom(typeInfo.Type))
            return;

        for (var i = typeInfo.Properties.Count - 1; i >= 0; i--)
        {
            if (!ExportedUserProperties.Contains(typeInfo.Properties[i].Name))
                typeInfo.Properties.RemoveAt(i);
        }
    }
}
