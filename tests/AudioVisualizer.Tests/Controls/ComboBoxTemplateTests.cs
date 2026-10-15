using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using AudioVisualizer.App.ViewModels;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Controls;

[Collection(WpfUi.Name)]
public class ComboBoxTemplateTests
{
    // Регрессия: закрытое значение выводилось как "Choice { Value = ... }", потому что
    // шаблон от DisplayMemberPath до ContentPresenter не доезжал. Списки переведены на явный
    // ItemTemplate, и он обязан попадать в SelectionBoxItemTemplate напрямую.
    [Fact]
    public void ЯвныйItemTemplate_ПопадаетВSelectionBoxItemTemplate() => Sta.Run(() =>
    {
        var template = CreateChoiceTemplate();
        var comboBox = new ComboBox
        {
            ItemTemplate = template,
            ItemsSource = new[] { new Choice<CornerStyle>(CornerStyle.Rounded, "Скруглённые") },
            SelectedIndex = 0
        };
        comboBox.ApplyTemplate();

        Assert.Same(template, comboBox.SelectionBoxItemTemplate);
    });

    [Fact]
    public void ВыбранныйЭлемент_БерётсяИзСписка() => Sta.Run(() =>
    {
        var first = new Choice<CornerStyle>(CornerStyle.Rectangular, "Прямые");
        var comboBox = new ComboBox
        {
            ItemTemplate = CreateChoiceTemplate(),
            ItemsSource = new[] { first },
            SelectedIndex = 0
        };
        comboBox.ApplyTemplate();

        Assert.Same(first, comboBox.SelectionBoxItem);
    });

    private static DataTemplate CreateChoiceTemplate()
        => (DataTemplate)XamlReader.Parse(
            """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <TextBlock Text="{Binding Title}" />
            </DataTemplate>
            """);
}