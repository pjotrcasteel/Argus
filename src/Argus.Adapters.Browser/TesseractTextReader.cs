using System.Diagnostics;
using System.Globalization;
using Argus.Core;

namespace Argus.Adapters.Browser;

/// <summary>Local, free automatic-page OCR for mixed scenic menus and small action controls, from viewport pixels only.</summary>
public sealed class TesseractTextReader : IFrameTextReader
{
    public async Task<IReadOnlyList<VisibleText>> ReadAsync(FrameObservation frame, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(Path.GetTempPath(), "argus-frame-" + Guid.NewGuid().ToString("N") + ".png");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            await File.WriteAllBytesAsync(path, frame.PngBytes, limit.Token);
            var start = new ProcessStartInfo("tesseract")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (var arg in new[] { path, "stdout", "--psm", "3", "tsv" }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start local Tesseract OCR.");
            try
            {
                var output = process.StandardOutput.ReadToEndAsync(limit.Token);
                var errors = process.StandardError.ReadToEndAsync(limit.Token);
                await process.WaitForExitAsync(limit.Token);
                var tsv = await output;
                var error = await errors;
                if (process.ExitCode != 0) throw new InvalidOperationException("Tesseract OCR failed: " + error);
                return TesseractTsvParser.Parse(tsv);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // One slow OCR frame is not a failure of the whole game experiment.
                // Do not invent text; the agent may use pixel-only evidence or wait for the next frame.
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                return [];
            }
            catch
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                throw;
            }
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            throw new InvalidOperationException("Tesseract not installed. Install free Tesseract OCR or run --agent observe.", e);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}

public static class TesseractTsvParser
{
    public static IReadOnlyList<VisibleText> Parse(string tsv)
    {
        var words = new List<(string Group, VisibleText Box)>();
        foreach (var line in tsv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1))
        {
            var parts = line.TrimEnd('\r').Split('\t');
            if (parts.Length < 12 || parts[0] != "5") continue;
            if (!float.TryParse(parts[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var confidence) || confidence < 0) continue;
            if (!int.TryParse(parts[6], out var left) || !int.TryParse(parts[7], out var top) ||
                !int.TryParse(parts[8], out var width) || !int.TryParse(parts[9], out var height)) continue;

            var text = string.Join('\t', parts.Skip(11)).Trim();
            if (text.Length == 0) continue;
            var group = string.Join(':', parts[2], parts[3], parts[4]);
            words.Add((group, new VisibleText(text, left, top, width, height, confidence)));
        }

        return words.GroupBy(w => w.Group).Select(group =>
        {
            var boxes = group.Select(x => x.Box).ToArray();
            var left = boxes.Min(x => x.X);
            var top = boxes.Min(x => x.Y);
            var right = boxes.Max(x => x.X + x.Width);
            var bottom = boxes.Max(x => x.Y + x.Height);
            return new VisibleText(string.Join(' ', boxes.Select(x => x.Text)), left, top, right - left, bottom - top,
                boxes.Average(x => x.Confidence));
        }).ToArray();
    }
}
