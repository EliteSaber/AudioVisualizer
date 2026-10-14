using AudioVisualizer.Core.Audio;
using AudioVisualizer.Core.Export;
using AudioVisualizer.Core.Settings;
using AudioVisualizer.Tests.Analysis;

namespace AudioVisualizer.Tests.Export;

/// <summary>
/// Кадры по времени трека: из них собирается и превью, и видео, поэтому расчёт должен
/// совпадать с тем, что делает окно.
/// </summary>
public class SpectrumFrameSourceTests
{
    [Fact]
    public void GetBars_ОдинСинус_ДаётПикВСвоейПолосе()
    {
        var source = CreateSource(Tone(TimeSpan.FromSeconds(1), 1000d));

        var values = source.GetBars(TimeSpan.FromSeconds(0.5d));

        Assert.Equal(source.BarCount, values.Length);

        var peak = Array.IndexOf(values, values.Max());

        // Полосы идут логарифмически от 30 до 16000 Гц, поэтому 1 кГц - это чуть правее
        // середины. Точный индекс здесь не проверяется: он зависит от числа полос анализа.
        Assert.InRange(peak, values.Length / 3, (values.Length * 3) / 4);
        Assert.True(values[peak] > 0.5f);

        // В верхней части спектра сигнала нет: пик обязан быть локальным, иначе проверка
        // проходила бы и на шуме.
        Assert.True(values[^1] < 0.2f);
    }

    [Fact]
    public void GetBars_ТаЖеСекунда_ТотЖеКадр()
    {
        var source = CreateSource(Tone(TimeSpan.FromSeconds(1), 440d));

        var first = source.GetBars(TimeSpan.FromSeconds(0.25d));
        var second = source.GetBars(TimeSpan.FromSeconds(0.25d));

        Assert.Equal(first, second);
    }

    [Fact]
    public void GetBars_ЗаКонцомТрека_ДаётТишину()
    {
        var source = CreateSource(Tone(TimeSpan.FromSeconds(1), 440d));

        var values = source.GetBars(TimeSpan.FromSeconds(30d));

        Assert.All(values, value => Assert.InRange(value, 0f, 1f));
        Assert.True(values.Max() < 0.5f);
    }

    [Fact]
    public void GetBars_БольшеСтолбиков_РовноСтолькоСколькоПросят()
    {
        var source = CreateSource(Tone(TimeSpan.FromSeconds(1), 440d), barCount: 24);

        Assert.Equal(24, source.GetBars(TimeSpan.Zero).Length);
    }

    [Theory]
    [InlineData(1d, 30, 30)]
    [InlineData(1d, 60, 60)]
    [InlineData(2.5d, 30, 75)]
    [InlineData(0.01d, 30, 1)]
    public void FrameCount_ОкруглениеВверх(double seconds, int framesPerSecond, int expected)
        => Assert.Equal(expected, SpectrumFrameSource.FrameCount(TimeSpan.FromSeconds(seconds), framesPerSecond));

    [Fact]
    public void FrameTime_РавномернаяСетка()
    {
        Assert.Equal(TimeSpan.Zero, SpectrumFrameSource.FrameTime(0, 30));
        Assert.Equal(TimeSpan.FromSeconds(1d / 30d), SpectrumFrameSource.FrameTime(1, 30));
        Assert.Equal(TimeSpan.FromSeconds(2d), SpectrumFrameSource.FrameTime(60, 30));
    }

    [Fact]
    public void НеположительнаяЧастотаКадров_Отклоняется()
        => Assert.Throws<ArgumentOutOfRangeException>(() => SpectrumFrameSource.FrameCount(TimeSpan.FromSeconds(1), 0));

    [Fact]
    public void ОтрицательныйНомерКадра_Отклоняется()
        => Assert.Throws<ArgumentOutOfRangeException>(() => SpectrumFrameSource.FrameTime(-1, 30));

    private static SpectrumFrameSource CreateSource(AudioData audio, int barCount = 64)
        => new(audio, SpectrumSettings.Default, barCount);

    /// <summary>Синус нужной частоты ровно на частоте развёртки FFT: без утёчки в соседние полосы.</summary>
    private static AudioData Tone(TimeSpan duration, double frequencyHz)
    {
        const int sampleRate = 44100;
        var fftSize = SpectrumSettings.Default.FftSize;

        return new AudioData(
            TestSignal.SineOnBin(sampleRate, (int)(frequencyHz * fftSize / sampleRate), fftSize, (int)(duration.TotalSeconds * sampleRate), 0.8f),
            sampleRate);
    }
}