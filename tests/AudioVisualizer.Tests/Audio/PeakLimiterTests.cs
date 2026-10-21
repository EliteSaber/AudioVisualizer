using AudioVisualizer.Core.Audio;

namespace AudioVisualizer.Tests.Audio;

/// <summary>
/// Ограничитель пика в превью: звук не должен выйти за предел, но тихий сигнал
/// проходить без изменений.
/// </summary>
public class PeakLimiterTests
{
    private const int SampleRate = 44100;

    [Fact]
    public void СигналГромчеПредела_Ограничивается()
    {
        var limiter = new PeakLimiter(SampleRate);

        var output = new float[1000];
        for (var i = 0; i < output.Length; i++)
        {
            output[i] = limiter.Process(2f);
        }

        Assert.All(output, sample => Assert.InRange(sample, -0.9901f, 0.9901f));
    }

    [Fact]
    public void ТихийСигнал_ПроходитБезИзменений()
    {
        var limiter = new PeakLimiter(SampleRate);

        var output = new float[100];
        for (var i = 0; i < output.Length; i++)
        {
            output[i] = limiter.Process(0.5f);
        }

        Assert.All(output, sample => Assert.InRange(sample, 0.4999f, 0.5001f));
    }

    [Fact]
    public void ПослеПикаГромкостьВозвращается()
    {
        var limiter = new PeakLimiter(SampleRate);

        Assert.InRange(limiter.Process(2f), 0.9899f, 0.9901f);

        // За две сотых секунды громкость возвращается почти к полной: иначе следующий
        // за пиком тихий кусок музыки играл бы приглушённым.
        for (var i = 0; i < SampleRate / 5; i++)
        {
            limiter.Process(0f);
        }

        Assert.InRange(limiter.Process(0.5f), 0.499f, 0.501f);
    }

    [Fact]
    public void НечисловойСэмпл_СчитаетсяТихим()
    {
        var limiter = new PeakLimiter(SampleRate);

        Assert.Equal(0f, limiter.Process(float.NaN));
        Assert.Equal(0f, limiter.Process(float.PositiveInfinity));
    }

    [Fact]
    public void СбросВозвращаетПолнуюГромкость()
    {
        var limiter = new PeakLimiter(SampleRate);

        limiter.Process(2f);
        limiter.Reset();

        Assert.InRange(limiter.Process(0.5f), 0.4999f, 0.5001f);
    }
}
