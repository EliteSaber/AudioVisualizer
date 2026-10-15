using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using AudioVisualizer.App.Controls;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Controls;

/// <summary>
/// Поле выбора цвета: перевод точки в насыщенность с яркостью и содержимое рисунка.
/// </summary>
/// <remarks>
/// Рисунок проверяется записью команд рисования в <see cref="DrawingVisual"/>, а не
/// изображением: так же проверяется <c>SpectrumElement</c>. Проверять нужно форму, а не
/// пиксели — в них тот же тон, что и в заливке.
/// </remarks>
[Collection(WpfUi.Name)]
public class ColorFieldTests
{
    private const double Width = 200d;
    private const double Height = 120d;

    [Fact]
    public void Поле_СоздаётсяИИзмеряется() => Sta.Run(() =>
    {
        var field = Create();

        Assert.Equal(Width, field.ActualWidth, 3);
        Assert.Equal(Height, field.ActualHeight, 3);
    });

    [Fact]
    public void ЦентрПоля_ДаётПоловинуНасыщенностиИЯркости() => Sta.Run(() =>
    {
        var field = Create();

        field.Pick(new Point(Width / 2d, Height / 2d));

        Assert.Equal(0.5d, field.Saturation, 3);
        Assert.Equal(0.5d, field.Value, 3);
    });

    [Fact]
    public void УглыПоля_ДаютКрайниеЗначения() => Sta.Run(() =>
    {
        // Верхний левый угол — серый, нижний правый — чистый тон, и наоборот по диагонали.
        var field = Create();

        field.Pick(new Point(0d, 0d));

        Assert.Equal(0d, field.Saturation, 3);
        Assert.Equal(1d, field.Value, 3);

        field.Pick(new Point(Width, Height));

        Assert.Equal(1d, field.Saturation, 3);
        Assert.Equal(0d, field.Value, 3);
    });

    [Fact]
    public void ТочкаЗаПределамиПоля_Зажимается() => Sta.Run(() =>
    {
        // Мышь доводится до края, а точка из GetPosition может оказаться и за ним.
        var field = Create();

        field.Pick(new Point(-40d, Height + 40d));

        Assert.Equal(0d, field.Saturation, 3);
        Assert.Equal(0d, field.Value, 3);

        field.Pick(new Point(Width * 2d, -Height));

        Assert.Equal(1d, field.Saturation, 3);
        Assert.Equal(1d, field.Value, 3);
    });

    [Fact]
    public void НевыбранноеПоле_ТочкуНеПринимает() => Sta.Run(() =>
    {
        // Поле живёт в попапе и измеряется позже: без габаритов переводить точку не во что.
        var field = new ColorField();

        field.Pick(new Point(10d, 10d));

        Assert.Equal(0d, field.Saturation, 3);
        Assert.Equal(0d, field.Value, 3);
    });

    [Fact]
    public void СвойстваВнеДиапазона_Зажимаются() => Sta.Run(() =>
    {
        var field = Create();

        field.Saturation = 3d;
        field.Value = -2d;

        Assert.Equal(1d, field.Saturation, 3);
        Assert.Equal(0d, field.Value, 3);
    });

    [Fact]
    public void Тон_ЗадаётСодержимоеПоля() => Sta.Run(() =>
    {
        // По содержимому поля и выбирают тон: оно обязано совпадать с выбранным цветом.
        var field = Create();

        field.Hue = 120d;

        var pure = Assert.IsType<SolidColorBrush>(DrawingsOf(field)[0].Brush);

        Assert.Equal(Colors.Lime, pure.Color);
    });

    [Fact]
    public void Поле_РисуетТонДваНаложенияИОтметку() => Sta.Run(() =>
    {
        // Перебор пикселей здесь означал бы тысячи прямоугольников на каждое движение мыши,
        // поэтому рисунок состоит ровно из трёх заливок и отметки из двух контуров.
        var drawings = DrawingsOf(Create());

        Assert.Equal(5, drawings.Length);
        Assert.Equal(3, drawings.Count(drawing => drawing.Brush is not null));
        Assert.Equal(2, drawings.Count(drawing => drawing.Brush is LinearGradientBrush));
        Assert.Equal(2, drawings.Count(drawing => drawing.Geometry is EllipseGeometry));
    });

    [Fact]
    public void Отметка_СтоитВТочкеЦвета() => Sta.Run(() =>
    {
        var field = Create();

        field.Saturation = 0.25d;
        field.Value = 0.75d;

        var marker = MarkerOf(field);

        Assert.Equal(Width * 0.25d, marker.Center.X, 3);
        Assert.Equal(Height * 0.25d, marker.Center.Y, 3);
    });

    [Fact]
    public void ОтметкаНеВылезает_ЗаКраемПоля() => Sta.Run(() =>
    {
        var field = Create();

        field.Saturation = 0d;
        field.Value = 1d;

        var marker = MarkerOf(field);

        Assert.True(marker.Center.X - marker.RadiusX >= 0d, $"Отметка уехала за поле: {marker.Bounds}.");
        Assert.True(marker.Center.Y - marker.RadiusY >= 0d, $"Отметка уехала за поле: {marker.Bounds}.");
    });

