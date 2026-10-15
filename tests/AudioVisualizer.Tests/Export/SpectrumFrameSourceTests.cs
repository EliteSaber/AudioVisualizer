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

    [Fact]
    public void GetBars_ТотЖеМомент_ПродолжаетСглаживание()
    {
        // Тон, потом тишина: хвост падает на релиз при каждом кадре.
        var source = CreateSource(Tone(TimeSpan.FromSeconds(0.5), 1000d));
        source.GetBars(TimeSpan.FromSeconds(0.1d));

        var first = source.GetBars(TimeSpan.FromSeconds(0.6d));
        var second = source.GetBars(TimeSpan.FromSeconds(0.6d));

        // Позиция плеера считана по прочитанным блокам звука, и между ними превью рисует
        // один момент несколько раз: эти повторы и есть время анимации. Если источник
        // отдаёт по ним один и тот же кадр, столбики стоят и дёргаются на границе блока.
        Assert.InRange(first.Max(), 0.75f, 0.85f);
        Assert.InRange(second.Max(), 0.55f, 0.7f);
    }

    [Fact]
    public void SeedFrom_КадрПереносится()
    {
        // Тон, потом тишина: столбик сначала полный, потом падает на релиз и держит хвост.
        var audio = Tone(TimeSpan.FromSeconds(0.5), 1000d);
        var warm = CreateSource(audio);
        warm.GetBars(TimeSpan.FromSeconds(0.1d));
        warm.GetBars(TimeSpan.FromSeconds(0.6d));

        var rebuilt = CreateSource(audio);
        rebuilt.SeedFrom(warm);

        // Показан прежний хвост, а не результат ещё одного шага сглаживания. Момент при этом
        // может быть любым: пока он на экране, ползунок стоит на нём же.
        Assert.InRange(rebuilt.GetBars(TimeSpan.FromSeconds(0.7d)).Max(), 0.75f, 0.85f);

        // Дальше источник считает как обычно: перенос не должен замораживать картинку.
        Assert.InRange(rebuilt.GetBars(TimeSpan.FromSeconds(0.7d)).Max(), 0.55f, 0.7f);
    }

    [Fact]
    public void SeedFrom_ЦепочкаПравок_КадрДоходитДоКонца()
    {
        var audio = Tone(TimeSpan.FromSeconds(0.5), 1000d);
        var first = CreateSource(audio);
        first.GetBars(TimeSpan.FromSeconds(0.1d));
        first.GetBars(TimeSpan.FromSeconds(0.6d));

        // Правки настроек идут цепочкой, и каждая постройка берёт кадр у предыдущей.
        var second = CreateSource(audio);
        second.SeedFrom(first);
        var third = CreateSource(audio);
        third.SeedFrom(second);
        var fourth = CreateSource(audio);
        fourth.SeedFrom(third);

        Assert.InRange(second.GetBars(TimeSpan.FromSeconds(0.6d)).Max(), 0.75f, 0.85f);
        Assert.InRange(third.GetBars(TimeSpan.FromSeconds(0.6d)).Max(), 0.75f, 0.85f);
        Assert.InRange(fourth.GetBars(TimeSpan.FromSeconds(0.6d)).Max(), 0.75f, 0.85f);
    }

    [Fact]
    public void SeedFrom_СпектрИзменён_КадрНеПереносится()
    {
        var audio = Tone(TimeSpan.FromSeconds(1), 1000d);
        var warm = CreateSource(audio);
        warm.GetBars(TimeSpan.FromSeconds(0.5d));

        // Диапазон децибел сдвинут так, что тот же тон в него больше не попадает.
        var quiet = new SpectrumFrameSource(
            audio,
            SpectrumSettings.Default with { MinDecibels = 0f, MaxDecibels = 1f },
            barCount: 64);

        quiet.SeedFrom(warm);

        // Кадр пересчитан, сглаживание продолжено: столбик падает на релиз, но не до нуля.
        // Перенесённый кадр показал бы единицу, а сырой без переноса - ноль.
        Assert.InRange(quiet.GetBars(TimeSpan.FromSeconds(0.5d)).Max(), 0.7f, 0.9f);
    }

    [Fact]
    public void SeedFrom_СпектрИзменён_СглаживаниеПереносится()
    {
        var audio = Tone(TimeSpan.FromSeconds(0.5), 1000d);
        var warm = CreateSource(audio);
        warm.GetBars(TimeSpan.FromSeconds(0.1d));
        warm.GetBars(TimeSpan.FromSeconds(0.6d));

        var rebuilt = CreateSource(audio);
        rebuilt.SeedPrevious(warm);

        Assert.InRange(rebuilt.GetBars(TimeSpan.FromSeconds(0.6d)).Max(), 0.55f, 0.7f);
    }

    [Fact]
    public void SeedFrom_АтакаИСпад_КадрПереносится()
    {
        var audio = Tone(TimeSpan.FromSeconds(0.5), 1000d);
        var warm = CreateSource(audio);
        warm.GetBars(TimeSpan.FromSeconds(0.1d));
        warm.GetBars(TimeSpan.FromSeconds(0.6d));

        // Коэффициенты сглаживания описывают следующий кадр, а не текущий, поэтому правка
        // любого из них не должна пересчитывать показанное.
        var rebuilt = new SpectrumFrameSource(
            audio,
            SpectrumSettings.Default with { Attack = 0.9f, Release = 0.6f },
            barCount: 64);

        rebuilt.SeedFrom(warm);

        Assert.InRange(rebuilt.GetBars(TimeSpan.FromSeconds(0.6d)).Max(), 0.75f, 0.85f);
    }

    [Fact]
    public void SeedFrom_ДругоеЧислоПолос_КадрНеПереносится()
    {
        // Перенос между конвейерами с разным числом полос не должен ломать расчёт: при
        // несовпадении длины сглаживание начинается заново, кадр берётся как есть.
        var rebuilt = new SpectrumFrameSource(
            Tone(TimeSpan.FromSeconds(1), 440d),
            SpectrumSettings.Default with { AnalysisBandCount = 128 },
            barCount: 64);

        rebuilt.SeedFrom(CreateSource(Tone(TimeSpan.FromSeconds(1), 1000d)));
        var values = rebuilt.GetBars(TimeSpan.FromSeconds(0.5d));

        Assert.Equal(64, values.Length);
        Assert.InRange(values.Max(), 0.95f, 1f);
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