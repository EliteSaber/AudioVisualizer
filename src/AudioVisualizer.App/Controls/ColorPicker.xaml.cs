using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.App.Controls;

/// <summary>
/// Выбор цвета: образец, значение в hex и всплывающая панель — палитра HSV с ползунками
/// каналов. Своя реализация вместо диалога WinForms, чтобы приложение осталось чистым WPF.
/// </summary>
public partial class ColorPicker : UserControl
{
    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color),
        typeof(RgbaColor),
        typeof(ColorPicker),
        new FrameworkPropertyMetadata(default(RgbaColor), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorChanged));

    private bool _updating;

    public ColorPicker()
    {
        InitializeComponent();

        ColorPopup.PlacementTarget = Swatch;
        ColorPopup.Opened += OnPopupOpened;
        HueField.Picked += OnFieldPicked;
        HueBar.Picked += OnStripPicked;
        ShowColor(Color);
    }

    public RgbaColor Color
    {
        get => (RgbaColor)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    private static void OnColorChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => ((ColorPicker)sender).ShowColor((RgbaColor)args.NewValue);

    private void ShowColor(RgbaColor color)
    {
        // Каналы и текст обновляются под защитой: правка ползунка меняет Color,
        // а та возвращается сюда же.
        _updating = true;
        try
        {
            Swatch.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
            HexLabel.Text = color.ToHex();
            HexInput.Text = color.ToHex();

            RedSlider.Value = color.R;
            GreenSlider.Value = color.G;
            BlueSlider.Value = color.B;
            AlphaSlider.Value = color.A;
            TransparentCheckBox.IsChecked = color.A == 0;

            // Палитра показывает текущий цвет, но выбора не сообщает: поднятое событие вернуло
            // бы тот же цвет обратно и сдвинуло его на байт при каждой правке.
            var hsv = HsvColor.FromRgba(color);

            HueField.Hue = hsv.Hue;
            HueField.Saturation = hsv.Saturation;
            HueField.Value = hsv.Value;
            HueBar.Hue = hsv.Hue;
        }
        finally
        {
            _updating = false;
        }
    }

    private void OnFieldPicked(object? sender, EventArgs args) => ApplyPickedColor();

    private void OnStripPicked(object? sender, EventArgs args)
    {
        // Тон у поля и у полосы один: выбрали тон — поле должно перерисоваться под него,
        // не теряя ни насыщенность, ни яркость.
        HueField.Hue = HueBar.Hue;

        ApplyPickedColor();
    }

    /// <summary>
    /// Цвет с палитры собирается из текущего тона, насыщенности и яркости. Альфа берётся из
    /// прежнего цвета: у полупрозрачной центральной линии и прозрачной границы столбика она
    /// своя, и палитра её бы стёрла.
    /// </summary>
    private void ApplyPickedColor()
    {
        Color = new HsvColor(HueField.Hue, HueField.Saturation, HueField.Value).ToRgba(Color.A);
    }

    private void OnChannelChanged(object sender, RoutedPropertyChangedEventArgs<double> args)
    {
        if (_updating)
        {
            return;
        }

        Color = new RgbaColor(
            (byte)RedSlider.Value,
            (byte)GreenSlider.Value,
            (byte)BlueSlider.Value,
            (byte)AlphaSlider.Value);
    }

    private void OnTransparentChecked(object sender, RoutedEventArgs args)
    {
        if (_updating || TransparentCheckBox.IsChecked != true)
        {
            return;
        }

        _updating = true;
        try
        {
            AlphaSlider.Value = 0;
        }
        finally
        {
            _updating = false;
        }

        Color = new RgbaColor((byte)RedSlider.Value, (byte)GreenSlider.Value, (byte)BlueSlider.Value, 0);
    }

    private void OnHexKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key == Key.Enter)
        {
            ApplyHex();
        }
    }

    private void OnHexLostFocus(object sender, RoutedEventArgs args) => ApplyHex();

    private void OnSwatchClick(object sender, MouseButtonEventArgs args) => ColorPopup.IsOpen = !ColorPopup.IsOpen;

    // Фокус переносим внутрь панели: пока он снаружи, Esc уходит элементу, на котором и был,
    // и до обработчика не доходит.
    private void OnPopupOpened(object? sender, EventArgs args) => HexInput.Focus();

    // Попап открывается по отпусканию кнопки мыши, а не по нажатию: открытый на нажатии,
    // он ловит отпускание как клик снаружи и тут же закрывается.
    private void OnPreviewKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key == Key.Escape && ColorPopup.IsOpen)
        {
            ColorPopup.IsOpen = false;
            args.Handled = true;
        }
    }

    private void ApplyHex()
    {
        if (RgbaColor.TryParse(HexInput.Text, out var parsed))
        {
            Color = parsed;
            return;
        }

        // Незаданный текст возвращаем к текущему цвету: молча оставлять опечатку нельзя.
        ShowColor(Color);
    }
}