namespace AudioVisualizer.Core.Analysis;

/// <summary>Перевод децибел в нормированную высоту столбика с обрезкой по краям диапазона и нелинейностью.</summary>
public sealed class DecibelMapper
{
    private readonly float _minDecibels;
    private readonly float _range;
    private readonly float _gainDb;
    private readonly float _gamma;

    public DecibelMapper(float minDecibels, float maxDecibels, float gainDb, float gamma)
    {
        if (maxDecibels <= minDecibels)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDecibels), maxDecibels, "Верхняя граница децибел должна быть выше нижней.");
        }

        if (gamma <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gamma), gamma, "Показатель нелинейности должен быть положительным.");
        }

        _minDecibels = minDecibels;
        _range = maxDecibels - minDecibels;
        _gainDb = gainDb;
        _gamma = gamma;
    }

    public float Map(float decibels)
    {
        var linear = Math.Clamp((decibels + _gainDb - _minDecibels) / _range, 0f, 1f);
        return MathF.Pow(linear, _gamma);
    }

    public void Map(ReadOnlySpan<float> decibels, Span<float> destination)
    {
        if (destination.Length < decibels.Length)
        {
            throw new ArgumentException($"Приёмник короче источника ({destination.Length} < {decibels.Length}).", nameof(destination));
        }

        for (var i = 0; i < decibels.Length; i++)
        {
            destination[i] = Map(decibels[i]);
        }
    }
}