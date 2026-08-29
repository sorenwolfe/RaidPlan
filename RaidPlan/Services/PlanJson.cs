using System;
using System.Globalization;
using System.Numerics;
using Newtonsoft.Json;

namespace RaidPlan.Services;

/// <summary>
/// Vector2 as a two-element array rounded to 4dp. The default object form costs ~20 characters a
/// point, which adds up fast in a freehand stroke.
/// </summary>
public sealed class CompactVector2Converter : JsonConverter<Vector2>
{
    public override void WriteJson(JsonWriter writer, Vector2 value, JsonSerializer serializer)
    {
        writer.WriteStartArray();
        writer.WriteValue(MathF.Round(value.X, 4));
        writer.WriteValue(MathF.Round(value.Y, 4));
        writer.WriteEndArray();
    }

    public override Vector2 ReadJson(JsonReader reader, Type objectType, Vector2 existingValue,
        bool hasExistingValue, JsonSerializer serializer)
    {
        switch (reader.TokenType)
        {
            case JsonToken.Null:
                return Vector2.Zero;

            case JsonToken.StartArray:
            {
                var x = ReadFloat(reader);
                var y = ReadFloat(reader);
                while (reader.TokenType != JsonToken.EndArray && reader.Read())
                {
                    // Skip any extra elements a future format might add.
                }

                return new Vector2(x, y);
            }

            case JsonToken.StartObject:
            {
                // Older plans, and anything hand-edited, use {"X":..,"Y":..}.
                float x = 0, y = 0;
                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType != JsonToken.PropertyName)
                        continue;

                    var name = (string?)reader.Value ?? string.Empty;
                    var value = ReadFloat(reader);
                    if (name.Equals("X", StringComparison.OrdinalIgnoreCase))
                        x = value;
                    else if (name.Equals("Y", StringComparison.OrdinalIgnoreCase))
                        y = value;
                }

                return new Vector2(x, y);
            }

            default:
                return Vector2.Zero;
        }
    }

    private static float ReadFloat(JsonReader reader)
    {
        if (!reader.Read())
            return 0f;

        return reader.Value switch
        {
            double d => (float)d,
            long l => l,
            decimal m => (float)m,
            string s when float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0f,
        };
    }
}

/// <summary>Serializer settings shared by the on-disk library and the share codes.</summary>
public static class PlanJson
{
    /// <summary>For share codes: defaulted properties are dropped and filled back in on read.</summary>
    public static JsonSerializerSettings Compact() => new()
    {
        Formatting = Formatting.None,
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
        Converters = { new CompactVector2Converter() },
    };

    /// <summary>Readable settings used for the JSON files in the plugin's config directory.</summary>
    public static JsonSerializerSettings Readable() => new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
        Converters = { new CompactVector2Converter() },
    };
}
