namespace AudioVisualizer.Core.Analysis;

/// <summary>
/// Разбиение оси частот на полосы по логарифмической шкале: низкие частоты получают
/// узкие полосы, высокие — широкие, как в любом музыкальном эквалайзере.
/// </summary>
public sealed class SpectrumBandMap
{
    public SpectrumBandMap(float minFrequencyHz, float maxFrequencyHz, int bandCount, float binWidthHz, int binCount)
    {
        if (minFrequencyHz <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minFrequencyHz), minFrequencyHz, "Нижняя частота должна быть положительной.");
        }

        if (maxFrequencyHz <= minFrequencyHz)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFrequencyHz), maxFrequencyHz, "Верхняя частота должна быть выше нижней.");
        }

        if (bandCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(bandCount), bandCount, "Число полос должно быть не меньше 1.");
        }

        if (binWidthHz <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(binWidthHz), binWidthHz, "Ширина бина должна быть положительной.");
        }

        if (binCount < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(binCount), binCount, "Число бинов должно быть не меньше 2.");
        }

        var logMin = Math.Log(minFrequencyHz);
        var logSpan = Math.Log(maxFrequencyHz) - logMin;

        // Бин 0 - постоянная составляющая, её в спектре не показываем.
        var lastBin = binCount - 1;

        var bands = new Band[bandCount];
        for (var i = 0; i < bandCount; i++)
        {
            var lowFrequency = Math.Exp(logMin + (logSpan * i / bandCount));
            var highFrequency = Math.Exp(logMin + (logSpan * (i + 1) / bandCount));

            var lowBin = Math.Clamp((int)Math.Round(lowFrequency / binWidthHz), 1, lastBin);
            var highBin = Math.Clamp((int)Math.Round(highFrequency / binWidthHz) - 1, lowBin, lastBin);
            var center = (float)Math.Exp(logMin + (logSpan * (i + 0.5) / bandCount));

            bands[i] = new Band(lowBin, highBin, center);
        }

        Bands = bands;
    }

    public IReadOnlyList<Band> Bands { get; }

    public int BandCount => Bands.Count;

    public readonly record struct Band(int LowBin, int HighBin, float CenterFrequencyHz);
}