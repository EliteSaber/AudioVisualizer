using System.Text.Json;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Settings;

public class RgbaColorTests
{
    [Fact]
    public void Parse_ШестизначныйЦвет_СчитаетсяНепрозрачным()
    {
        var color = RgbaColor.Parse("#3884FF");

        Assert.Equal(new RgbaColor(0x38, 0x84, 0xFF, 0xFF), color);
    }

    [Fact]
    public void Parse_ВосемьзначныйЦвет_ЧитаетАльфу()
    {
        var color = RgbaColor.Parse("#3884FF80");

        Assert.Equal(new RgbaColor(0x38, 0x84, 0xFF, 0x80), color);
    }

    [Fact]
    public void Parse_БезПрефиксаИСПробелами_Работает() => Assert.Equal(new RgbaColor(1, 2, 3, 4), RgbaColor.Parse("  01020304 "));

    [Fact]
    public void Parse_Null_ВыбрасываетArgumentNullException()
        => Assert.Throws<ArgumentNullException>(() => RgbaColor.Parse(null!));

    [Theory]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("не цвет")]
    [InlineData("#GGHHIIJJ")]
    public void Parse_НекорректныйТекст_ВыбрасываетFormatException(string text)
        => Assert.Throws<FormatException>(() => RgbaColor.Parse(text));

    [Fact]
    public void TryParse_НекорректныйТекст_ВозвращаетFalse()
    {
        Assert.False(RgbaColor.TryParse("ерунда", out _));
        Assert.False(RgbaColor.TryParse(null, out _));
    }

    [Fact]
    public void ToHex_ВозвращаетВосемьЗнаков()
    {
        Assert.Equal("#3884FF80", new RgbaColor(0x38, 0x84, 0xFF, 0x80).ToHex());
        Assert.Equal("#00000000", RgbaColor.Transparent.ToHex());
    }

    [Fact]
    public void Сериализация_ПишетЦветСтрокой()
    {
        var json = JsonSerializer.Serialize(new RgbaColor(0x38, 0x84, 0xFF, 0x80));

        Assert.Equal("\"#3884FF80\"", json);
    }

    [Fact]
    public void Десериализация_ЧитаетЦветИзСтроки()
    {
        var color = JsonSerializer.Deserialize<RgbaColor>("\"#11223344\"");

        Assert.Equal(new RgbaColor(0x11, 0x22, 0x33, 0x44), color);
    }

    [Fact]
    public void Faded_ГаситТолькоПрозрачность()
    {
        Assert.Equal(0, new RgbaColor(10, 20, 30, 255).Faded(0d).A);
        Assert.Equal(204, new RgbaColor(10, 20, 30, 255).Faded(0.8d).A);
        Assert.Equal(102, new RgbaColor(10, 20, 30, 128).Faded(0.8d).A);
    }

    [Fact]
    public void Faded_БольшеЕдиницы_ОставляетЦветИ_ЗажимаетАльфу()
    {
        var faded = new RgbaColor(10, 20, 30, 255).Faded(4d);

        Assert.Equal(255, faded.A);
        Assert.Equal(10, faded.R);
        Assert.Equal(20, faded.G);
        Assert.Equal(30, faded.B);
    }
}