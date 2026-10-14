using AudioVisualizer.Core.Analysis;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Analysis;

public class EqualizerProcessorTests
{
    private static readonly EqualizerBand[] TwoBands =
    [
        new(100f, 0f),
        new(1000f, 6f)
    ];

    [Fact]
    public void GainAt_ВЦентреПолосы_ВозвращаетУсилениеПолосы()
    {
        Assert.Equal(0f, EqualizerProcessor.GainAt(TwoBands, 100f));
        Assert.Equal(6f, EqualizerProcessor.GainAt(TwoBands, 1000f));
    }

    [Fact]
    public void GainAt_МеждуПолосами_ИнтерполируетПоЛогШкале()
    {
        // Геометрическая середина между 100 и 1000 Гц, по лог-шкале это ровно половина пути.
        var gain = EqualizerProcessor.GainAt(TwoBands, MathF.Sqrt(100f * 1000f));

        Assert.Equal(3f, gain, 3);
    }

    [Fact]
    public void GainAt_ВнеДиапазона_ОграничиваетКрайнейПолосой()
    {
        Assert.Equal(0f, EqualizerProcessor.GainAt(TwoBands, 20f));
        Assert.Equal(6f, EqualizerProcessor.GainAt(TwoBands, 20000f));
    }

    [Fact]
    public void GainAt_ПустойСписокПолос_ВозвращаетНоль()
        => Assert.Equal(0f, EqualizerProcessor.GainAt([], 1000f));

    [Fact]
    public void Apply_ПрибавляетУсиление_ККаждойПолосе()
    {
        var processor = new EqualizerProcessor([100f, 1000f], TwoBands);
        var decibels = new[] { -30f, -30f };

        processor.Apply(decibels);

        Assert.Equal(-30f, decibels[0], 3);
        Assert.Equal(-24f, decibels[1], 3);
    }

    [Fact]
    public void Apply_НулевыеУсиления_НеМеняютЗначения()
    {
        var processor = new EqualizerProcessor([100f, 1000f], [new EqualizerBand(100f, 0f), new EqualizerBand(1000f, 0f)]);
        var decibels = new[] { -42f, -11f };

        processor.Apply(decibels);

        Assert.Equal([-42f, -11f], decibels);
    }

    [Fact]
    public void Apply_ОтклоняетЧислоПолос_НеСовпадающее_СЧисломЗначений()
    {
        var processor = new EqualizerProcessor([100f, 1000f], TwoBands);

        Assert.Throws<ArgumentException>(() => processor.Apply([-30f, -30f, -30f]));
    }

    [Fact]
    public void Constructor_ОтклоняетНеотсортированныеПолосы()
    {
        EqualizerBand[] unsorted = [new(1000f, 0f), new(100f, 6f)];

        Assert.Throws<ArgumentException>(() => new EqualizerProcessor([500f], unsorted));
    }
}