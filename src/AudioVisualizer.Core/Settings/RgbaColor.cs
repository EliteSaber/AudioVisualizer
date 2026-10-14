using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioVisualizer.Core.Settings;

/// <summary>Цвет в виде четырёх байтов: в файле настроек хранится строкой <c>#RRGGBBAA</c>.</summary>
[JsonConverter(typeof(RgbaColorJsonConverter))]
public readonly record struct RgbaColor(byte R, byte G, byte B, byte A = 255)
{
    public static RgbaColor Transparent => new(0, 0, 0, 0);

    public static RgbaColor Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var value = text.Trim().TrimStart('#');

        if (value.Length == 6)
        {
            value += "FF";
        }

        if (value.Length != 8 || !uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Цвет \"{text}\" не похож на #RRGGBB или #RRGGBBAA.");
        }

        return new RgbaColor(
            (byte)((parsed >> 24) & 0xFF),
            (byte)((parsed >> 16) & 0xFF),
            (byte)((parsed >> 8) & 0xFF),
            (byte)(parsed & 0xFF));
    }

    public static bool TryParse(string? text, out RgbaColor color)
    {
        if (text is null)
        {
            color = default;
            return false;
        }

        try
        {
            color = Parse(text);
            return true;
        }
        catch (FormatException)
        {
            color = default;
            return false;
        }
    }

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}{A:X2}";

    public override string ToString() => ToHex();
}

public sealed class RgbaColorJsonConverter : JsonConverter<RgbaColor>
{
    public override RgbaColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => RgbaColor.Parse(reader.GetString() ?? throw new FormatException("Цвет не может быть пустым."));

    public override void Write(Utf8JsonWriter writer, RgbaColor value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToHex());
}