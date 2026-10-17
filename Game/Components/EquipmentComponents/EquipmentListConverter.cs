using Strange_Universe.Game.Entities.ShipParts;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Components.EquipmentComponents;

/// <summary>
/// Serializes <see cref="Ship.Equipment"/> as a JSON array of name strings.
/// On read each name is turned into the full item with <see cref="Equipment.Create"/>.
/// </summary>
public class EquipmentListConverter : JsonConverter<List<EquipmentComponent>>
{
    public override List<EquipmentComponent> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = new List<EquipmentComponent>();
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected start of array for Equipment.");

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            string name = reader.GetString()
                ?? throw new JsonException("Expected a non-null equipment name string.");

            // Each ship gets its own copy; an unknown name becomes an inert placeholder that keeps its name.
            list.Add(EquipmentComponent.Create(name));
        }
        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<EquipmentComponent> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
            writer.WriteStringValue(item.EquipmentName);
        writer.WriteEndArray();
    }
}
