namespace AudioVisualizer.Tests;

/// <summary>Временный каталог с автоудалением: тесты настроек пишут настоящие файлы.</summary>
internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Root = Path.Combine(Path.GetTempPath(), $"av-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string File(string name) => Path.Combine(Root, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}