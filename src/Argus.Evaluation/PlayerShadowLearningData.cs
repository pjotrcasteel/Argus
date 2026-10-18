using System.Globalization;

namespace Argus.Evaluation;

/// <summary>
/// One independent reviewed snapshot around an unverified shadow player location.
/// Label 1 means player was human-visible, 0 human-absent, null unknown/unreviewed.
/// No location, game decision or future frame may be used as a training feature.
/// </summary>
public sealed record PlayerShadowLearningRow(string RunId, int Step, string RawBridge,
    int PlayerPalettePixels, double RgbStdDev, double StrongEdgeFraction, string Witness, int? Label)
{
    public double[] Features() =>
    [
        Math.Log(1 + PlayerPalettePixels), RgbStdDev / 64, StrongEdgeFraction * 4,
        RawBridge == "BRIDGE_TWO" ? 1d : 0d
    ];
}

public static class PlayerShadowLearningData
{
    public const string Header = "run_id,step,raw_bridge,player_palette_pixels,max_rgb_stddev,strong_edge_fraction,witness,reviewed_candidate_local_label_v2";
    public const string CsvName = "player-shadow-feature-rows.csv";

    /// <summary>
    /// Only read explicitly named dev-* child folders. Never enumerate into run-01/02,
    /// held-out subfolders or any other research evidence.
    /// </summary>
    public static IReadOnlyList<PlayerShadowLearningRow> ReadDevelopment(string root)
    {
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var all = new List<PlayerShadowLearningRow>();
        var dirs = Directory.GetDirectories(root, "dev-*", SearchOption.TopDirectoryOnly)
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (dirs.Length > 128) throw new InvalidDataException("Development run limit exceeded.");
        foreach (var dir in dirs)
        {
            var runId = Path.GetFileName(dir);
            if (runId.Length > 64 || runId.Length < 5 || !runId.StartsWith("dev-", StringComparison.Ordinal) ||
                runId.Skip(4).Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
                throw new InvalidDataException("Unsafe development run name.");
            var file = Path.Combine(dir, CsvName);
            if (!File.Exists(file)) continue;
            var lines = File.ReadAllLines(file);
            if (lines.Length is < 2 or > 20_001 || lines[0] != Header)
                throw new InvalidDataException("Invalid development feature CSV header or size for " + runId);
            var steps = new HashSet<int>();
            var previous = -1;
            foreach (var line in lines.Skip(1))
            {
                var cols = ParseLine(line);
                if (cols.Count != 8 || cols[0] != runId ||
                    !int.TryParse(cols[1], NumberStyles.None, CultureInfo.InvariantCulture, out var step) ||
                    step <= previous || !steps.Add(step) ||
                    cols[2] is not ("BRIDGE_ONE" or "BRIDGE_TWO") ||
                    !int.TryParse(cols[3], NumberStyles.None, CultureInfo.InvariantCulture, out var palette) ||
                    palette is < 0 or > 2401 ||
                    !double.TryParse(cols[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var variation) ||
                    !double.IsFinite(variation) || variation is < 0 or > 128 ||
                    !double.TryParse(cols[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var edge) ||
                    !double.IsFinite(edge) || edge is < 0 or > 1 ||
                    cols[6] is not ("EMPTY_PATCH_VETO" or "INDETERMINATE") ||
                    cols[7] is not ("" or "0" or "1"))
                    throw new InvalidDataException("Invalid feature record for " + runId);
                // These signals must agree with the original M2.11 pixel veto.
                if (cols[6] == "EMPTY_PATCH_VETO" &&
                    (palette != 0 || variation >= 5 || edge >= 0.01))
                    throw new InvalidDataException("Witness contradicts recorded pixel measurements.");
                all.Add(new PlayerShadowLearningRow(runId, step, cols[2], palette, variation, edge,
                    cols[6], cols[7] == "" ? null : cols[7] == "1" ? 1 : 0));
                previous = step;
            }
        }
        return all;
    }

    private static IReadOnlyList<string> ParseLine(string line)
    {
        var result = new List<string>();
        var value = new System.Text.StringBuilder();
        var quoted = false;
        var closed = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    value.Append('"');
                    i++;
                }
                else if (c == '"') { quoted = false; closed = true; }
                else value.Append(c);
            }
            else if (c == ',')
            {
                result.Add(value.ToString());
                value.Clear();
                closed = false;
            }
            else if (c == '"' && value.Length == 0 && !closed) quoted = true;
            else if (closed || c == '"') throw new InvalidDataException("Malformed quoted CSV.");
            else value.Append(c);
        }
        if (quoted) throw new InvalidDataException("Unterminated quoted CSV field.");
        result.Add(value.ToString());
        return result;
    }
}
