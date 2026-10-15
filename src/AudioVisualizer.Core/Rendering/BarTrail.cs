namespace AudioVisualizer.Core.Rendering;

/// <summary>
/// Призрак прежней высоты столбика: кадр гаснет не мгновенно, и за столбиком тянется след.
/// </summary>
/// <remarks>
/// Живёт в значениях, а не в пикселях кадра. Обратная связь по самому кадру (полупрозрачный
/// фон поверх следующего) дала бы тот же рисунок, но только в буфере выгрузки: на экране WPF
/// чистит грязные области сам, след бы там просто не появился, а в mp4 стоил бы смешивания
/// двух мегапикселей в управляемом коде на каждом кадре. Здесь считается одно число на
/// столбик, поэтому превью и ролик совпадают и стоят почти ничего.
/// </remarks>
public sealed class BarTrail
{
    /// <summary>Полоса короче этого считается шумом округления: её не видно, а геометрия нужна.</summary>
    public const double MinimumVisibleHeight = 0.5d;

    private float[]? _ghost;

    /// <summary>Снимок, под который состояние уже посчитано.</summary>
    private SpectrumSnapshot? _lastSnapshot;

    private double _width;
    private double _height;

    /// <summary>Есть ли чему гореть. Проверяется до расчёта геометрии полос.</summary>
    public bool IsActive => _ghost is not null;

    /// <summary>
    /// Готовит призраков к отрисовке. Состояние двигается при смене снимка, а не при каждом
    /// вызове отрисовки: WPF вправе перерисовать один и тот же кадр несколько раз (смена DPI,
    /// изменение размера, перерисовка родителя), и след сдвинулся бы на два шага за кадр.
    /// </summary>
    /// <remarks>
    /// След обнуляется при смене числа значений (поменяли ширину, промежуток, отступ или
    /// размер кадра), при смене размера кадра и при включении следа из нуля: иначе на первом
    /// кадре после правки мелькнул бы призрак по старой сетке столбиков.
    /// </remarks>
    public void Advance(SpectrumSnapshot snapshot, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var sizeChanged = _width != width || _height != height;
        var sameFrame = ReferenceEquals(snapshot, _lastSnapshot) && !sizeChanged;

        _lastSnapshot = snapshot;
        _width = width;
        _height = height;

        if (sameFrame)
        {
            return;
        }

        var values = snapshot.Values;

        if (snapshot.Bars.TrailDecayMs <= 0d)
        {
            // След выключен, и держать его в памяти незачем: без него лишнего массива на кадр
            // не появляется, а включается он без стартовой вспышки - призрак появится только
            // со второго кадра, как и положено.
            _ghost = null;

            return;
        }

        if (_ghost is null || _ghost.Length != values.Length || sizeChanged)
        {
            _ghost = values.ToArray();

            return;
        }

        // Затухание на кадр: доля следа, которая остаётся после времени кадра. Время, а не
        // кадры, поэтому частота кадров меняет скорость следа, а не его длительность.
        var frameSeconds = 1d / Math.Max(1, snapshot.FramesPerSecond);
        var remaining = (float)Math.Exp(-frameSeconds / (snapshot.Bars.TrailDecayMs / 1000d));

        for (var index = 0; index < values.Length; index++)
        {
            _ghost[index] = Math.Max(values[index], _ghost[index] * remaining);
        }
    }

    /// <summary>
    /// Высота призрака столбика: она не ниже текущей, иначе след уходил бы под столбик.
    /// Без следа или за его пределами возвращается само значение.
    /// </summary>
    public float GhostOf(int index, float value)
    {
        if (_ghost is null || index < 0 || index >= _ghost.Length)
        {
            return value;
        }

        return Math.Max(value, _ghost[index]);
    }

    /// <summary>Забывает след: следующий кадр начинается без призрака.</summary>
    public void Reset()
    {
        _ghost = null;
        _lastSnapshot = null;
        _width = 0d;
        _height = 0d;
    }
}