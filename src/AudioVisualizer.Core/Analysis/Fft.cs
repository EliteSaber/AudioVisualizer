namespace AudioVisualizer.Core.Analysis;

/// <summary>
/// Преобразование Фурье radix-2 с перестановкой битов и предвычисленными таблицами.
/// Буферы передаёт вызывающий: внутри выделяются только таблицы, зависящие от размера.
/// </summary>
public sealed class Fft
{
    private readonly int[] _bitReversed;
    private readonly float[] _cosine;
    private readonly float[] _sine;

    public Fft(int size)
    {
        if (size < 2 || (size & (size - 1)) != 0)
        {
            throw new ArgumentException($"Размер FFT должен быть степенью двойки не меньше 2, получено {size}.", nameof(size));
        }

        Size = size;

        var bits = (int)Math.Log2(size);
        _bitReversed = new int[size];
        for (var i = 0; i < size; i++)
        {
            var reversed = 0;
            for (var bit = 0; bit < bits; bit++)
            {
                if ((i & (1 << bit)) != 0)
                {
                    reversed |= 1 << (bits - 1 - bit);
                }
            }

            _bitReversed[i] = reversed;
        }

        _cosine = new float[size / 2];
        _sine = new float[size / 2];
        for (var k = 0; k < size / 2; k++)
        {
            var angle = -2.0 * Math.PI * k / size;
            _cosine[k] = (float)Math.Cos(angle);
            _sine[k] = (float)Math.Sin(angle);
        }
    }

    public int Size { get; }

    public void Forward(Span<float> real, Span<float> imaginary)
    {
        if (real.Length < Size)
        {
            throw new ArgumentException($"Буфер вещественной части короче размера FFT ({real.Length} < {Size}).", nameof(real));
        }

        if (imaginary.Length < Size)
        {
            throw new ArgumentException($"Буфер мнимой части короче размера FFT ({imaginary.Length} < {Size}).", nameof(imaginary));
        }

        for (var i = 0; i < Size; i++)
        {
            var j = _bitReversed[i];
            if (j <= i)
            {
                continue;
            }

            (real[i], real[j]) = (real[j], real[i]);
            (imaginary[i], imaginary[j]) = (imaginary[j], imaginary[i]);
        }

        for (var length = 2; length <= Size; length <<= 1)
        {
            var half = length >> 1;
            var step = Size / length;
            for (var start = 0; start < Size; start += length)
            {
                for (int offset = 0, twiddle = 0; offset < half; offset++, twiddle += step)
                {
                    float cos = _cosine[twiddle];
                    float sin = _sine[twiddle];

                    var even = start + offset;
                    var odd = even + half;

                    var productReal = (real[odd] * cos) - (imaginary[odd] * sin);
                    var productImaginary = (real[odd] * sin) + (imaginary[odd] * cos);

                    real[odd] = real[even] - productReal;
                    imaginary[odd] = imaginary[even] - productImaginary;
                    real[even] += productReal;
                    imaginary[even] += productImaginary;
                }
            }
        }
    }
}