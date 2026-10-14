using AudioVisualizer.Core.Analysis;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Analysis;

public class SpectrumPipelineTests
{
    private const int SampleRate = 44100;
    private const int FftSize = 4096;

    [Fact]
    public void Analyze_Тишина_ДаётНулевыеВысоты()
    {
        var pipeline = new SpectrumPipeline(SpectrumSettings.Default, SampleRate);

        var values = pipeline.Analyze(new float[SampleRate], 0);

        Assert.All(values, value => Assert.True(value < 0.001f));
    }

    [Fact]
    public void Analyze_ГромкийТон_ДаётВысокиеЗначения()
    {
        var pipeline = new SpectrumPipeline(SpectrumSettings.Default, SampleRate);
        var samples = TestSignal.SineOnBin(SampleRate, 93, FftSize, FftSize * 4, 0.5f);

        var values = pipeline.Analyze(samples, FftSize);

        Assert.True(values.Max() > 0.99f);
        Assert.All(values, value => Assert.InRange(value, 0f, 1f));
    }

    [Fact]
    public void Analyze_ЭквалайзерУсиливаетПолосу_СтолбикСтановитсяВыше()
    {
        var settings = SpectrumSettings.Default with
        {
            Equalizer = SpectrumSettings.DefaultEqualizer
                .Select(band => band.CenterFrequencyHz == 1000f ? band with { GainDb = 12f } : band)
                .ToArray()
        };
        var samples = TestSignal.SineOnBin(SampleRate, 93, FftSize, FftSize * 4, 0.0005f);

        var flat = new SpectrumPipeline(SpectrumSettings.Default, SampleRate).Analyze(samples, FftSize).Max();
        var boosted = new SpectrumPipeline(settings, SampleRate).Analyze(samples, FftSize).Max();

        Assert.True(boosted > flat);
    }

    [Fact]
    public void AnalyzeBars_ВозвращаетЗапрошенноеЧислоСтолбиков()
    {
        var pipeline = new SpectrumPipeline(SpectrumSettings.Default, SampleRate);
        var samples = TestSignal.SineOnBin(SampleRate, 93, FftSize, FftSize * 4, 0.5f);

        var bars = pipeline.AnalyzeBars(samples, FftSize, 64);

        Assert.Equal(64, bars.Length);
        Assert.All(bars, value => Assert.InRange(value, 0f, 1f));
    }

    [Fact]
    public void BandCount_СоответствуетНастройке()
    {
        var pipeline = new SpectrumPipeline(SpectrumSettings.Default with { AnalysisBandCount = 96 }, SampleRate);

        Assert.Equal(96, pipeline.BandCount);
    }

    [Fact]
    public void Reset_ОчищаетСглаживание()
    {
        var pipeline = new SpectrumPipeline(SpectrumSettings.Default, SampleRate);
        var loud = TestSignal.SineOnBin(SampleRate, 93, FftSize, FftSize * 4, 0.5f);
        pipeline.Analyze(loud, FftSize);

        var silence = new float[FftSize * 4];
        var faded = pipeline.Analyze(silence, 0);
        Assert.Contains(faded, value => value > 0.01f);

        pipeline.Reset();
        var fresh = pipeline.Analyze(silence, 0);

        Assert.All(fresh, value => Assert.True(value < 0.001f));
    }
}