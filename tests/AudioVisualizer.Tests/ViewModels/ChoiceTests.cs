using AudioVisualizer.App.ViewModels;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.ViewModels;

public class ChoiceTests
{
    [Fact]
    public void ToString_ВозвращаетПодпись()
    {
        var choice = new Choice<CornerStyle>(CornerStyle.OuterRounded, "Скруглённые снаружи");

        Assert.Equal("Скруглённые снаружи", choice.ToString());
    }

    [Fact]
    public void ToString_ВсегдаПодпись_АНеСоставЗаписи()
    {
        var choice = new Choice<BarFillMode>(BarFillMode.Solid, "Однотонная");

        Assert.DoesNotContain("Choice", choice.ToString());
        Assert.DoesNotContain("Solid", choice.ToString());
    }
}