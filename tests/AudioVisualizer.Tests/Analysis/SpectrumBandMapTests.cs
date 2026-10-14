using AudioVisualizer.Core.Analysis;

namespace AudioVisualizer.Tests.Analysis;

public class SpectrumBandMapTests
{
    private static SpectrumBandMap CreateMap(int bandCount = 64, float minFrequencyHz = 30f, float maxFrequencyHz = 16000f)
        => new(minFrequencyHz, maxFrequencyHz, bandCount, 44100f / 4096f, 2048);

    [Fact]
    public void Constructor_ЗадаётЗапрошенноеЧислоПолос() => Assert.Equal(64, CreateMap().BandCount);

    [Fact]
    public void Bands_ЦентрыСтрогоВозрастают()
    {
        var bands = CreateMap().Bands;

        for (var i = 1; i < bands.Count; i++)
        {
            Assert.True(bands[i].CenterFrequencyHz > bands[i - 1].CenterFrequencyHz,
                $"Центр полосы {i} ({bands[i].CenterFrequencyHz}) должен быть выше предыдущей ({bands[i - 1].CenterFrequencyHz}).");
        }
    }

    [Fact]
    public void Bands_ЦентрыЛежат_ВЗаданномДиапазоне()
    {
        foreach (var band in CreateMap().Bands)
        {
            Assert.InRange(band.CenterFrequencyHz, 30f, 16000f);
        }
    }

    [Fact]
    public void Bands_НижниеГраницы_НеУбывают()
    {
        var bands = CreateMap().Bands;

        for (var i = 1; i < bands.Count; i++)
        {
            Assert.True(bands[i].LowBin >= bands[i - 1].LowBin);
        }
    }

    [Fact]
    public void Bands_ПропускаютПостояннуюСоставляющую() => Assert.True(CreateMap().Bands[0].LowBin >= 1);

    [Fact]
    public void Bands_КаждаяПолосаСодержитХотяБыОдинБин()
    {
        foreach (var band in CreateMap(bandCount: 4096).Bands)
        {
            Assert.True(band.HighBin >= band.LowBin);
        }
    }

    [Theory]
    [InlineData(0f, 16000f)]
    [InlineData(-10f, 16000f)]
    [InlineData(16000f, 16000f)]
    [InlineData(20000f, 16000f)]
    public void Constructor_ОтклоняетНекорректныйДиапазон(float minFrequencyHz, float maxFrequencyHz)
        => Assert.Throws<ArgumentOutOfRangeException>(() => CreateMap(minFrequencyHz: minFrequencyHz, maxFrequencyHz: maxFrequencyHz));

    [Fact]
    public void Constructor_ОтклоняетНекорректноеЧислоПолос()
        => Assert.Throws<ArgumentOutOfRangeException>(() => CreateMap(bandCount: 0));

    [Fact]
    public void Constructor_ОтклоняетНекорректнуюШиринуБина()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new SpectrumBandMap(30f, 16000f, 64, 0f, 2048));

    [Fact]
    public void Constructor_ОтклоняетМалоеЧислоБинов()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new SpectrumBandMap(30f, 16000f, 64, 10f, 1));
}