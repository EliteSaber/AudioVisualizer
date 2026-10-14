using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AudioVisualizer.Core.Rendering;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Core.Export;

/// <summary>
/// Рисует кадры видео в буфер BGRA, готовый к отправке в ffmpeg.
/// </summary>
/// <remarks>
/// Создаётся один раз на выгрузку и переиспользуется: на кадре 1920x1080 это 8 МБ копирования,
/// которые иначе выбрасывались бы в мусор. Снимок и буфер поэтому живут дольше одного кадра.
/// И создание, и <see cref="Render"/> должны идти на STA-треде: уже конструктор
/// <see cref="FrameworkElement"/> поднимает инфраструктуру ввода WPF, которая требует STA.
/// </remarks>
public sealed class FrameRenderer
{
    private readonly SpectrumElement _element;
    private readonly RenderTargetBitmap _bitmap;
    private readonly byte[] _buffer;
    private readonly int _stride;
    private readonly ColorSettings _colors;
    private readonly BarSettings _bars;

    public FrameRenderer(int width, int height, ColorSettings colors, BarSettings bars)
    {
        ArgumentNullException.ThrowIfNull(colors);
        ArgumentNullException.ThrowIfNull(bars);

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Ширина кадра должна быть положительной.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Высота кадра должна быть положительной.");
        }

        _colors = colors;
        _bars = bars;

        _element = new SpectrumElement { Width = width, Height = height };
        _element.Measure(new Size(width, height));
        _element.Arrange(new Rect(0d, 0d, width, height));

        // Pbgra32 - это ровно тот порядок байт, который ждёт -pixel_format bgra.
        _bitmap = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);
        _stride = width * 4;
        _buffer = new byte[_stride * height];
    }

    /// <summary>
    /// Рисует кадр по высотам столбиков и возвращает буфер BGRA. Буфер общий для всех кадров:
    /// его нужно передать потребителю до следующего вызова.
    /// </summary>
    public ReadOnlyMemory<byte> Render(float[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        _element.Snapshot = new SpectrumSnapshot(values, _colors, _bars);

        // Элемент отсоединён от дерева, поэтому присваивание свойства не запускает разметку
        // само по себе. Без UpdateLayout отсоединённый FrameworkElement отдаёт пустой кадр:
        // OnRender не вызывается, и в ffmpeg уходит одноцветная картинка.
        _element.UpdateLayout();

        _bitmap.Render(_element);
        _bitmap.CopyPixels(_buffer, _stride, 0);

        return _buffer;
    }
}