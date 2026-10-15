using System.Windows;
using System.Windows.Media;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.App.Controls;

/// <summary>
/// Поле выбора цвета: насыщенность по горизонтали, яркость по вертикали, тон у него общий с
/// полосой тона.
/// </summary>
/// <remarks>
/// Поле рисуется тремя заливками, а не перебором пикселей: прямоугольник чистого тона, поверх
/// него белый слева направо и чёрный сверху вниз. Их сумма даёт ровно HSV, а поле 236x120
/// точек рисовалось бы двадцатью тысячами прямоугольников на каждое движение мыши.
///
/// Класс не запечатан: тесты рисуют его вызовом <c>OnRender</c> в отдельный
/// <c>DrawingVisual</c>, так же как <c>SpectrumElement</c>.
/// </remarks>
public class ColorField : ColorPlane
{
    private const double MarkerRadius = 6d;

    private readonly Brush _saturationWash;
    private readonly Brush _valueShade;
    private readonly Pen _markerShadow;
    private readonly Pen _markerLight;

    public static readonly DependencyProperty SaturationProperty = DependencyProperty.Register(
        nameof(Saturation),
        typeof(double),
        typeof(ColorField),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender)
        {
            CoerceValueCallback = (sender, value) => new HsvColor(0d, (double)value, 0d).Saturation
        });

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(ColorField),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender)
        {
            CoerceValueCallback = (sender, value) => new HsvColor(0d, 0d, (double)value).Value
        });

    public ColorField()
    {
        // Наложения не зависят от тона: иначе на каждом движении мыши создавались бы заново.
        _saturationWash = PaletteBrushes.Fade(Colors.White, Color.FromArgb(0, 255, 255, 255), new Point(1d, 0d));
        _valueShade = PaletteBrushes.Fade(Color.FromArgb(0, 0, 0, 0), Colors.Black, new Point(0d, 1d));
        _markerShadow = PaletteBrushes.Marker(Colors.Black, 3d);
        _markerLight = PaletteBrushes.Marker(Colors.White, 1.5d);
    }

    /// <summary>Насыщенность: 0 слева, 1 справа.</summary>
    public double Saturation
    {
        get => (double)GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    /// <summary>Яркость: 1 вверху, 0 внизу.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);

        var frame = new Rect(0d, 0d, ActualWidth, ActualHeight);

        if (frame.IsEmpty)
        {
            return;
        }

        drawingContext.DrawRectangle(PaletteBrushes.Solid(new HsvColor(Hue, 1d, 1d).ToRgba(255)), null, frame);
        drawingContext.DrawRectangle(_saturationWash, null, frame);
        drawingContext.DrawRectangle(_valueShade, null, frame);

        DrawMarker(drawingContext, frame);
    }

    /// <summary>
    /// Точки за краями поля дают крайние значения: иначе цвет собирался бы из значения,
    /// которого на поле нет.
    /// </summary>
    protected override void Choose(Point position)
    {
        SetCurrentValue(SaturationProperty, Math.Clamp(position.X / ActualWidth, 0d, 1d));
        SetCurrentValue(ValueProperty, Math.Clamp(1d - (position.Y / ActualHeight), 0d, 1d));
    }

    /// <summary>Отметка текущего цвета: чёрный контур под белым, иначе её не видно ни на чём.</summary>
    private void DrawMarker(DrawingContext drawingContext, Rect frame)
    {
        var diameter = MarkerRadius * 2d;
        var center = new Point(Saturation * frame.Width, (1d - Value) * frame.Height);
        var bounds = new Rect(
            Math.Clamp(center.X - MarkerRadius, 0d, Math.Max(0d, frame.Width - diameter)),
            Math.Clamp(center.Y - MarkerRadius, 0d, Math.Max(0d, frame.Height - diameter)),
            diameter,
            diameter);

        var marker = new EllipseGeometry(bounds);

        drawingContext.DrawGeometry(null, _markerShadow, marker);
        drawingContext.DrawGeometry(null, _markerLight, marker);
    }
}