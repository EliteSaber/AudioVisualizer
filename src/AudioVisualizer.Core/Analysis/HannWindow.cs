namespace AudioVisualizer.Core.Analysis;

/// <summary>Периодическое окно Ханна: сглаживает края кадра и размазывает пики по соседним бинам.</summary>
public static class HannWindow
{
    /// <summary>Когерентное усиление окна: амплитуда сигнала после оконного сглаживания падает в столько раз.</summary>
    public const float CoherentGain = 0.5f;

    public static float[] Values(int size)
    {
        if (size < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "Размер окна должен быть не меньше 2.");
        }

        var window = new float[size];
        for (var i = 0; i < size; i++)
        {
            window[i] = 0.5f - (0.5f * MathF.Cos(2 * MathF.PI * i / size));
        }

        return window;
    }
}