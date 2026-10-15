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
    private readonly SpectrumSettings _spectrum;
    private readonly SpectrumPipeline _pipeline;
    private readonly int _barCount;

    /// <summary>
    /// Полосы последнего показанного кадра. Запомнены именно полосы, а не столбики: при
    /// смене ширины столбика, промежутка, отступа или размера кадра наполнение то же самое,
    /// пересчитать надо только число столбиков.
    /// </summary>
    private float[]? _lastBands;

    /// <summary>
    /// Кадр, перенесённый из прежнего источника: отдаётся один раз, первым же запросом.
    /// </summary>
    private float[]? _replayBands;

    public SpectrumFrameSource(AudioData audio, SpectrumSettings spectrum, int barCount)
    {
        ArgumentNullException.ThrowIfNull(audio);
        ArgumentNullException.ThrowIfNull(spectrum);

        _audio = audio;
        _spectrum = spectrum;
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
        // Перенесённый кадр отдаётся один раз и без сверки с моментом: момент к этому
        // времени почти никогда не совпадает - при игре позиция уехала с прошлого тика,
        // а на паузе Play/Pause держат позицию трека, которая впереди показанного кадра.
        // Перерисовав прежний кадр, мы показываем то, что уже на экране, а куда плеер успел
        // уехать, догонит ближайший тик таймера.
        //
        // Постоянного замыкания здесь быть не должно: позиция плеера считана по прочитанным
        // блокам звука, и повторы одного момента между ними - время анимации, за которое
        // сглаживание догоняет спектр.
        if (_replayBands is not null)
        {
            var carried = _replayBands;
            _replayBands = null;

            return SpectrumResampler.Resample(carried, _barCount);
        }

        var startIndex = (int)(position.TotalSeconds * _audio.SampleRate);
        _lastBands = _pipeline.Analyze(_audio.Samples, startIndex);

        return SpectrumResampler.Resample(_lastBands, _barCount);
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

    /// <summary>
    /// Переносит состояние из другого источника: сглаживание, а если наполнение кадра не
    /// изменилось - ещё и сам кадр, чтобы перерисовка показала то же, что уже на экране.
    /// Вызывается при пересборке источника под новые настройки.
    /// </summary>
    /// <remarks>
    /// Кадр переносится не всегда. Правка усиления, гаммы, диапазона децибел, FFT или полос
    /// эквалайзера пересчитывает спектр заново - иначе столбики замерли бы до смены позиции.
    /// Атака и спад в сравнение не входят: они описывают следующий кадр, а не текущий, и
    /// пересчёт по ним сдвинул бы картинку на шаг сглаживания. Сравнение идёт по значению,
    /// поэтому переносятся и правки оформления: цвета, геометрия столбиков, размер и частота
    /// кадра.
    /// </remarks>
    public void SeedFrom(SpectrumFrameSource other)
    {
        ArgumentNullException.ThrowIfNull(other);

        _pipeline.SeedPrevious(other._pipeline);

        if (!FillsSameFrame(other))
        {
            return;
        }

        // Перенесённый кадр сразу становится последним показанным: правки настроек идут
        // цепочкой, и каждая постройка источника берёт кадр у предыдущей. Если оставить его
        // только в _replayBands, то уже вторая постройка пересчитает спектр заново и столбики
        // сдвинутся на шаг сглаживания.
        _lastBands = other._lastBands;
        _replayBands = other._lastBands;
    }

    private bool FillsSameFrame(SpectrumFrameSource other)
        => (_spectrum with { Attack = 0f, Release = 0f }) == (other._spectrum with { Attack = 0f, Release = 0f })
            && other._lastBands is not null;

    /// <summary>
    /// Переносит сглаживание из другого источника: конвейер новый, и без переноса первый кадр
    /// после пересборки прошёл бы без сглаживания.
    /// </summary>
    /// <remarks>
    /// Нужен для случаев, когда кадр переносить нельзя: спектр изменился, а сглаживание
    /// всё равно должно продолжиться с того, что на экране.
    /// </remarks>
    public void SeedPrevious(SpectrumFrameSource other)
    {
        ArgumentNullException.ThrowIfNull(other);

        _pipeline.SeedPrevious(other._pipeline);
    }
}