using System.Diagnostics;
using System.Globalization;
using Argus.Core;

namespace Argus.Adapters.Browser;

/// <summary>Local, free, screenshot-only full-page and bounded footer OCR. Requires Tesseract on host.</summary>
public sealed class TesseractTextReader : IFrameTextReader
{
    private static readonly TimeSpan FullFrameBudget = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan FooterBudget = TimeSpan.FromSeconds(2);

    public async Task<IReadOnlyList<VisibleText>> ReadAsync(FrameObservation frame, CancellationToken cancellationToken = default)
    {
        var identity = Guid.NewGuid().ToString("N");
        var original = Path.Combine(Path.GetTempPath(), "argus-frame-" + identity + ".png");
        var footerPath = Path.Combine(Path.GetTempPath(), "argus-footer-" + identity + ".bmp");
        try
        {
            await File.WriteAllBytesAsync(original, frame.PngBytes, cancellationToken);
            var entire = await ReadWithBudgetAsync(original, 3, FullFrameBudget, cancellationToken);
            var footer = FooterOcrImage.FromPng(frame.PngBytes);
            if (footer is null) return entire;

            // A second OCR pass over viewport-relative, enlarged original
            // pixels recovers tiny buttons without game-specific coordinates.
            await File.WriteAllBytesAsync(footerPath, footer.BmpBytes, cancellationToken);
            var smallText = await ReadWithBudgetAsync(footerPath, 6, FooterBudget, cancellationToken);
            return entire.Concat(smallText.Select(footer.Map)).ToArray();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException(
                "Tesseract not installed. Install free Tesseract OCR or use --agent observe.", ex);
        }
        finally
        {
            if (File.Exists(original)) File.Delete(original);
            if (File.Exists(footerPath)) File.Delete(footerPath);
        }
    }

    private static async Task<IReadOnlyList<VisibleText>> ReadWithBudgetAsync(
        string file, int segmentation, TimeSpan duration, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(duration);
        try
        {
            return await ReadOcrAsync(file, segmentation, budget.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Preserve any previous OCR pass; an expensive screenshot is unknown, not empty game state.
            return [];
        }
    }

    private static async Task<IReadOnlyList<VisibleText>> ReadOcrAsync(string file, int segmentation, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("tesseract")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var item in new[] { file, "stdout", "--psm", segmentation.ToString(CultureInfo.InvariantCulture), "tsv" })
            start.ArgumentList.Add(item);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start local Tesseract OCR.");
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errors = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var tsv = await output;
            var error = await errors;
            if (process.ExitCode != 0)
                throw new InvalidOperationException("Tesseract OCR failed: " + error);
            return TesseractTsvParser.Parse(tsv);
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
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
