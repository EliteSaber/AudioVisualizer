using AudioVisualizer.Core.Analysis;
using AudioVisualizer.Core.Audio;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Core.Export;

/// <summary>
/// Высоты столбиков для произвольного момента трека.
/// </summary>
/// <remarks>
/// Превью и выгрузка видео берут кадры отсюда: раздельный расчёт развёл бы картинку в mp4
/// и на экране, а сглаживание по соседним кадрам вообще сделало бы их несопоставимыми.
/// </remarks>
public sealed class SpectrumFrameSource
{
    private readonly AudioData _audio;
    private readonly SpectrumPipeline _pipeline;
    private readonly int _barCount;

    public SpectrumFrameSource(AudioData audio, SpectrumSettings spectrum, int barCount)
    {
        ArgumentNullException.ThrowIfNull(audio);
        ArgumentNullException.ThrowIfNull(spectrum);

        _audio = audio;
        _pipeline = new SpectrumPipeline(spectrum, audio.SampleRate);
        _barCount = Math.Max(1, barCount);
    }

    public int BarCount => _barCount;

    /// <summary>
    /// Высоты столбиков в момент <paramref name="position"/>. За треком окно дополняется
    /// нулями, поэтому момент за концом файла даёт тишину, а не исключение.
    /// </summary>
    public float[] GetBars(TimeSpan position)
    {
        var startIndex = (int)(position.TotalSeconds * _audio.SampleRate);

        return _pipeline.AnalyzeBars(_audio.Samples, startIndex, _barCount);
    }

    /// <summary>
    /// Число кадров на трек. Округление вверх: короткий последний кадр лучше, чем обрыв
    /// видео на пару кадров раньше конца аудио.
    /// </summary>
    public static int FrameCount(TimeSpan duration, int framesPerSecond)
    {
        if (framesPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond), framesPerSecond, "Частота кадров должна быть положительной.");
        }

        return Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds * framesPerSecond));
    }

    /// <summary>Момент, который показывает кадр с номером <paramref name="frameIndex"/>.</summary>
    public static TimeSpan FrameTime(int frameIndex, int framesPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(framesPerSecond, 1);

        return TimeSpan.FromSeconds(frameIndex / (double)framesPerSecond);
    }
}