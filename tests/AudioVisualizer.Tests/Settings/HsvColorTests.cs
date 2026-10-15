using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Settings;

/// <summary>
/// Перевод цвета в HSV и обратно. Палитра выбора цвета работает в этой модели, и ошибка в
/// переводе выглядит снаружи как «палитра показывает не тот цвет».
/// </summary>
public class HsvColorTests
{
    /// <summary>
    /// Круг туда-обратно обязан возвращать исходные каналы: палитра показывает текущий цвет
    /// и по нему же выбирают новый, и любой сдвиг на байт виден на столбиках как дрожание.
    /// Набор цветов взят из настроек по умолчанию и по краям диапазона.
    /// </summary>
    [Theory]
    [InlineData(0xFF, 0x00, 0x00)]
    [InlineData(0x00, 0xFF, 0x00)]
    [InlineData(0x00, 0x00, 0xFF)]
    [InlineData(0x38, 0x84, 0xFF)]
    [InlineData(0xFF, 0x60, 0xA8)]
    [InlineData(0x12, 0x12, 0x18)]
    [InlineData(0x7F, 0x7F, 0x7F)]
    [InlineData(0x01, 0x01, 0x02)]
    [InlineData(0xFF, 0xFF, 0xFF)]
    [InlineData(0x00, 0x00, 0x00)]
    public void ЦветТудаИОбратно_НеМеняется(byte red, byte green, byte blue)
    {
        var color = new RgbaColor(red, green, blue, 128);

        Assert.Equal(color, HsvColor.FromRgba(color).ToRgba(128));
    }

    [Fact]
    public void ОсновныеТоны_СтоятСвоиМеста()
    {
        // Полоса тона размечена градусами, и если тон сместится, весь круг поедет вместе с ним.
        Assert.Equal(0d, HsvColor.FromRgba(new RgbaColor(0xFF, 0x00, 0x00)).Hue, 3);
        Assert.Equal(120d, HsvColor.FromRgba(new RgbaColor(0x00, 0xFF, 0x00)).Hue, 3);
        Assert.Equal(240d, HsvColor.FromRgba(new RgbaColor(0x00, 0x00, 0xFF)).Hue, 3);
    }

    [Fact]
    public void Серый_БезНасыщенности()
    {
        // У серого тона нет. Подставить 0 нужно явно: иначе серый получил бы оттенок из
        // остатка деления и поехал по кругу вместо того, чтобы стоять на нуле.
        var hsv = HsvColor.FromRgba(new RgbaColor(0x80, 0x80, 0x80));

        Assert.Equal(0d, hsv.Saturation, 3);
        Assert.Equal(0d, hsv.Hue, 3);
        Assert.Equal(0x80 / 255d, hsv.Value, 3);
    }

    [Fact]
    public void ТонОборачиваетсяПоКругу()
    {
        Assert.Equal(10d, new HsvColor(370d, 1d, 1d).Hue, 3);
        Assert.Equal(350d, new HsvColor(-10d, 1d, 1d).Hue, 3);
        Assert.Equal(0d, new HsvColor(HsvColor.HueCircle, 1d, 1d).Hue, 3);
    }

    [Fact]
    public void ЗначенияВнеДиапазона_Зажимаются()
    {
        // Точка за краем поля должна давать крайний цвет, а не значение, из которого цвет уже
        // не собрать.
        var hsv = new HsvColor(-10d, 2d, -1d);

        Assert.Equal(350d, hsv.Hue, 3);
        Assert.Equal(1d, hsv.Saturation, 3);
        Assert.Equal(0d, hsv.Value, 3);
    }

    [Fact]
    public void Альфа_БерётсяИзАргумента()
    {
        // HSV альфу не описывает: у полупрозрачной центральной линии палитра её стирать не должна.
        var green = new HsvColor(120d, 1d, 1d);

        Assert.Equal(new RgbaColor(0x00, 0xFF, 0x00, 0), green.ToRgba(0));
        Assert.Equal(new RgbaColor(0x00, 0xFF, 0x00, 255), green.ToRgba(255));
    }

    [Fact]
    public void Радуга_НачинаетсяИЗаканчиваетсяКрасным()
    {
        // Круг замкнут: без совпадения концов на стыке полосы тона был бы чужой оттенок.
        var ramp = HsvColor.HueRamp(HsvColor.RampSteps);

        Assert.Equal(HsvColor.RampSteps, ramp.Count);
        Assert.Equal(new RgbaColor(0xFF, 0x00, 0x00), ramp[0]);
        Assert.Equal(ramp[0], ramp[^1]);
    }

    [Fact]
    public void Радуга_ПроходитЧерезСектораКруга()
    {
        var ramp = HsvColor.HueRamp(HsvColor.RampSteps);
        var sector = (HsvColor.RampSteps - 1) / 6;

        Assert.Equal(new RgbaColor(0xFF, 0x00, 0x00), ramp[0]);
        Assert.Equal(new RgbaColor(0xFF, 0xFF, 0x00), ramp[sector]);
        Assert.Equal(new RgbaColor(0x00, 0xFF, 0x00), ramp[2 * sector]);
        Assert.Equal(new RgbaColor(0x00, 0xFF, 0xFF), ramp[3 * sector]);
        Assert.Equal(new RgbaColor(0x00, 0x00, 0xFF), ramp[4 * sector]);
        Assert.Equal(new RgbaColor(0xFF, 0x00, 0xFF), ramp[5 * sector]);
    }

    [Fact]
    public void Радуга_ДлиннееОдногоСектора_НужнаНеОбязательно()
    {
        // Отрезка из двух точек достаточно для ровной заливки: проверяется только то, что
        // градиент вообще собирается и концы у него красные.
        var ramp = HsvColor.HueRamp(2);

        Assert.Equal(2, ramp.Count);
        Assert.Equal(ramp[0], ramp[1]);

        Assert.Throws<ArgumentOutOfRangeException>(() => HsvColor.HueRamp(1));
    }
}