using System.Windows;
using System.Windows.Media;
using AudioVisualizer.Core.Rendering;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Rendering;

[Collection(WpfUi.Name)]
public class SpectrumElementTests
{
    private const double FrameWidth = 800d;
    private const double FrameHeight = 450d;

    private static TestElement CreateElement(SpectrumSnapshot? snapshot)
    {
        var element = new TestElement { Width = FrameWidth, Height = FrameHeight };
        element.Snapshot = snapshot;
        element.Measure(new Size(FrameWidth, FrameHeight));
        element.Arrange(new Rect(0d, 0d, FrameWidth, FrameHeight));

        return element;
    }

    private static SpectrumSnapshot Snapshot(
        float[] values,
        Func<BarSettings, BarSettings>? configureBars = null,
        ColorSettings? colors = null)
    {
        var bars = new BarSettings
        {
            Width = 20,
            Spacing = 4,
            Margin = 20,
            CenterGap = 4,
            CornerRadius = 6,
            CornerStyle = CornerStyle.OuterRounded,
            FillMode = BarFillMode.Solid,
            BorderWidth = 0d
        };

        return new SpectrumSnapshot(values, colors ?? new ColorSettings(), configureBars is null ? bars : configureBars(bars));
    }

    /// <summary>Рисует элемент в отдельный DrawingVisual и отдаёт геометрии, которые он вывел.</summary>
    private static GeometryDrawing[] DrawingsOf(TestElement element)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            element.Render(context);
        }

        return visual.Drawing?.Children.OfType<GeometryDrawing>().ToArray() ?? [];
    }

    /// <summary>Столбики рисуются геометрией, фон и центральная линия - прямоугольниками.</summary>
    private static GeometryDrawing[] BarsOf(TestElement element)
        => DrawingsOf(element).Where(drawing => drawing.Geometry is not RectangleGeometry).ToArray();

    [Fact]
    public void БезСнимка_НичегоНеРисует() => Sta.Run(() => Assert.Empty(DrawingsOf(CreateElement(null))));

    [Fact]
    public void Фон_ЗаливаетВесьКадр() => Sta.Run(() =>
    {
        var drawings = DrawingsOf(CreateElement(Snapshot([])));

        var background = Assert.Single(drawings);
        Assert.IsType<RectangleGeometry>(background.Geometry);
        Assert.Equal(new Rect(0d, 0d, FrameWidth, FrameHeight), background.Geometry.Bounds);
    });

    [Fact]
    public void Столбик_РисуетсяВверхИВнизОтЦентра() => Sta.Run(() =>
    {
        var bars = BarsOf(CreateElement(Snapshot([1f])));

        Assert.Equal(2, bars.Length);
        Assert.Equal(bars[0].Geometry.Bounds.Height, bars[1].Geometry.Bounds.Height, 5);
        Assert.Equal(bars[0].Geometry.Bounds.Width, bars[1].Geometry.Bounds.Width, 5);
    });

    [Fact]
    public void Столбик_ЗеркаленОтносительноЦентра() => Sta.Run(() =>
    {
        var bars = BarsOf(CreateElement(Snapshot([1f])));

        // Отступ 20, центр 225, половина зазора 2: верхний край 20, нижний 430.
        Assert.Equal(20d, bars[0].Geometry.Bounds.Top, 5);
        Assert.Equal(FrameHeight - 20d, bars[1].Geometry.Bounds.Bottom, 5);
        Assert.Equal(FrameHeight, bars[0].Geometry.Bounds.Top + bars[1].Geometry.Bounds.Bottom, 5);
    });

    [Fact]
    public void БезЗеркала_РисуетсяТолькоВерхняяПоловина() => Sta.Run(() =>
    {
        var bars = BarsOf(CreateElement(Snapshot([1f], bars => bars with { Mirror = false })));

        Assert.Single(bars);
        Assert.True(bars[0].Geometry.Bounds.Top < FrameHeight / 2d);
    });

    [Fact]
    public void НулевыеЗначения_НеРисуютСтолбиков() => Sta.Run(() =>
    {
        Assert.Empty(BarsOf(CreateElement(Snapshot([0f, 0f]))));
    });

    [Fact]
    public void ЦентральнаяЛиния_ЗаливаетЗазорМеждуПоловинами() => Sta.Run(() =>
    {
        var drawings = DrawingsOf(CreateElement(Snapshot([0.5f])));

        // Фон, два столбика и центральная линия - она рисуется последней.
        Assert.Equal(4, drawings.Length);

        var centerLine = drawings[^1];
        Assert.IsType<RectangleGeometry>(centerLine.Geometry);
        Assert.Equal(4d, centerLine.Geometry.Bounds.Height, 5);
        Assert.Equal(FrameHeight / 2d, centerLine.Geometry.Bounds.Top + 2d, 5);
    });

    [Fact]
    public void ЧислоСтолбиков_ОпределяетсяНастройками() => Sta.Run(() =>
    {
        // (800 - 40) + 4 = 764, шаг 24, значит 31 столбик с каждой стороны.
        // Значений заведомо больше: лишние не должны ни рисоваться, ни ломать кадр.
        var element = CreateElement(Snapshot(Enumerable.Repeat(1f, 40).ToArray()));

        Assert.Equal(31, BarsOf(element).Length / 2);
    });

    [Fact]
    public void ЗначенийМеньшеСтолбиков_НеЛомаютКадр() => Sta.Run(() =>
    {
        var element = CreateElement(Snapshot([1f]));

        Assert.Equal(2, BarsOf(element).Length);
    });

    [Fact]
    public void ГраницаСтолбика_ЗадаётсяПером() => Sta.Run(() =>
    {
        var colors = new ColorSettings { BarBorder = new RgbaColor(255, 255, 255) };
        var bar = BarsOf(CreateElement(Snapshot([1f], bars => bars with { BorderWidth = 2d }, colors)))[0];

        Assert.NotNull(bar.Pen);
        Assert.Equal(2d, bar.Pen!.Thickness, 5);
    });

    [Fact]
    public void ГраницаБезЦвета_НеРисуется() => Sta.Run(() =>
    {
        var snapshot = new SpectrumSnapshot(
            [1f],
            new ColorSettings { BarBorder = RgbaColor.Transparent },
            new BarSettings { Width = 20, BorderWidth = 2d, CornerStyle = CornerStyle.OuterRounded });

        Assert.Null(BarsOf(CreateElement(snapshot))[0].Pen);
    });

    [Fact]
    public void ПустойСнимок_ДаётТолькоФон() => Sta.Run(() => Assert.Single(DrawingsOf(CreateElement(SpectrumSnapshot.Empty))));

    private sealed class TestElement : SpectrumElement
    {
        public void Render(DrawingContext context) => OnRender(context);
    }
}