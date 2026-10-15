using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

    [Fact]
    public void Палитра_ПоказываетТекущийЦвет() => Sta.Run(() =>
    {
        var color = new RgbaColor(0x38, 0x84, 0xFF);
        var picker = new ColorPicker { Color = color };
        var field = Field(picker);
        var hsv = HsvColor.FromRgba(color);

        Assert.Equal(hsv.Hue, field.Hue, 3);
        Assert.Equal(hsv.Saturation, field.Saturation, 3);
        Assert.Equal(hsv.Value, field.Value, 3);
        Assert.Equal(hsv.Hue, Strip(picker).Hue, 3);
    });

    [Fact]
    public void ВыборВПоле_МеняетЦвет() => Sta.Run(() =>
    {
        var picker = new ColorPicker { Color = new RgbaColor(0x38, 0x84, 0xFF) };
        var field = Field(picker);

        field.Hue = 120d;
        field.Pick(new Point(FieldWidth, 0d));

        Assert.Equal(new RgbaColor(0x00, 0xFF, 0x00), picker.Color);
    });

    [Fact]
    public void ВыборВПоле_СохраняетАльфу() => Sta.Run(() =>
    {
        // У полупрозрачной центральной линии альфа своя: палитра задаёт тон и насыщенность,
        // но прозрачность должна остаться прежней.
        var picker = new ColorPicker { Color = new RgbaColor(0x38, 0x84, 0xFF, 0x40) };
        var field = Field(picker);

        field.Hue = 240d;
        field.Pick(new Point(FieldWidth, 0d));

        Assert.Equal(new RgbaColor(0x00, 0x00, 0xFF, 0x40), picker.Color);
    });

    [Fact]
    public void ВыборПриПрозрачномЦвете_ОстаётсяПрозрачным() => Sta.Run(() =>
    {
        var picker = new ColorPicker { Color = RgbaColor.Transparent };
        var field = Field(picker);

        field.Hue = 120d;
        field.Pick(new Point(FieldWidth, 0d));

        Assert.Equal(0, picker.Color.A);
    });

    [Fact]
    public void ВыборТона_МеняетЦветИТонОбеихПлоскостей() => Sta.Run(() =>
    {
        var picker = new ColorPicker { Color = new RgbaColor(0xFF, 0x00, 0x00) };
        var field = Field(picker);
        var strip = Strip(picker);

        // Тон у поля и у полосы один: выбрали тон — обе плоскости встают на него, не теряя ни
        // насыщенности, ни яркости.
        strip.Pick(new Point(StripWidth / 4d, 0d));

        Assert.Equal(new RgbaColor(0x80, 0xFF, 0x00), picker.Color);
        Assert.Equal(1d, field.Saturation, 3);
        Assert.Equal(1d, field.Value, 3);

        // Тон плоскостей приходит уже из цвета, а восемь бит на канал дают не 90°, а ближайший
        // к ним оттенок: без этой оговорки любая правка цвета сдвигала бы отметки.
        var hue = HsvColor.FromRgba(picker.Color).Hue;

        Assert.Equal(hue, field.Hue, 3);
        Assert.Equal(hue, strip.Hue, 3);
    });

    [Fact]
    public void СвойстваПалитры_НеПравятЦветСами() => Sta.Run(() =>
    {
        // Связь односторонняя: пока цвет ставит программа, круг HSV -> RGB -> HSV со сдвигом на
        // байт дёргал бы столбики при каждой правке настроек.
        var color = new RgbaColor(0x12, 0x34, 0x56, 0x40);
        var picker = new ColorPicker { Color = color };
        var field = Field(picker);

        field.Hue = 300d;
        field.Saturation = 0d;
        field.Value = 1d;

        Assert.Equal(color, picker.Color);
    });

    [Fact]
    public void ВыборВПоле_ВиденВОбразце() => Sta.Run(() =>
    {
        var picker = new ColorPicker { Color = new RgbaColor(0xFF, 0x00, 0x00) };
        var field = Field(picker);

        field.Hue = 240d;
        field.Pick(new Point(FieldWidth, 0d));

        var swatch = Assert.IsType<SolidColorBrush>(Assert.IsType<Border>(picker.FindName("Swatch")).Background);

        Assert.Equal(Colors.Blue, swatch.Color);
    });

    private const double FieldWidth = 236d;
    private const double FieldHeight = 120d;
    private const double StripWidth = 240d;
    private const double StripHeight = 18d;

    /// <summary>
    /// Плоскость палитры из попапа: она лежит в отдельном визуальном дереве и само себя не
    /// меряет, поэтому габариты задаются здесь — как их задаст контейнер попапа. Ширина полосы
    /// взята кратной четырём: её четверть — ровно 90 градусов.
    /// </summary>
    private static ColorField Field(ColorPicker picker) => Plane<ColorField>(picker, "HueField", FieldWidth, FieldHeight);

    private static HueStrip Strip(ColorPicker picker) => Plane<HueStrip>(picker, "HueBar", StripWidth, StripHeight);

    private static T Plane<T>(ColorPicker picker, string name, double width, double height)
        where T : ColorPlane
    {
        var plane = picker.FindName(name) as T;

        Assert.True(plane is not null, $"В панели выбора цвета нет элемента {name}.");

        plane!.Width = width;
        plane.Height = height;
        plane.Measure(new Size(width, height));
        plane.Arrange(new Rect(0d, 0d, width, height));

        return plane;
    }
}