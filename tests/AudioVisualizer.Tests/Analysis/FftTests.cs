using AudioVisualizer.Core.Analysis;

namespace AudioVisualizer.Tests.Analysis;

public class FftTests
{
    [Fact]
    public void Forward_ВыделяетПик_В_ОжидаемомБине()
    {
        const int size = 1024;
        const int bin = 100;
        var samples = TestSignal.SineOnBin(size, bin, size, size, 1f);

        var real = samples.ToArray();
        var imaginary = new float[size];
        new Fft(size).Forward(real, imaginary);

        var peakBin = 0;
        var peakMagnitude = 0f;
        for (var i = 0; i < size / 2; i++)
        {
            var magnitude = MathF.Sqrt((real[i] * real[i]) + (imaginary[i] * imaginary[i]));
            if (magnitude > peakMagnitude)
            {
                peakMagnitude = magnitude;
                peakBin = i;
            }
        }

        Assert.Equal(bin, peakBin);
        Assert.Equal(size / 2f, peakMagnitude, 2);
    }

    [Fact]
    public void Forward_СохраняетЭнергию_ПоПарсивалю()
    {
        const int size = 512;
        var samples = TestSignal.SineOnBin(size, 37, size, size, 0.8f);

        var timeEnergy = samples.Sum(value => value * value);
        var real = samples.ToArray();
        var imaginary = new float[size];
        new Fft(size).Forward(real, imaginary);
        var spectrumEnergy = 0f;
        for (var i = 0; i < size; i++)
        {
            spectrumEnergy += (real[i] * real[i]) + (imaginary[i] * imaginary[i]);
        }

        Assert.Equal(timeEnergy * size, spectrumEnergy, 1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(100)]
    [InlineData(0)]
    public void Constructor_ОтклоняетНеСтепеньДвойки(int size)
        => Assert.Throws<ArgumentException>(() => new Fft(size));

    [Fact]
    public void Forward_ОтклоняетБуферКорочеРазмера()
    {
        var fft = new Fft(64);

        Assert.Throws<ArgumentException>(() => fft.Forward(new float[32], new float[64]));
        Assert.Throws<ArgumentException>(() => fft.Forward(new float[64], new float[32]));
    }

    [Fact]
    public void Size_ВозвращаетПереданноеЗначение() => Assert.Equal(2048, new Fft(2048).Size);
}