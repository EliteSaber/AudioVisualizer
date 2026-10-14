using AudioVisualizer.Core.Analysis;

namespace AudioVisualizer.Tests.Analysis;

public class DecibelMapperTests
{
    private static DecibelMapper CreateMapper(float gamma = 1f, float gainDb = 0f)
        => new(minDecibels: -70f, maxDecibels: -10f, gainDb: gainDb, gamma: gamma);

    [Fact]
    public void Map_НижняяГраница_ДаётНоль() => Assert.Equal(0f, CreateMapper().Map(-70f));

    [Fact]
    public void Map_ВерхняяГраница_ДаётЕдиницу() => Assert.Equal(1f, CreateMapper().Map(-10f));

    [Fact]
    public void Map_СерединаДиапазона_ДаётПоловину() => Assert.Equal(0.5f, CreateMapper().Map(-40f), 5);

    [Fact]
    public void Map_ТишеНижнейГраницы_ОбрезаетсяВНоль() => Assert.Equal(0f, CreateMapper().Map(-120f));

    [Fact]
    public void Map_ГромчеВерхнейГраницы_ОбрезаетсяВЕдиницу() => Assert.Equal(1f, CreateMapper().Map(0f));

    [Fact]
    public void Map_Усиление_СдвигаетЗначение()
    {
        Assert.Equal(1f, CreateMapper(gainDb: 10f).Map(-20f), 5);
        Assert.Equal(0f, CreateMapper(gainDb: -10f).Map(-70f));
    }

    [Fact]
    public void Map_ГаммаБольшеЕдиницы_СжимаетСередину() => Assert.Equal(0.25f, CreateMapper(gamma: 2f).Map(-40f), 5);

    [Fact]
    public void Map_ГаммаМеньшеЕдиницы_РастягиваетСередину() => Assert.Equal(0.707f, CreateMapper(gamma: 0.5f).Map(-40f), 3);

    [Fact]
    public void Map_ОстаётсяМонотонной()
    {
        var mapper = CreateMapper();

        var previous = -1f;
        for (var decibels = -80f; decibels <= 0f; decibels += 1f)
        {
            var value = mapper.Map(decibels);
            Assert.True(value >= previous, $"Значение упало на {decibels} дБ.");
            previous = value;
        }
    }

    [Fact]
    public void Map_ЗаполняетПриёмник_Поэлементно()
    {
        var mapper = CreateMapper();
        float[] source = [-70f, -40f, -10f];
        var destination = new float[3];

        mapper.Map(source, destination);

        Assert.Equal([0f, 0.5f, 1f], destination);
    }

    [Fact]
    public void Constructor_ОтклоняетПеревёрнутыйДиапазон()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new DecibelMapper(-10f, -70f, 0f, 1f));

    [Fact]
    public void Constructor_ОтклоняетНеположительнуюГамму()
        => Assert.Throws<ArgumentOutOfRangeException>(() => CreateMapper(gamma: 0f));
}