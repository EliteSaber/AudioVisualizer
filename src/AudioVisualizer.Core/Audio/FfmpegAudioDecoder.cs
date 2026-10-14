using System.Diagnostics;
using System.Text;

namespace AudioVisualizer.Core.Audio;

/// <summary>
/// Декодирование средствами ffmpeg: любой формат, который он понимает, приводится к моно WAV
/// с плавающей точкой. Аргументы передаются списком, поэтому путь с пробелами и кириллицей не ломается.
/// </summary>
public static class FfmpegAudioDecoder
{
    public static AudioData Decode(string path, string ffmpegPath, CancellationToken cancellationToken = default)
        => DecodeAsync(path, ffmpegPath, cancellationToken).GetAwaiter().GetResult();

    public static async Task<AudioData> DecodeAsync(string path, string ffmpegPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);

        var startInfo = new ProcessStartInfo(ffmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // Каналы не схлопываем: у ffmpeg свой коэффициент свода 1/sqrt(2) на канал, он поднимает
// громкость на 3 дБ и может завалить моно в ноль. Сводим в моно сами при разборе WAV.
foreach (var argument in new[]
        {
            "-hide_banner", "-loglevel", "error", "-nostdin",
            "-i", path,
            "-vn",
            "-f", "wav",
            "-acodec", "pcm_f32le",
            "-"
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new AudioDecodeException($"Не удалось запустить ffmpeg по пути \"{ffmpegPath}\".");
        }

        using var output = new MemoryStream();
        var error = new StringBuilder();

        // stderr читаем параллельно: иначе процесс встанет, когда pipes переполнятся.
        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(output, cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await using (cancellationToken.Register(static state => KillQuietly((Process)state!), process))
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }

        await outputTask.ConfigureAwait(false);
        var errorText = await errorTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new AudioDecodeException($"ffmpeg не смог декодировать файл (код {process.ExitCode}): {errorText.Trim()}");
        }

        output.Position = 0;
        return WavCodec.Read(output);
    }

    private static void KillQuietly(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Процесс уже завершился - ничего чинить не нужно.
        }
    }
}