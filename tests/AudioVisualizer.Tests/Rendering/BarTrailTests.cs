using AudioVisualizer.Core.Rendering;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Rendering;

/// <summary>
/// Затухание следа считается само по себе, без рисования: проверяется формула, сбросы при
/// смене сетки столбиков и то, что повторная отрисовка того же кадра след не двигает.
/// </summary>
public class BarTrailTests
{
    private const double Width = 800d;
    private const double Height = 450d;

    [Fact]
    public void БезСледа_ПризракаНет()
    {
        var trail = new BarTrail();

        trail.Advance(Snapshot([1f], trailDecayMs: 0d), Width, Height);

        Assert.False(trail.IsActive);
        Assert.Equal(1f, trail.GhostOf(0, 1f));
    }

    [Fact]
    public void Убывание_ОставляетПризракаВышеТекущего()
    {
        var trail = new BarTrail();

        trail.Advance(Snapshot([1f]), Width, Height);
        trail.Advance(Snapshot([0.5f]), Width, Height);

        // 60 кадров в секунду и 500 мс затухания: за кадр остаётся exp(-(1/60) / 0.5) = 0.967.
        Assert.InRange(trail.GhostOf(0, 0.5f), 0.96f, 0.98f);
    }

    [Fact]
    public void Затухание_ИдётПоВремени_АНеПоКадрам()
    {
        var rare = new BarTrail();
        var frequent = new BarTrail();

        rare.Advance(Snapshot([1f], framesPerSecond: 30), Width, Height);
        rare.Advance(Snapshot([0.5f], framesPerSecond: 30), Width, Height);

        frequent.Advance(Snapshot([1f], framesPerSecond: 120), Width, Height);
        frequent.Advance(Snapshot([0.5f], framesPerSecond: 120), Width, Height);

        // За один кадр при 30 fps следа остаётся меньше, чем при 120, но за секунду оба
        // одинаковы: поэтому длина следа не зависит от пресета выгрузки.
        Assert.True(rare.GhostOf(0, 0.5f) < frequent.GhostOf(0, 0.5f));
    }

    [Fact]
    public void ТотЖеСнимок_НеДвигаетСлед()
    {
        var trail = new BarTrail();
        var second = Snapshot([0.5f]);

        trail.Advance(Snapshot([1f]), Width, Height);
        trail.Advance(second, Width, Height);
        var once = trail.GhostOf(0, 0.5f);

        trail.Advance(second, Width, Height);

        // WPF вправе перерисовать один кадр несколько раз; без такой проверки след на каждой
        // лишней перерисовке уезжал бы ещё на шаг.
        Assert.Equal(once, trail.GhostOf(0, 0.5f));
    }

    [Fact]
    public void ВключениеСледа_ПервыйКадрБезПризрака()
    {
        var trail = new BarTrail();

        trail.Advance(Snapshot([1f], trailDecayMs: 0d), Width, Height);
        trail.Advance(Snapshot([0.5f]), Width, Height);

        Assert.Equal(0.5f, trail.GhostOf(0, 0.5f));
    }

    [Fact]
    public void СменаЧислаСтолбиков_СбрасываетСлед()
    {
        var trail = new BarTrail();

        trail.Advance(Snapshot([1f, 1f]), Width, Height);
        trail.Advance(Snapshot([0.5f, 0.5f, 0.5f]), Width, Height);

        // Поменяли ширину, промежуток или отступ, и призрак по старой сетке столбиков мелькнул бы.
        Assert.Equal(0.5f, trail.GhostOf(0, 0.5f));
    }

    [Fact]
    public void СменаРазмераКадра_СбрасываетСлед()
    {
        var trail = new BarTrail();

        trail.Advance(Snapshot([1f]), Width, Height);
        trail.Advance(Snapshot([0.5f]), 1920d, 1080d);

        Assert.Equal(0.5f, trail.GhostOf(0, 0.5f));
    }

    [Fact]
    public void Reset_ЗабываетСлед()
    {
        var trail = new BarTrail();

        trail.Advance(Snapshot([1f]), Width, Height);
        trail.Reset();
        trail.Advance(Snapshot([0.5f]), Width, Height);

        Assert.Equal(0.5f, trail.GhostOf(0, 0.5f));
    }

    [Fact]
    public void ПризракНеНижеСтолбика()
    {
        var trail = new BarTrail();

        trail.Advance(Snapshot([0.2f]), Width, Height);
        trail.Advance(Snapshot([1f]), Width, Height);

        // Столбик вырос: след был бы под ним, поэтому виден только сам столбик.
        Assert.Equal(1f, trail.GhostOf(0, 1f));
    }

    private static SpectrumSnapshot Snapshot(float[] values, double trailDecayMs = 500d, int framesPerSecond = 60)
        => new(values, new ColorSettings(), new BarSettings { TrailDecayMs = trailDecayMs }, framesPerSecond);
}