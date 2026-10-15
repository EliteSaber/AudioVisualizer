using System.Windows;
using System.Windows.Media;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.App.Controls;

/// <summary>
/// Полоса тона: слева направо полный круг, 0 — красный.
/// </summary>
/// <remarks>
/// Цвета полосы берёт <see cref="HsvColor.HueRamp"/> через <see cref="PaletteBrushes"/>, а не
/// список в разметке: и градиент, и проверки круга тонов должны считаться по одному правилу.
///
/// Класс не запечатан по той же причине, что и <see cref="ColorField"/>: рисунок проверяется
/// вызовом <c>OnRender</c> из тестов.
/// </remarks>
public class HueStrip : ColorPlane
{
    private const double MarkerThickness = 3d;

    private readonly Brush _ramp;
    private readonly Pen _markerShadow;
    private readonly Pen _markerLight;

    public HueStrip()
    {
        _ramp = PaletteBrushes.HueRamp();
        _markerShadow = PaletteBrushes.Marker(Colors.Black, MarkerThickness);
        _markerLight = PaletteBrushes.Marker(Colors.White, MarkerThickness / 3d);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);

        var frame = new Rect(0d, 0d, ActualWidth, ActualHeight);

        if (frame.IsEmpty)
        {
            return;
        }

        drawingContext.DrawRectangle(_ramp, null, frame);
        DrawMarker(drawingContext, frame);
    }

    /// <summary>
    /// Точки за краями дают крайние тона: круг замкнут, и точка правее полосы — это начало,
    /// а не «ничего».
    /// </summary>
    protected override void Choose(Point position)
    {
        var hue = position.X / ActualWidth * HsvColor.HueCircle;

        SetCurrentValue(HueProperty, hue);
    }

    /// <summary>Индикатор тона: чёрная линия под белой, иначе его не видно на светлом краю.</summary>
    /// <remarks>
    /// У правого края индикатор уезжает на половину толщины за полосу: 359° и 1° — это почти
    /// один и тот же тон, и держать его внутри полосы можно было бы только округлением.
    /// </remarks>
    private void DrawMarker(DrawingContext drawingContext, Rect frame)
    {
        var x = Hue / HsvColor.HueCircle * frame.Width;
        var shadow = new Rect(x - (MarkerThickness / 2d), 0d, MarkerThickness, frame.Height);
        var light = new Rect(x - (MarkerThickness / 6d), 0d, MarkerThickness / 3d, frame.Height);

        drawingContext.DrawRectangle(null, _markerShadow, shadow);
        drawingContext.DrawRectangle(null, _markerLight, light);
    }
}