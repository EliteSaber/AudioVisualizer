using System.Windows;
using AudioVisualizer.App.Controls;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Controls;

[Collection(WpfUi.Name)]
public class ColorPickerTests
{
    // Регрессия: значение по умолчанию у свойства зависимостей передавалось как default,
    // то есть null, и регистрация падала ещё до создания элемента.
    [Fact]
    public void Создание_НеПадает() => Sta.Run(() =>
    {
        var picker = new ColorPicker();

        Assert.Equal(default, picker.Color);
    });

    [Fact]
    public void ЗаданныйЦвет_ВозвращаетсяОбратно() => Sta.Run(() =>
    {
        var picker = new ColorPicker { Color = new RgbaColor(0x12, 0x34, 0x56, 0x78) };

        Assert.Equal(new RgbaColor(0x12, 0x34, 0x56, 0x78), picker.Color);
    });

    [Fact]
    public void Разметка_ЗагружаетсяИИзмеряется() => Sta.Run(() =>
    {
        var picker = new ColorPicker();

        picker.Measure(new Size(300d, 200d));
        picker.Arrange(new Rect(0d, 0d, 300d, 200d));

        Assert.Equal(300d, picker.ActualWidth, 3);
        Assert.True(picker.ActualHeight > 0d);
    });

    [Fact]
    public void ПрозрачныйЦвет_Принимается() => Sta.Run(() =>
    {
        var picker = new ColorPicker { Color = RgbaColor.Transparent };

        Assert.Equal(0, picker.Color.A);
    });
}