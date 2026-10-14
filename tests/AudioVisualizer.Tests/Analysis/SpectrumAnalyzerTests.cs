using AudioVisualizer.Core.Analysis;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Analysis;

public class SpectrumAnalyzerTests
{
    private const int SampleRate = 44100;
    private const int FftSize = 4096;

    [Fact]
    public void AnalyzeDecibels_ТонНаЧастотеБина_ДаётАмплитудуВДецибелах()
    {
        // Бин 93 при 44100 Гц и окне 4096 - это 1001 Гц, тон попадает точно в бин.
        var analyzer = new SpectrumAnalyzer(SpectrumSettings.Default, SampleRate);
        var samples = TestSignal.SineOnBin(SampleRate, 93, FftSize, FftSize * 4, 0.5f);

        var decibels = analyzer.AnalyzeDecibels(samples, FftSize);

        var peak = Array.IndexOf(decibels, decibels.Max());
        var center = analyzer.BandMap.Bands[peak].CenterFrequencyHz;

        Assert.InRange(center, 900f, 1100f);
        Assert.InRange(decibels[peak], -7.5f, -4.5f);
    }

    [Fact]
    public void AnalyzeDecibels_Тишина_ДаётЗначенияНижеМинусВосьмидесяти()
    {
        var analyzer = new SpectrumAnalyzer(SpectrumSettings.Default, SampleRate);

        var decibels = analyzer.AnalyzeDecibels(new float[FftSize * 4], 0);

        Assert.All(decibels, value => Assert.True(value < -80f, $"Полоса показала {value} дБ вместо тишины."));
    }

    [Fact]
    public void AnalyzeDecibels_ОкноЗаПределамиСигнала_НеПадает()
    {
        var analyzer = new SpectrumAnalyzer(SpectrumSettings.Default, SampleRate);
        var samples = TestSignal.SineOnBin(SampleRate, 93, FftSize, FftSize * 4, 0.5f);

        var decibels = analyzer.AnalyzeDecibels(samples, -FftSize * 2);

        Assert.All(decibels, value => Assert.True(value < -80f));
    }

    [Fact]
    public void AnalyzeDecibels_ТонВышеДиапазона_НеПопадаетВПолосы()
    {
        // Бин 1800 при тех же параметрах - это 19.4 кГц, далеко за верхней границей 8 кГц.
        var settings = SpectrumSettings.Default with { MaxFrequencyHz = 8000f };
        var analyzer = new SpectrumAnalyzer(settings, SampleRate);
        var samples = TestSignal.SineOnBin(SampleRate, 1800, FftSize, FftSize * 4, 0.5f);

        var decibels = analyzer.AnalyzeDecibels(samples, FftSize);

        // Сам тон даёт около -6 дБ, сюда должна доходить только утечка боковых лепестков окна.
        Assert.All(decibels, value => Assert.True(value < -70f, $"Полоса показала {value} дБ вместо утечки."));
    }

    [Fact]
    public void AnalyzeDecibels_ЧислоПолос_СоответствуетНастройке()
    {
        var analyzer = new SpectrumAnalyzer(SpectrumSettings.Default with { AnalysisBandCount = 128 }, SampleRate);

        Assert.Equal(128, analyzer.BandCount);
        Assert.Equal(128, analyzer.AnalyzeDecibels(new float[FftSize * 4], 0).Length);
    }

    [Fact]
    public void Constructor_ОтклоняетНекорректнуюЧастотуДискретизации()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new SpectrumAnalyzer(SpectrumSettings.Default, 0));
}