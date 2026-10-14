using AudioVisualizer.Core.Analysis;

namespace AudioVisualizer.Tests.Analysis;

public class SpectrumResamplerTests
{
    [Fact]
    public void Resample_СжимаетДоМеньшегоЧислаПолос_СохраняяПики()
    {
        float[] source = [0f, 0.1f, 0.9f, 0.2f, 0f, 0f, 0f, 0.5f];

        var result = SpectrumResampler.Resample(source, 4);

        Assert.Equal([0.1f, 0.9f, 0f, 0.5f], result);
    }

    [Fact]
    public void Resample_РастягиваетДоБольшегоЧислаПолос()
    {
        float[] source = [0.4f, 0.8f];

        var result = SpectrumResampler.Resample(source, 6);

        Assert.Equal(6, result.Length);
        Assert.All(result, value => Assert.True(value is 0.4f or 0.8f));
    }

    [Fact]
    public void Resample_ОдинСтолбик_БерётМаксимумПоВсемуСпектру()
    {
        float[] source = [0.2f, 0.7f, 0.3f];

        var result = SpectrumResampler.Resample(source, 1);

        Assert.Equal([0.7f], result);
    }

    [Fact]
    public void Resample_ПустойИсточник_ДаётНули()
    {
        var result = SpectrumResampler.Resample([], 3);

        Assert.Equal([0f, 0f, 0f], result);
    }

    [Fact]
    public void Resample_НекорректноеЧислоСтолбиков_Отклоняется()
        => Assert.Throws<ArgumentOutOfRangeException>(() => SpectrumResampler.Resample([0.5f], 0));

    [Fact]
    public void Resample_ПустойИсточник_ИНольСтолбиков_Отклоняется()
        => Assert.Throws<ArgumentOutOfRangeException>(() => SpectrumResampler.Resample([], 0));
}