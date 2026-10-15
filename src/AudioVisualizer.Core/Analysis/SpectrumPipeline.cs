using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Core.Analysis;

/// <summary>
/// Полный расчёт одного кадра визуализации: анализ, визуальный EQ, перевод в высоту
/// и сглаживание. Превью и экспорт видео считают кадры только здесь, поэтому картинка
/// в mp4 совпадает с тем, что пользователь видел на экране.
/// </summary>
public sealed class SpectrumPipeline
{
    private readonly SpectrumAnalyzer _analyzer;
    private readonly EqualizerProcessor _equalizer;
    private readonly DecibelMapper _mapper;
    private readonly SpectrumSmoother _smoother;

    public SpectrumPipeline(SpectrumSettings settings, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _analyzer = new SpectrumAnalyzer(settings, sampleRate);
        _mapper = new DecibelMapper(settings.MinDecibels, settings.MaxDecibels, settings.GainDb, settings.Gamma);
        _smoother = new SpectrumSmoother(settings.Attack, settings.Release);

        var bandCenters = _analyzer.BandMap.Bands.Select(band => band.CenterFrequencyHz).ToArray();
        _equalizer = new EqualizerProcessor(bandCenters, settings.Equalizer);
    }

    public int BandCount => _analyzer.BandCount;

    public float[] Analyze(ReadOnlySpan<float> samples, int startIndex)
    {
        var frame = _analyzer.AnalyzeDecibels(samples, startIndex);
        _equalizer.Apply(frame);
        _mapper.Map(frame, frame);
        _smoother.Smooth(frame);
        return frame;
    }

    /// <summary>Значения аналитических полос для текущего столбика в виде высоты от 0 до 1.</summary>
    public float[] AnalyzeBars(ReadOnlySpan<float> samples, int startIndex, int barCount)
        => SpectrumResampler.Resample(Analyze(samples, startIndex), barCount);

    public void Reset() => _smoother.Reset();

    /// <summary>
    /// Переносит предыдущий кадр из другого конвейера. Вызывается при пересборке источника
    /// под новые настройки: конвейер новый, а сглаживание должно продолжиться с того же кадра.
    /// </summary>
    public void SeedPrevious(SpectrumPipeline other)
    {
        ArgumentNullException.ThrowIfNull(other);

        _smoother.SeedPrevious(other._smoother);
    }
}