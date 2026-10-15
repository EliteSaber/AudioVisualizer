using AudioVisualizer.Core.Analysis;

namespace AudioVisualizer.Tests.Analysis;

public class SpectrumSmootherTests
{
    [Fact]
    public void Smooth_ПервыйКадр_ПроходитБезСглаживания()
    {
        var smoother = new SpectrumSmoother(attack: 0.5f, release: 0.1f);
        var frame = new[] { 0.2f, 0.8f };

        smoother.Smooth(frame);

        Assert.Equal([0.2f, 0.8f], frame);
    }

    [Fact]
    public void Smooth_Рост_ИдётСАтаку()
    {
        var smoother = new SpectrumSmoother(attack: 0.5f, release: 0.1f);
        var first = new[] { 0f };
        smoother.Smooth(first);

        var second = new[] { 1f };
        smoother.Smooth(second);
        Assert.Equal(0.5f, second[0], 5);

        var third = new[] { 1f };
        smoother.Smooth(third);
        Assert.Equal(0.75f, third[0], 5);
    }

    [Fact]
    public void Smooth_Спад_ИдётСРелизом()
    {
        var smoother = new SpectrumSmoother(attack: 1f, release: 0.25f);
        var first = new[] { 1f };
        smoother.Smooth(first);

        var second = new[] { 0f };
        smoother.Smooth(second);

        Assert.Equal(0.75f, second[0], 5);
    }

    [Fact]
    public void Smooth_КоэффициентыОбрезаютсяВДиапазон()
    {
        // Атака выше единицы даёт мгновенный рост, релиз ниже нуля - значение замирает.
        var smoother = new SpectrumSmoother(attack: 5f, release: -3f);
        var first = new[] { 0.5f };
        smoother.Smooth(first);

        var rise = new[] { 1f };
        smoother.Smooth(rise);
        Assert.Equal(1f, rise[0], 5);

        var fall = new[] { 0f };
        smoother.Smooth(fall);
        Assert.Equal(1f, fall[0], 5);
    }

    [Fact]
    public void Smooth_ДругаяДлинаКадра_НачинаетЗаново()
    {
        var smoother = new SpectrumSmoother(attack: 0.5f, release: 0.5f);
        var first = new[] { 0f, 0f, 0f };
        smoother.Smooth(first);

        var shorter = new[] { 0.9f };
        smoother.Smooth(shorter);

        Assert.Equal([0.9f], shorter);
    }

    [Fact]
    public void Reset_ОчищаетСостояние()
    {
        var smoother = new SpectrumSmoother(attack: 0.5f, release: 0.1f);
        var first = new[] { 0f };
        smoother.Smooth(first);
        smoother.Smooth([1f]);

        smoother.Reset();
        var afterReset = new[] { 0.4f };
        smoother.Smooth(afterReset);

        Assert.Equal([0.4f], afterReset);
    }

    [Fact]
    public void SeedPrevious_КадрПродолжаетсяОтПрежнего()
    {
        var warm = new SpectrumSmoother(attack: 0.5f, release: 0.25f);
        warm.Smooth([1f]);

        // Пересобранный сглаживатель без переноса взял бы первый кадр как есть.
        var rebuilt = new SpectrumSmoother(attack: 0.5f, release: 0.25f);
        rebuilt.SeedPrevious(warm);

        var frame = new[] { 0f };
        rebuilt.Smooth(frame);

        Assert.Equal(0.75f, frame[0], 5);
    }

    [Fact]
    public void SeedPrevious_ПустоеСостояние_ОстаётсяПервыйКадрКакЕсть()
    {
        var fresh = new SpectrumSmoother(attack: 0.5f, release: 0.25f);
        var rebuilt = new SpectrumSmoother(attack: 0.5f, release: 0.25f);

        rebuilt.SeedPrevious(fresh);
        var frame = new[] { 0.3f };
        rebuilt.Smooth(frame);

        Assert.Equal([0.3f], frame);
    }

    [Fact]
    public void SeedPrevious_ДругаяДлинаКадра_НачинаетЗаново()
    {
        var warm = new SpectrumSmoother(attack: 0.5f, release: 0.25f);
        warm.Smooth([1f, 1f, 1f]);

        var rebuilt = new SpectrumSmoother(attack: 0.5f, release: 0.25f);
        rebuilt.SeedPrevious(warm);

        var frame = new[] { 0f };
        rebuilt.Smooth(frame);

        Assert.Equal([0f], frame);
    }
}