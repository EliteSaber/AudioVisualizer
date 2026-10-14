namespace AudioVisualizer.Core.Analysis;

/// <summary>
/// Сглаживание кадров спектра: рост значения идёт быстро, спад медленно, иначе столбики
/// дёргаются на каждом ударе. Первый кадр принимается без сглаживания.
/// </summary>
public sealed class SpectrumSmoother
{
    private readonly float _attack;
    private readonly float _release;
    private float[]? _previous;

    public SpectrumSmoother(float attack, float release)
    {
        _attack = Math.Clamp(attack, 0f, 1f);
        _release = Math.Clamp(release, 0f, 1f);
    }

    public void Smooth(Span<float> values)
    {
        if (_previous is null || _previous.Length != values.Length)
        {
            _previous = values.ToArray();
            return;
        }

        for (var i = 0; i < values.Length; i++)
        {
            var target = values[i];
            var current = _previous[i];
            var coefficient = target > current ? _attack : _release;

            current += (target - current) * coefficient;
            _previous[i] = current;
            values[i] = current;
        }
    }

    public void Reset() => _previous = null;
}