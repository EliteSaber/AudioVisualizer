namespace AudioVisualizer.Tests;

/// <summary>
/// Тесты, создающие элементы WPF, выполняются по одному.
/// </summary>
/// <remarks>
/// Элементы требуют STA и общих ресурсов приложения, а xUnit по умолчанию распараллеливает
/// классы. Из-за этого тест управления цветом то создавался, то падал на разборе своей же
/// разметки: словарь с кистями ещё не был загружен, а <c>StaticResource</c> на несуществующей
/// кисти роняет разбор сразу. Порядок запуска в таком тесте становится частью условия.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WpfUi
{
    public const string Name = "WPF";
}