    [Fact]
    public void Выбор_СообщаетОПравке() => Sta.Run(() =>
    {
        var field = Create();
        var picks = 0;

        field.Picked += (_, _) => picks++;
        field.Pick(new Point(Width / 2d, Height / 2d));

        Assert.Equal(1, picks);
    });

/// <summary>
    /// Регрессия: поле брало мышь и не отпускало её. Захват не отпускается сам никогда, и
    /// пока он висел на палитре, любой щелчок в окне приходил в неё и ставил цвет в угол, а
    /// ползунки попапа не получали мышь вовсе.
    /// </summary>
    /// <remarks>
    /// Проверяется состояние перетаскивания, а не сам захват: WPF отказывает в захвате мыши без
    /// настоящего ввода, и в тестах <c>Mouse.Captured</c> всегда остаётся пустым. Режим
    /// <c>CaptureMode.SubTree</c> тоже не виден тестам, поэтому он держится на комментарии в
    /// <c>ColorPlane</c>: с <c>Element</c> мышь внутри попапа досталась бы только палитре.
    /// </remarks>
    /// <summary>
    /// Регрессия: поле брало мышь и не отпускало её. Захват не отпускается сам никогда, и
    /// пока он висел на палитре, любой щелчок в окне приходил в неё и ставил цвет в угол, а
    /// ползунки попапа не получали мышь вовсе.
    /// </summary>
/// <remarks>
/// Проверяется состояние перетаскивания, а не сам захват: WPF отказывает в захвате мыши без
/// настоящего ввода, поэтому <c>Mouse.Captured</c> в тестах всегда пуст, даже под живым
/// окном. По той же причине не проверяется и <c>CaptureMode.SubTree</c> — он держится на
/// комментарии в <c>ColorPlane</c>: с <c>Element</c> мышь внутри попапа досталась бы только
/// палитре, и ползунки перестали бы реагировать до отпускания кнопки.
/// </remarks>
[Fact]
    public void Нажатие_НачинаетПеретаскивание() => Sta.Run(() =>
    {
        var field = Create();
        using var window = new Host(field).Show();

        field.Press();

        Assert.True(field.IsDragging);
    });

    [Fact]
    public void ОтпусканиеКнопки_КонецПеретаскивания() => Sta.Run(() =>
    {
        var field = Create();
        using var window = new Host(field).Show();

        field.Press();
        field.Release();

        Assert.False(field.IsDragging);
    });

    [Fact]
    public void ОтсоединениеПоля_КонецПеретаскивания() => Sta.Run(() =>
    {
        // Попап закрылся, а мышь осталась на невидимом поле: окно продолжало бы слать ему
        // щелчки. Событие приходит, когда попап снимает содержимое с PopupRoot.
        var field = Create();
        using var window = new Host(field).Show();

        field.Press();
        field.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent, field));

        Assert.False(field.IsDragging);
    });

    /// <summary>
    /// Элементы под настоящим окном. Захват мыши берётся только у элемента, у которого есть
    /// источник представления, и WPF отказывает в нём без живого ввода: окно сообщений
    /// недостаточно. Окно без рамки и за краем экрана — на экране его не видно.
    /// </summary>
    private sealed class Host(params FrameworkElement[] elements) : IDisposable
    {
        private readonly Window _window = new()
        {
            Content = Stack(elements),
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Left = -32000,
            Top = -32000,
            Width = 1,
            Height = 1
        };

        public Host Show()
        {
            _window.Show();

            return this;
        }

        public void Dispose() => _window.Close();

        private static Grid Stack(IEnumerable<FrameworkElement> elements)
        {
            var root = new Grid();

            foreach (var element in elements)
            {
                root.Children.Add(element);
            }

            return root;
        }
    }

    /// <summary>Поле размечено своими габаритами: в попапе оно измеряется контейнером.</summary>
    private static TestField Create()
    {
        var field = new TestField { Width = Width, Height = Height };
        field.Measure(new Size(Width, Height));
        field.Arrange(new Rect(0d, 0d, Width, Height));

        return field;
    }

    /// <summary>
    /// Отметка: у неё два контура по одной геометрии, поэтому берётся первая. Положение
    /// проверяется по <see cref="EllipseGeometry.Center"/>, а не по Bounds: границы эллипса WPF
    /// считает по своей аппроксимации и выдаёт заведомо больший прямоугольник.
    /// </summary>
    private static EllipseGeometry MarkerOf(TestField field)
        => (EllipseGeometry)DrawingsOf(field).First(drawing => drawing.Geometry is EllipseGeometry).Geometry!;

    private static GeometryDrawing[] DrawingsOf(TestField field)
    {
        var visual = new DrawingVisual();

        using (var context = visual.RenderOpen())
        {
            field.Render(context);
        }

        return [.. visual.Drawing?.Children.OfType<GeometryDrawing>() ?? []];
    }

    /// <summary>
    /// Рисунок элемента доступен извне только через <c>OnRender</c>: метод защищённый, поэтому
    /// тест берёт его подклассом — так же поступает проверка <c>SpectrumElement</c>.
    ///
    /// Там же и нажатие: поднимать <c>MouseLeftButtonDownEvent</c> вручную бесполезно —
    /// виртуальные обработчики элемента она не зовёт, а <c>GetPosition</c> без источника
    /// представления вовсе бросает.
    /// </summary>
    private sealed class TestField : ColorField
    {
        public void Render(DrawingContext context) => OnRender(context);

        public void Press() => OnMouseLeftButtonDown(Button(UIElement.MouseLeftButtonDownEvent));

        public void Release() => OnMouseLeftButtonUp(Button(UIElement.MouseLeftButtonUpEvent));

        // Source ставится после RoutedEvent: иначе RoutedEventArgs бросает на пустое значение.
        private MouseButtonEventArgs Button(RoutedEvent routedEvent)
        {
            var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = routedEvent };

            args.Source = this;

            return args;
        }
    }
}