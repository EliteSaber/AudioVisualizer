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
        ColorSettings? colors = null,
        int framesPerSecond = 60)
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

        return new SpectrumSnapshot(values, colors ?? new ColorSettings(), configureBars is null ? bars : configureBars(bars), framesPerSecond);
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

    /// <summary>
    /// Полосы следа: у них градиент по габаритам самой полосы, а заливка столбиков идёт
    /// градиентом по координатам кадра. На разнице в режиме отображения и держится выбор:
    /// при однотонной заливке полосы иначе и не отличить от столбиков.
    /// </summary>
    private static GeometryDrawing[] TrailsOf(TestElement element)
        => DrawingsOf(element)
            .Where(drawing => drawing.Brush is LinearGradientBrush { MappingMode: BrushMappingMode.RelativeToBoundingBox })
            .ToArray();

    private static TestElement CreateTrailElement()
        => CreateElement(Snapshot([1f], bars => bars with { TrailDecayMs = 500d }));

    /// <summary>Два кадра: высокий столбик, затем вдвое ниже. Возвращает элемент после второго.</summary>
    private static TestElement FallFrame()
    {
        var element = CreateTrailElement();
        DrawingsOf(element);

        element.Snapshot = Snapshot([0.5f], bars => bars with { TrailDecayMs = 500d });

        return element;
    }

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

    [Fact]
    public void Градиент_ЦентрСтолбикаЦветЦентра_АКрайЦветКрая() => Sta.Run(() =>
    {
        var colors = new ColorSettings { BarFill = new RgbaColor(56, 132, 255), BarFillSecondary = new RgbaColor(255, 96, 168) };
        var bars = BarsOf(CreateElement(Snapshot([1f], bars => bars with { FillMode = BarFillMode.VerticalGradient }, colors)));

        Assert.Equal(2, bars.Length);

        // Отступ 20, центр 225, половина зазора 2, высота при единице 203: верхняя половина
        // идёт от верхнего края кадра к центру, нижняя - от центра к нижнему краю.
        var upper = (LinearGradientBrush)bars[0].Brush!;
        var lower = (LinearGradientBrush)bars[1].Brush!;

        Assert.Equal(BrushMappingMode.Absolute, upper.MappingMode);
        Assert.Equal(20d, upper.StartPoint.Y, 5);
        Assert.Equal(223d, upper.EndPoint.Y, 5);
        Assert.Equal(colors.BarFillSecondary, Near(upper.GradientStops[0].Color));
        Assert.Equal(colors.BarFill, Near(upper.GradientStops[^1].Color));

        Assert.Equal(227d, lower.StartPoint.Y, 5);
        Assert.Equal(430d, lower.EndPoint.Y, 5);
        Assert.Equal(colors.BarFill, Near(lower.GradientStops[0].Color));
        Assert.Equal(colors.BarFillSecondary, Near(lower.GradientStops[^1].Color));
    });

    [Fact]
    public void ПолосаСледа_БерётЦветСтолбикаНаТойЖеВысоте() => Sta.Run(() =>
    {
        var colors = new ColorSettings { BarFill = new RgbaColor(56, 132, 255), BarFillSecondary = new RgbaColor(255, 96, 168) };

        var element = CreateElement(Snapshot([1f], bars => bars with { FillMode = BarFillMode.VerticalGradient, TrailDecayMs = 500d }, colors));
        DrawingsOf(element);

        element.Snapshot = Snapshot([0.25f], bars => bars with { FillMode = BarFillMode.VerticalGradient, TrailDecayMs = 500d }, colors);

        // Верхушка столбика на 172.25 при градиенте от 20 до 223, то есть три четверти пути от
        // цвета края к цвету центра: (106, 123, 233). Половину пути брать нельзя: там оба цвета
        // усредняются в один, и переставленные местами цвета дали бы тот же ответ.
        var seam = ((LinearGradientBrush)TrailsOf(element)[0].Brush!).GradientStops[^1].Color;

        Assert.InRange(seam.R, 105, 107);
        Assert.InRange(seam.G, 122, 124);
        Assert.InRange(seam.B, 232, 234);
    });

    [Fact]
    public void СледВыключен_ПолосНеРисуется() => Sta.Run(() =>
    {
        var element = CreateElement(Snapshot([1f]));
        DrawingsOf(element);

        element.Snapshot = Snapshot([0.5f]);

        Assert.Empty(TrailsOf(element));
    });

    [Fact]
    public void Убывание_ОставляетПолосуОтПрежнейВысоты() => Sta.Run(() =>
    {
        var trails = TrailsOf(FallFrame());

        // Половина сверху и половина снизу. Отступ 20, центр 225, половина зазора 2, высота
        // при единице 203: призрак 0.967 - это вершина 26.7 вместо нынешних 121.5.
        Assert.Equal(2, trails.Length);
        Assert.Equal(94.8d, trails[0].Geometry.Bounds.Height, 1);
        Assert.Equal(26.7d, trails[0].Geometry.Bounds.Top, 1);
        Assert.Equal(FrameHeight - 26.7d, trails[1].Geometry.Bounds.Bottom, 1);
        Assert.Equal(trails[0].Geometry.Bounds.Width, trails[1].Geometry.Bounds.Width, 5);
    });

    [Fact]
    public void ПолосаСледа_ГаснетОтПрежнейВершиныКСтолбику() => Sta.Run(() =>
    {
        var colors = new ColorSettings { BarFill = new RgbaColor(10, 20, 30) };
        var element = CreateElement(Snapshot([1f], bars => bars with { TrailDecayMs = 500d }, colors));
        DrawingsOf(element);

        element.Snapshot = Snapshot([0.5f], bars => bars with { TrailDecayMs = 500d }, colors);

        var trails = TrailsOf(element);

        // Сверху след уходит в прозрачность, снизу - в цвет столбика: у нижней половины
        // наоборот, потому что полоса растёт от центра вниз.
        Assert.Equal(0, ((LinearGradientBrush)trails[0].Brush!).GradientStops[0].Color.A);
        Assert.Equal(255, ((LinearGradientBrush)trails[0].Brush!).GradientStops[^1].Color.A);
        Assert.Equal(0, ((LinearGradientBrush)trails[1].Brush!).GradientStops[^1].Color.A);

        // Край у столбика берёт цвет заливки на этой высоте, поэтому стыка не видно.
        Assert.Equal(10, ((LinearGradientBrush)trails[0].Brush!).GradientStops[^1].Color.R);
    });

    [Fact]
    public void ОдинИтотЖеКадрДважды_СледНеСдвигается() => Sta.Run(() =>
    {
        var element = FallFrame();
        var first = TrailsOf(element).Select(drawing => drawing.Geometry.Bounds).ToArray();

        var second = TrailsOf(element).Select(drawing => drawing.Geometry.Bounds).ToArray();

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
    });

    [Fact]
    public void РостСтолбика_СледаНет() => Sta.Run(() =>
    {
        var element = CreateTrailElement();
        DrawingsOf(element);

        element.Snapshot = Snapshot([1f], bars => bars with { TrailDecayMs = 500d });

        Assert.Empty(TrailsOf(element));
    });

    [Fact]
    public void ВключениеСледа_ПервыйКадрБезСледа() => Sta.Run(() =>
    {
        var element = CreateElement(Snapshot([1f]));
        DrawingsOf(element);

        // Выключенный след держится только пока выключен: включать его на ходу - значит
        // получить вспышку призрака по старой высоте.
        element.Snapshot = Snapshot([0.5f], bars => bars with { TrailDecayMs = 500d });

        Assert.Empty(TrailsOf(element));
    });

private sealed class TestElement : SpectrumElement
    {
        public void Render(DrawingContext context) => OnRender(context);
    }

    /// <summary>Цвет WPF в виде настройки: так его можно сравнить с <see cref="ColorSettings"/>.</summary>
    private static RgbaColor Near(Color color) => new(color.R, color.G, color.B, color.A);
}