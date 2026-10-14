using System.Windows;
using AudioVisualizer.Core.Rendering;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Rendering;

public class BarLayoutTests
{
    private static BarLayout CreateLayout(double width = 1920d, double height = 1080d, BarSettings? bars = null)
        => new(width, height, bars ?? new BarSettings { Width = 8, Spacing = 4, Margin = 48, CenterGap = 4 });

    [Fact]
    public void BarCount_СчитаетсяИзШириныИПромежутка()
    {
        // (1920 - 96) + 4 = 1828, шаг 12, значит 152 столбика с последним промежутком.
        var layout = CreateLayout();

        Assert.Equal(152, layout.BarCount);
    }

    [Fact]
    public void BarCount_УзкийСтолбик_ДалёкОтКраёв()
    {
        var layout = CreateLayout(bars: new BarSettings { Width = 2, Spacing = 2, Margin = 0, CenterGap = 0 });

        // (1920 + 2) / 4 = 480 с половиной, значит 480 столбиков с последним промежутком.
        Assert.Equal(480, layout.BarCount);
    }

    [Fact]
    public void BarCount_КадрУжеОдногоСтолбика_РавенНулю()
    {
        var layout = CreateLayout(width: 20d, bars: new BarSettings { Width = 8, Spacing = 4, Margin = 48 });

        Assert.Equal(0, layout.BarCount);
    }

    [Fact]
    public void Столбики_Центрируются_ПоШиринеКадра()
    {
        var layout = CreateLayout();

        Assert.Equal((1920d - layout.ContentWidth) / 2d, layout.ContentLeft, 5);
        Assert.True(layout.ContentLeft >= 48d);
        Assert.True(layout.ContentLeft + layout.ContentWidth <= 1920d - 48d);
    }

    [Fact]
    public void CenterY_ЭтоПоловинаВысоты()
    {
        var layout = CreateLayout(height: 1080d);

        Assert.Equal(540d, layout.CenterY, 5);
    }

    [Fact]
    public void MaxHalfHeight_ОстанавливаетсяНаОтступеОтКрая()
    {
        // Центр 540, половина зазора 2, отступ 48: самый высокий столбик идёт от 540-2-490.
        var layout = CreateLayout();

        Assert.Equal(490d, layout.MaxHalfHeight, 5);
    }

    [Fact]
    public void ВерхнийСтолбик_ОтходитОтЦентраНаПоловинуЗазора()
    {
        var layout = CreateLayout();
        var bar = layout.GetUpperBar(0, 1d);

        Assert.Equal(layout.CenterY - 2d, bar.Bottom, 5);
        Assert.Equal(48d, bar.Top, 5);
    }

    [Fact]
    public void НижнийСтолбик_ЗеркаленВерхнему()
    {
        var layout = CreateLayout();
        var upper = layout.GetUpperBar(10, 0.6d);
        var lower = layout.GetLowerBar(10, 0.6d);

        Assert.Equal(upper.Height, lower.Height, 5);
        Assert.Equal(upper.X, lower.X, 5);
        Assert.Equal(layout.CenterY + 2d, lower.Top, 5);
        Assert.Equal(upper.Top + lower.Bottom, layout.CenterY * 2d, 5);
    }

    [Fact]
    public void НулевоеЗначение_ДаётПустойПрямоугольник()
    {
        var layout = CreateLayout();
        var bar = layout.GetUpperBar(0, 0d);

        Assert.Equal(0d, bar.Height, 5);
    }

    [Fact]
    public void ЗначениеБольшеЕдиницы_Обрезается()
    {
        var layout = CreateLayout();

        Assert.Equal(layout.MaxHalfHeight, layout.GetUpperBar(0, 5d).Height, 5);
        Assert.Equal(0d, layout.GetUpperBar(0, -1d).Height, 5);
    }

    [Fact]
    public void ЦентральнаяЛиния_ЗаливаетЗазорМеждуПоловинами()
    {
        var layout = CreateLayout();
        var line = layout.GetCenterLine();

        Assert.Equal(4d, line.Height, 5);
        Assert.Equal(layout.CenterY, line.Top + (line.Height / 2d), 5);
        Assert.Equal(layout.ContentWidth, line.Width, 5);
    }

    [Fact]
    public void Столбики_НеПерекрываются()
    {
        var layout = CreateLayout(bars: new BarSettings { Width = 10, Spacing = 2, Margin = 0 });

        for (var index = 1; index < layout.BarCount; index++)
        {
            Assert.True(layout.BarLeft(index) - (layout.BarLeft(index - 1) + 10d) >= 2d);
        }
    }

    [Fact]
    public void НомерСтолбикаВнеДиапазона_Отклоняется()
    {
        var layout = CreateLayout();

        Assert.Throws<ArgumentOutOfRangeException>(() => layout.GetUpperBar(-1, 0.5d));
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.GetUpperBar(layout.BarCount, 0.5d));
    }

    [Fact]
    public void ОтрицательныеРазмерыКадра_Отклоняются()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BarLayout(-1d, 1080d, new BarSettings()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BarLayout(1920d, -1d, new BarSettings()));
    }

    [Fact]
    public void КрошечныйКадр_НеДаётОтрицательнойВысоты()
    {
        var layout = new BarLayout(100d, 10d, new BarSettings { Width = 4, Spacing = 1, Margin = 8, CenterGap = 4 });

        Assert.Equal(0d, layout.MaxHalfHeight);
    }

    [Fact]
    public void Mirror_Выключен_ОтключаетНижнююПоловину()
        => Assert.False(CreateLayout(bars: new BarSettings { Mirror = false }).IsMirrored);
}