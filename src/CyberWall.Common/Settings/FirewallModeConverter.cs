using System.Text.Json;
using System.Text.Json.Serialization;
using CyberWall.Common.Models;

namespace CyberWall.Common.Settings;

/// <summary>
/// Stores the firewall mode by name so the meaning of the value never depends on the
/// order of the enum. Files written by older versions used the numeric value, which is
/// still read and converted.
/// </summary>
public sealed class FirewallModeConverter : JsonConverter<FirewallMode>
{
    public override FirewallMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            // Legacy files stored the numeric value of the enum.
            var legacy = reader.GetInt32();
            return Enum.IsDefined(typeof(FirewallMode), legacy)
                ? (FirewallMode)legacy
                : FirewallMode.Ask;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString();
            if (Enum.TryParse<FirewallMode>(text, ignoreCase: true, out var parsed))
                return parsed;
        }

        return FirewallMode.Ask;
    }

    public override void Write(Utf8JsonWriter writer, FirewallMode value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
