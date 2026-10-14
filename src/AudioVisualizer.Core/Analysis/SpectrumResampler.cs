namespace AudioVisualizer.Core.Analysis;

/// <summary>Пересчёт полос анализа в число столбиков: каждое значение берётся как максимум по своему диапазону.</summary>
public static class SpectrumResampler
{
    public static float[] Resample(float[] values, int targetCount)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (targetCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(targetCount), targetCount, "Число столбиков должно быть не меньше 1.");
        }

        var result = new float[targetCount];
        if (values.Length == 0)
        {
            return result;
        }

        for (var i = 0; i < targetCount; i++)
        {
            var low = (int)((long)i * values.Length / targetCount);
            var high = (int)Math.Ceiling((i + 1) * (double)values.Length / targetCount) - 1;
            high = Math.Min(Math.Max(high, low), values.Length - 1);

            var peak = 0f;
            for (var j = low; j <= high; j++)
            {
                peak = Math.Max(peak, values[j]);
            }

            result[i] = peak;
        }

        return result;
    }
}