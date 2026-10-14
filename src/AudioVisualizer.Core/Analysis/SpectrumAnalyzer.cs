using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Core.Analysis;

/// <summary>
/// Окно + FFT + разбиение на полосы. На входе отрезок моно-сигнала, на выходе громкость
/// каждой полосы в децибелах. Окно выходит за пределы сигнала, края дополняются нулями.
/// </summary>
public sealed class SpectrumAnalyzer
{
    private const float Epsilon = 1e-9f;

    private readonly Fft _fft;
    private readonly float[] _window;
    private readonly SpectrumBandMap _bandMap;
    private readonly float[] _real;
    private readonly float[] _imaginary;
    private readonly float _normalization;

    public SpectrumAnalyzer(SpectrumSettings settings, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Частота дискретизации должна быть положительной.");
        }

        var fftSize = settings.FftSize;
        _fft = new Fft(fftSize);
        _window = HannWindow.Values(fftSize);
        _bandMap = new SpectrumBandMap(
            settings.MinFrequencyHz,
            settings.MaxFrequencyHz,
            settings.AnalysisBandCount,
            sampleRate / (float)fftSize,
            fftSize / 2);
        _real = new float[fftSize];
        _imaginary = new float[fftSize];

        // Пик бина для синуса амплитуды A равен A * fftSize / 2 * когерентное усиление окна.
        _normalization = 1f / ((fftSize / 2f) * HannWindow.CoherentGain);
    }

    public SpectrumBandMap BandMap => _bandMap;

    public int BandCount => _bandMap.BandCount;

    public float[] AnalyzeDecibels(ReadOnlySpan<float> samples, int startIndex)
    {
        for (var i = 0; i < _real.Length; i++)
        {
            var index = startIndex + i;
            var value = index >= 0 && index < samples.Length ? samples[index] : 0f;
            _real[i] = value * _window[i];
            _imaginary[i] = 0f;
        }

        _fft.Forward(_real, _imaginary);

        var result = new float[_bandMap.BandCount];
        for (var band = 0; band < result.Length; band++)
        {
            var range = _bandMap.Bands[band];

            var peak = 0f;
            for (var bin = range.LowBin; bin <= range.HighBin; bin++)
            {
                var magnitude = MathF.Sqrt((_real[bin] * _real[bin]) + (_imaginary[bin] * _imaginary[bin]));
                peak = Math.Max(peak, magnitude);
            }

            result[band] = 20f * MathF.Log10((peak * _normalization) + Epsilon);
        }

        return result;
    }
}