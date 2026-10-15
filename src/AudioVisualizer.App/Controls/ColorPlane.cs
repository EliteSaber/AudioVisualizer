using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.App.Controls;

/// <summary>
/// Плоскость выбора цвета: тон у неё общий, а что означают остальные координаты — у поля и у
/// полосы своё. Здесь живёт всё, что одинаково: тон, захват мыши и сообщение о выборе.
/// </summary>
/// <remarks>
/// Связь с настройками односторонняя: значение элемента меняет только <see cref="Pick"/>, то
/// есть только мышь. Если бы тон брался из цвета и тут же возвращался в него, круг HSV -&gt;
/// RGB -&gt; HSV со сдвигом на байт дёргал бы столбики на каждом кадре превью.
///
/// Про захват мыши и его обязательный отпуск — в ловушках `PROJECT_CONTEXT.md`: попап со
/// <c>StaysOpen="False"</c> держит мышь сам, и элемент внутри попапа, взявший захват и не
/// отпустивший его, оставляет окно слепым.
/// </remarks>
public abstract class ColorPlane : FrameworkElement
{
    public static readonly DependencyProperty HueProperty = DependencyProperty.Register(
        nameof(Hue),
        typeof(double),
        typeof(ColorPlane),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender)
        {
            // Нормализация тона живёт в HsvColor, а не в копии Math.Clamp: диапазоны палитры
            // и модели цвета не должны разойтись. Делегат вернуть double не может, поэтому
            // приведение оставлено лямбде: так правило о возвращаемом типе не ругается.
            CoerceValueCallback = (sender, value) => new HsvColor((double)value, 0d, 0d).Hue
        });

    protected ColorPlane()
    {
        Cursor = Cursors.Hand;
        Unloaded += OnUnloaded;
    }

    /// <summary>Тон в градусах: 0 — красный, <see cref="HsvColor.HueCircle"/> — снова красный.</summary>
    public double Hue
    {
        get => (double)GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }

    /// <summary>Цвет выбран мышью. К этому моменту свойства элемента уже новые.</summary>
    public event EventHandler? Picked;

    /// <summary>
    /// Идёт ли перетаскивание: мышь захвачена, кнопка ещё не отпущена. Это и есть проверяемый
    /// признак того, что захват симметричен: <see cref="CaptureMode"/> в тестах не виден —
    /// WPF отказывает в захвате без настоящего ввода, — а состояние перетаскивания видно.
    /// </summary>
    public bool IsDragging => _dragging;

    private bool _dragging;

    /// <summary>
    /// Переводит точку в значение и сообщает о выборе. Прямо из программы тоже можно:
    /// цвета полосы тона выбираются её же полем в тестах, и обратная связь оттуда лишняя.
    /// </summary>
    public void Pick(Point position)
    {
        if (ActualWidth <= 0d || ActualHeight <= 0d)
        {
            return;
        }

        Choose(position);

        Picked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Захват нужен, чтобы перетаскивание не обрывалось на краю поля: без него мышь уходит
    /// соседнему элементу попапа и выбор там замирает.
    /// </summary>
    /// <remarks>
    /// Захватывается поддерево, а не сам элемент. <see cref="CaptureMode.Element"/> ограничивает
    /// захват одним элементом, и мышь внутри попапа досталась бы только палитре: ползунки
    /// перестали бы реагировать до отпускания кнопки.
    /// </remarks>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        Mouse.Capture(this, CaptureMode.SubTree);

        Pick(e.GetPosition(this));

        // Флаг ставится последним, после захвата и выбора. Отказ в захвате WPF отправляет мышь
        // обратно сразу же, и начатое перетаскивание тут же честно отменилось бы — а отменять
        // ещё нечего.
        _dragging = true;
    }

    /// <summary>
    /// Отпустить захват обязан сам элемент: WPF этого не делает никогда. Пока мышь осталась
    /// на палитре, любой щелчок в окне приходит сюда и ставит цвет в угол поля, а ползунки
    /// попапа не получают мышь вовсе.
    /// </summary>
    /// <remarks>
    /// Захват попапу возвращать не нужно: <see cref="System.Windows.Controls.Primitives.Popup"/>
    /// делает это сам в обработчике потери захвата — ровно потому, что наш перехват сбросил его
    /// флаг, иначе закрывать панель по щелчку снаружи было бы уже нечем.
    /// </remarks>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        CancelDrag();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_dragging)
        {
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed && IsMouseCaptured)
        {
            Pick(e.GetPosition(this));
        }
        else
        {
            // Кнопку отпустили мимо нашего обработчика: потеря фокуса, чужой захват. Перетаскивание
            // всё равно идёт, а мышь вернуться должна: иначе следующий щелчку в окне опять
            // достался бы палитре.
            CancelDrag();
        }
    }

    /// <summary>
    /// Попап закрылся — поле отсоединилось от дерева, а мышь осталась на нём. Пока захват
    /// висит, окно слепое: любой щелчок уходит в невидимое поле. Отдельный перехват видимости
    /// не нужен: событие приходит ровно тогда, когда попап снимает содержимое с
    /// <c>PopupRoot</c>.
    /// </summary>
    private void OnUnloaded(object sender, RoutedEventArgs args) => CancelDrag();

    /// <summary>
    /// Конец перетаскивания и отпуск мыши в одном месте: все пути выходят сюда, и забыть
    /// отпустить нельзя. Отпускать обязан сам элемент — WPF этого не делает никогда.
    /// </summary>
    private void CancelDrag()
    {
        _dragging = false;

        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    /// <summary>Значение из точки. Считает сама плоскость: она уже знает свои габариты.</summary>
    protected abstract void Choose(Point position);
}