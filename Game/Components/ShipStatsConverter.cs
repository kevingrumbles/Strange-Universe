using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Components;

/// <summary>
/// Saves a ship's type as <c>{ "ShipTypeName": "..." }</c>, just its name. On read the name is turned back into
/// a new built-in <see cref="ShipStats"/> with <see cref="ShipStats.FromName"/>. This is the one place a ship type is made from a string.
/// </summary>
public class ShipStatsConverter : JsonConverter<ShipStats>
{
    public override ShipStats Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected an object for ShipType.");

        string name = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName && reader.GetString() == nameof(ShipStats.ShipTypeName))
            {
                reader.Read();
                name = reader.GetString();
            }
            else
            {
                reader.Skip(); // ignore the value of any other property
            }
        }
        return ShipStats.FromName(name);
    }

    public override void Write(Utf8JsonWriter writer, ShipStats value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(nameof(ShipStats.ShipTypeName), value.ShipTypeName);
        writer.WriteEndObject();
    }
}
