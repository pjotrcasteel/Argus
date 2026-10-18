using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Argus.Agents.Visual;
using StbImageSharp;

namespace Argus.Evaluation;

/// <summary>
/// Strictly imports the frozen ORIGINAL screenshot-only M2.15 review ZIP plus private owner exports.
/// No held-out files, non-development runs, reselected frames or previously recorded AI guesses.
/// Fail closed rather than fit on tampered, contradictory or inferred labels.
/// </summary>
public static class SpatialProbeImporter
{
    public const string Seed = "argus-m215-independent-spatial-probes-20261018-v1";
    private const string SourceSeed = "argus-m214-blind-24-bins-development-only-20261018";
    private static readonly string[] Runs = ["dev-f", "dev-g", "dev-h"];
    private static readonly HashSet<string> Fields =
        ["sampleId", "step", "x", "y", "radius", "role", "label"];

    public static IReadOnlyList<SpatialProbeRow> ReadPrivate(string archivePath, IEnumerable<string> exportPaths)
    {
        var exports = exportPaths.ToArray();
        if (exports.Length != 3) throw new InvalidDataException("Exactly three private development exports are required.");
        using var zip = ZipFile.OpenRead(archivePath);
        if (zip.Entries.Count != 41 || zip.Entries.Any(e => e.Length > 6_000_000 || e.FullName.Contains("..", StringComparison.Ordinal)))
            throw new InvalidDataException("Original private reviewer archive has unexpected contents.");
        var byName = zip.Entries.ToDictionary(e => e.FullName, StringComparer.Ordinal);
        using var plan = JsonDocument.Parse(ReadText(byName, "spatial-study-plan.json"));
        var root = plan.RootElement;
        if (GetInt(root, "schemaVersion") != 1 || GetString(root, "mode") != "development-only" ||
            GetString(root, "sourceSeed") != SourceSeed || GetString(root, "studySeed") != Seed ||
            GetInt(root, "radiusPixels") != 26)
            throw new InvalidDataException("Frozen M2.15 development manifest mismatch.");
        var declaredRuns = root.GetProperty("runIds").EnumerateArray().Select(x => x.GetString()).ToArray();
        if (!declaredRuns.SequenceEqual(Runs))
            throw new InvalidDataException("Frozen M2.15 run identities differ.");
        var planRuns = root.GetProperty("runs").EnumerateArray().ToArray();
        if (planRuns.Length != 3) throw new InvalidDataException("M2.15 requires three source runs.");

        var ownerExports = new Dictionary<string, JsonDocument>(StringComparer.Ordinal);
        try
        {
            foreach (var path in exports)
            {
                var json = new FileInfo(path);
                if (!json.Exists || json.Length > 100_000)
                    throw new InvalidDataException("Private label export missing or excessive.");
                var doc = JsonDocument.Parse(File.ReadAllText(path));
                var export = doc.RootElement;
                var run = GetString(export, "runId");
                if (!Runs.Contains(run, StringComparer.Ordinal) || !ownerExports.TryAdd(run, doc) ||
                    GetInt(export, "schemaVersion") != 1 || GetString(export, "studySeed") != Seed ||
                    export.EnumerateObject().Count() != 4)
                {
                    doc.Dispose();
                    throw new InvalidDataException("Wrong or duplicate private development label export.");
                }
            }

            var rows = new List<SpatialProbeRow>();
            var allowedArchiveEntries = new HashSet<string>(StringComparer.Ordinal)
                { "spatial-study-plan.json", "index.html" };
            var hashes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (group, index) in planRuns.Select((g, i) => (g, i)))
            {
                var run = Runs[index];
                if (GetString(group, "runId") != run ||
                    !hashes.Add(GetString(group, "traceSha256")))
                    throw new InvalidDataException("Distinct original development run provenance required.");

                var images = group.GetProperty("images").EnumerateArray().ToArray();
                var candidates = group.GetProperty("candidates").EnumerateArray().ToArray();
                if (images.Length != 12 || candidates.Length != 24)
                    throw new InvalidDataException("Frozen original M2.15 sampling changed.");
                allowedArchiveEntries.Add($"annoteren/{run}/index.html");
                var decoded = new Dictionary<int, ImageResult>();
                foreach (var image in images)
                {
                    var step = GetInt(image, "step");
                    var file = GetString(image, "file");
                    if (file != $"frame-{step:D4}.png" || decoded.ContainsKey(step))
                        throw new InvalidDataException("Unexpected original screenshot name/step.");
                    var entryPath = $"annoteren/{run}/{file}";
                    allowedArchiveEntries.Add(entryPath);
                    if (!byName.TryGetValue(entryPath, out var entry))
                        throw new InvalidDataException("Missing original screenshot.");
                    using var stream = entry.Open();
                    using var data = new MemoryStream();
                    stream.CopyTo(data);
                    var png = data.ToArray();
                    if (!string.Equals(Convert.ToHexString(SHA256.HashData(png)),
                        GetString(image, "sha256"), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Original screenshot SHA256 mismatch.");
                    try
                    {
                        var original = ImageResult.FromMemory(png, ColorComponents.RedGreenBlue);
                        if (original.Width != 390 || original.Height != 844)
                            throw new InvalidDataException("Source screenshot viewport changed.");
                        decoded.Add(step, original);
                    }
                    catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException)
                    {
                        throw new InvalidDataException("Invalid original PNG.", ex);
                    }
                }

                var expected = candidates.ToDictionary(x => GetString(x, "sampleId"), StringComparer.Ordinal);
                if (expected.Count != 24)
                    throw new InvalidDataException("Duplicate original candidate sample ID.");
                var review = ownerExports[run].RootElement.GetProperty("probes").EnumerateArray().ToArray();
                if (review.Length != 24)
                    throw new InvalidDataException("Missing or extra human annotation.");
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var byStep = new Dictionary<int, List<(int X, int Y)>>();
                var center = 0;
                var control = 0;
                foreach (var item in review)
                {
                    var keys = item.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
                    if (!keys.SetEquals(Fields))
                        throw new InvalidDataException("Unexpected annotation data fields.");
                    var sample = GetString(item, "sampleId");
                    if (!expected.TryGetValue(sample, out var original) || !seen.Add(sample))
                        throw new InvalidDataException("Duplicate or invented candidate reference.");
                    foreach (var key in new[] { "step", "x", "y", "radius" })
                        if (GetInt(item, key) != GetInt(original, key))
                            throw new InvalidDataException("Human label moved the frozen candidate circle.");
                    var role = GetString(item, "role");
                    if (role != GetString(original, "role") || GetString(original, "label") != "unknown" ||
                        GetInt(item, "radius") != 26)
                        throw new InvalidDataException("Candidate role/radius or original unknown truth changed.");
                    if (role == "center-anchor") center++; else if (role == "spatial-control") control++;
                    else throw new InvalidDataException("Unregistered candidate role.");
                    var label = GetString(item, "label");
                    if (label is not ("present" or "absent" or "unknown"))
                        throw new InvalidDataException("Illegal human candidate label.");
                    var step = GetInt(item, "step");
                    var x = GetInt(item, "x");
                    var y = GetInt(item, "y");
                    if (x < 26 || y < 26 || x >= 390 - 26 || y >= 844 - 26 || !decoded.TryGetValue(step, out var frame))
                        throw new InvalidDataException("Candidate is outside frozen original image.");
                    if (label == "present")
                    {
                        if (!byStep.TryGetValue(step, out var positions))
                        {
                            positions = [];
                            byStep.Add(step, positions);
                        }
                        // One player cannot have its center inside two disjoint 26px circles in one screenshot.
                        if (positions.Any(p => (p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y) > 52 * 52))
                            throw new InvalidDataException($"CONTRADICTORY HUMAN LABELS: {run}, step {step}, " +
                                $"multiple disjoint probes marked present; review privately before learning.");
                        positions.Add((x, y));
                    }
                    var patch = VisualAppearanceDescriptors.Measure(frame.Data, frame.Width, frame.Height,
                        new VisualBlob(x, y, 53, 53, 0, 0));
                    var features = new double[] { patch.MeanLuminance, patch.LuminanceStdDev,
                        patch.ContrastToSurroundings, patch.GradientDensity, patch.MeanSaturation };
                    rows.Add(new SpatialProbeRow(run, sample, step, role, features,
                        label == "unknown" ? null : label == "present" ? 1 : 0));
                }
                if (center != 12 || control != 12 || rows.Count(r => r.RunId == run) != 24)
                    throw new InvalidDataException("M2.15 must retain twelve center and twelve control probes.");
            }
            if (!byName.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(allowedArchiveEntries))
                throw new InvalidDataException("Unrecognized archive contents; private labels or traces must not be embedded.");
            return rows;
        }
        finally
        {
            foreach (var doc in ownerExports.Values) doc.Dispose();
        }
    }

    private static string ReadText(Dictionary<string, ZipArchiveEntry> entries, string name)
    {
        if (!entries.TryGetValue(name, out var entry) || entry.Length > 100_000)
            throw new InvalidDataException("Missing or oversized original manifest.");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private static string GetString(JsonElement json, string name) => json.GetProperty(name).GetString()
        ?? throw new InvalidDataException("Null frozen reference field: " + name);

    private static int GetInt(JsonElement json, string name) => json.GetProperty(name).GetInt32();
}
