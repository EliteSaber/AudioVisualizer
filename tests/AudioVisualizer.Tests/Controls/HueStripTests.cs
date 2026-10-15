using System.Windows;
using System.Windows.Media;
using AudioVisualizer.App.Controls;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Controls;

/// <summary>
/// Полоса тона: перевод точки в градусы и содержимое рисунка.
/// </summary>
/// <remarks>
/// Градиент собирается из <see cref="HsvColor.HueRamp"/>, поэтому проверяется именно он, а не
/// числа в разметке: тот же список тонов используется и в проверках круга.
/// </remarks>
[Collection(WpfUi.Name)]
public class HueStripTests
{
    private const double Width = 236d;
    private const double Height = 18d;

    [Fact]
    public void Полоса_СоздаётсяИИзмеряется() => Sta.Run(() =>
    {
        var strip = Create();

        Assert.Equal(Width, strip.ActualWidth, 3);
        Assert.Equal(Height, strip.ActualHeight, 3);
    });

    [Fact]
    public void ЦентрПолосы_ДаётТон180() => Sta.Run(() =>
    {
        var strip = Create();

        strip.Pick(new Point(Width / 2d, Height / 2d));

        Assert.Equal(180d, strip.Hue, 3);
    });

    [Fact]
    public void КраяПолосы_ДаютКрасный() => Sta.Run(() =>
    {
        // Начало и конец круга — один и тот же тон: красным должен быть и левый край.
        var strip = Create();

        strip.Pick(new Point(0d, 0d));

        Assert.Equal(0d, strip.Hue, 3);

        strip.Pick(new Point(Width, 0d));

        Assert.Equal(0d, strip.Hue, 3);
    });

    [Fact]
    public void ЧетвертьПолосы_ДаётТон90() => Sta.Run(() =>
    {
        var strip = Create();

        strip.Pick(new Point(Width / 4d, 0d));

        Assert.Equal(90d, strip.Hue, 3);
    });

    [Fact]
    public void ТочкаЗаПределамиПолосы_Зажимается() => Sta.Run(() =>
    {
        var strip = Create();

        strip.Pick(new Point(-Width, 0d));

        Assert.Equal(0d, strip.Hue, 3);

        strip.Pick(new Point(Width * 3d, 0d));

        Assert.Equal(0d, strip.Hue, 3);
    });

    [Fact]
    public void ТонВнеКруга_Оборачивается() => Sta.Run(() =>
    {
        var strip = Create();

        strip.Hue = 400d;

        Assert.Equal(40d, strip.Hue, 3);
    });

    [Fact]
    public void Полоса_РисуетКругТонов() => Sta.Run(() =>
    {
        var drawings = DrawingsOf(Create());
        var ramp = Assert.IsType<LinearGradientBrush>(drawings[0].Brush);

        Assert.Equal(HsvColor.RampSteps, ramp.GradientStops.Count);
        Assert.Equal(0d, ramp.GradientStops[0].Offset, 3);
        Assert.Equal(1d, ramp.GradientStops[^1].Offset, 3);
        Assert.Equal(Colors.Red, ramp.GradientStops[0].Color);
        Assert.Equal(Colors.Red, ramp.GradientStops[^1].Color);
    });

    [Fact]
    public void Отметка_СтоитНаВыбранномТоне() => Sta.Run(() =>
    {
        var strip = Create();

        strip.Hue = 90d;

        var marker = MarkerOf(strip);

        Assert.Equal(Width / 4d, marker.X + (marker.Width / 2d), 3);
        Assert.Equal(0d, marker.Y, 3);
        Assert.Equal(Height, marker.Height, 3);
    });

    [Fact]
    public void Выбор_СообщаетОПравке() => Sta.Run(() =>
    {
        var strip = Create();
        var picks = 0;

        strip.Picked += (_, _) => picks++;
        strip.Pick(new Point(Width / 2d, Height / 2d));

        Assert.Equal(1, picks);
    });

    private static TestStrip Create()
    {
        var strip = new TestStrip { Width = Width, Height = Height };
        strip.Measure(new Size(Width, Height));
        strip.Arrange(new Rect(0d, 0d, Width, Height));

        return strip;
    }

    /// <summary>
    /// Габариты индикатора: у него две линии разной толщины, и обе они без кисти, поэтому
    /// ищется самая толстая из них.
    /// </summary>
    private static Rect MarkerOf(TestStrip strip)
        => DrawingsOf(strip)
            .Where(drawing => drawing.Brush is null)
            .OrderByDescending(drawing => drawing.Geometry!.Bounds.Width)
            .First().Geometry!.Bounds;

    private static GeometryDrawing[] DrawingsOf(TestStrip strip)
    {
        var visual = new DrawingVisual();

        using (var context = visual.RenderOpen())
        {
            strip.Render(context);
        }

        return [.. visual.Drawing?.Children.OfType<GeometryDrawing>() ?? []];
    }

    /// <summary>Рисунок доступен извне только через защищённый <c>OnRender</c>.</summary>
    private sealed class TestStrip : HueStrip
    {
        public void Render(DrawingContext context) => OnRender(context);
    }
}