using AudioVisualizer.Core.Export;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Export;

/// <summary>
/// Рендер кадра в буфер ffmpeg. Проверяется то, что иначе видно только в готовом видео:
/// столбики действительно рисуются, а буфер имеет ожидаемый размер.
/// </summary>
/// <remarks>
/// Рендерер создаётся внутри STA-потока: конструктор элемента WPF поднимает инфраструктуру
/// ввода, а она требует однопоточного апартамента. Экспортёр так же поступает.
/// </remarks>
[Collection(WpfUi.Name)]
public class FrameRendererTests
{
    // Отступы по умолчанию (48 px сверху и снизу) съедают небольшой кадр целиком: столбикам
    // не остаётся высоты, и проверять на них бессмысленно.
    private static BarSettings Bars => new() { Width = 8, Spacing = 4, CornerRadius = 0, Margin = 8, CenterGap = 4 };

    [Fact]
    public void Render_БуферРазмераКадраВBgra()
    {
        var length = Sta.Get(() =>
        {
            var renderer = new FrameRenderer(64, 32, new ColorSettings(), Bars);

            return renderer.Render(new float[16]).Length;
        });

        Assert.Equal(64 * 4 * 32, length);
    }

    [Fact]
    public void Render_ТихийКадр_ЗалитЦветомФона()
    {
        var colors = new ColorSettings { Background = new RgbaColor(10, 20, 30, 255) };

        var frame = Sta.Get(() => new FrameRenderer(64, 32, colors, Bars).Render(new float[16]));

        // Фон рисуется первым и на весь кадр: если бы он не рисовался, буфер остался бы
        // чёрным прозрачным, и ffmpeg отдал бы чёрное видео вместо тишины с фоном.
        // Буфер в порядке bgra, как и ждёт -pixel_format bgra: синий, зелёный, красный.
        Assert.Equal(30, frame.Span[0]);
        Assert.Equal(20, frame.Span[1]);
        Assert.Equal(10, frame.Span[2]);
    }

    [Fact]
    public void Render_ГромкийКадр_ОтличаетсяОтТихого()
    {
        var silent = RenderFrame(new float[32]);
        var loud = RenderFrame(Enumerable.Repeat(1f, 32).ToArray());

        Assert.NotEqual(silent, loud);
    }

    [Fact]
    public void Render_ТихийКадр_НеНаследуетПредыдущий()
    {
        // Прозрачный фон не пишет в буфер ни одного пикселя, поэтому след громкого кадра
        // остался бы на месте, если бы цель не очищалась: Render у RenderTargetBitmap
        // очищает её не сам. Сравниваем не с нулями (центральная линия рисуется всегда),
        // а с тихим кадром, собранным начисто.
        var colors = new ColorSettings { Background = RgbaColor.Transparent };
        var loud = Enumerable.Repeat(1f, 32).ToArray();

        var afterLoud = Sta.Get(() =>
        {
            var renderer = new FrameRenderer(320, 240, colors, Bars);
            renderer.Render(loud);

            return renderer.Render(new float[32]).ToArray();
        });

        var fresh = RenderFrame(new float[32], colors);

        Assert.Equal(fresh, afterLoud);
    }

    [Fact]
    public void Render_СоСледом_ТихийКадрНеПустой()
    {
        // Обратная сторона очистки буфера: след рисуется сам, а не остаётся в буфере от
        // предыдущего кадра. Фон и центральная линия прозрачные, поэтому ненулевые байты
        // могут быть только призраком прежней высоты.
        var colors = new ColorSettings { Background = RgbaColor.Transparent, CenterLine = RgbaColor.Transparent };
        var bars = Bars with { TrailDecayMs = 500d };

        var frame = Sta.Get(() =>
        {
            var renderer = new FrameRenderer(320, 240, colors, bars);
            renderer.Render(Enumerable.Repeat(1f, 32).ToArray());

            return renderer.Render(new float[32]).ToArray();
        });

        Assert.Contains(frame, value => value != 0);
    }

    [Fact]
    public void СлишкомМногоЗначений_ЛишниеНеЛомаютКадр()
    {
        var expected = RenderFrame(new float[32]);

        // Столбиков на кадре меньше, чем значений: лишние должны просто игнорироваться,
        // иначе длинный массив из превью сдвинул бы картинку.
        var actual = RenderFrame(new float[200]);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0, 32)]
    [InlineData(-2, 32)]
    [InlineData(64, 0)]
    [InlineData(64, -2)]
    public void НедопустимыйРазмер_Отклоняется(int width, int height)
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => Sta.Get(() => new FrameRenderer(width, height, new ColorSettings(), Bars)));

    private static byte[] RenderFrame(float[] values, ColorSettings? colors = null)
        => Sta.Get(() => new FrameRenderer(320, 240, colors ?? new ColorSettings(), Bars).Render(values).ToArray());
}