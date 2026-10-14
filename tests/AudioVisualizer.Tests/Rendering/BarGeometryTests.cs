using System.Windows;
using AudioVisualizer.Core.Rendering;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Rendering;

public class BarGeometryTests
{
    [Fact]
    public void ПрямоугольныеУглы_ЗаполняютВесьПрямоугольник()
    {
        var geometry = BarGeometry.Build(new Rect(10d, 20d, 100d, 200d), 20d, CornerStyle.Rectangular, outerCornersAtTop: true);

        Assert.True(geometry.FillContains(new Point(15d, 25d)));
        Assert.True(geometry.FillContains(new Point(105d, 215d)));
        Assert.False(geometry.FillContains(new Point(9d, 25d)));
    }

    [Fact]
    public void СкруглениеУгловУЦентра_ОставляетУглыПрямыми()
    {
        var geometry = BarGeometry.Build(new Rect(0d, 0d, 100d, 200d), 20d, CornerStyle.OuterRounded, outerCornersAtTop: true);

        // Внешний угол срезан дугой с центром в (20, 20): точка (5, 5) вне её, (10, 10) внутри.
        Assert.False(geometry.FillContains(new Point(5d, 5d)));
        Assert.False(geometry.FillContains(new Point(95d, 5d)));

        // Углы у центральной линии прямые, там заполнено всё.
        Assert.True(geometry.FillContains(new Point(5d, 195d)));
        Assert.True(geometry.FillContains(new Point(95d, 195d)));
    }

    [Fact]
    public void СкруглениеСнизу_ЗеркалитПоведение()
    {
        var geometry = BarGeometry.Build(new Rect(0d, 0d, 100d, 200d), 20d, CornerStyle.OuterRounded, outerCornersAtTop: false);

        Assert.True(geometry.FillContains(new Point(5d, 5d)));
        Assert.False(geometry.FillContains(new Point(5d, 195d)));
        Assert.False(geometry.FillContains(new Point(95d, 195d)));
    }

    [Fact]
    public void СкруглениеВсехУглов_УбираетОбеПары()
    {
        var geometry = BarGeometry.Build(new Rect(0d, 0d, 100d, 200d), 20d, CornerStyle.Rounded, outerCornersAtTop: true);

        Assert.False(geometry.FillContains(new Point(5d, 5d)));
        Assert.False(geometry.FillContains(new Point(5d, 195d)));
    }

    [Fact]
    public void ПустойПрямоугольник_ДаётПустуюГеометрию()
        => Assert.True(BarGeometry.Build(Rect.Empty, 10d, CornerStyle.OuterRounded, outerCornersAtTop: true).IsEmpty());

    [Fact]
    public void РадиусБольшеПоловиныСтороны_НеЛомаетГеометрию()
    {
        var geometry = BarGeometry.Build(new Rect(0d, 0d, 20d, 20d), 500d, CornerStyle.Rounded, outerCornersAtTop: true);

        Assert.True(geometry.FillContains(new Point(10d, 10d)));
        Assert.False(geometry.FillContains(new Point(1d, 1d)));
    }

    [Fact]
    public void Геометрия_Заморожена()
    {
        var geometry = BarGeometry.Build(new Rect(0d, 0d, 20d, 20d), 4d, CornerStyle.OuterRounded, outerCornersAtTop: true);

        Assert.True(geometry.IsFrozen);
    }
}