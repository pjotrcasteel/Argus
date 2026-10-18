using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Argus.Evaluation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class SpatialProbeLearningTests
{
    [TestMethod]
    public void Evaluate_ThreeIndependentRunGroups_FitsDeterministicImageOnlyLearner()
    {
        var rows = ArtificialRows().Reverse().ToArray();
        var a = SpatialProbeLearning.Evaluate(rows);
        var b = SpatialProbeLearning.Evaluate(rows.Reverse().ToArray());

        Assert.AreEqual("EXPLORATORY_TRAINED", a.Status);
        Assert.AreEqual(72, a.Labeled);
        Assert.HasCount(3, a.LeaveOneRunOut);
        Assert.IsNotNull(a.PrivateResearchModel);
        CollectionAssert.AreEqual(a.PrivateResearchModel.Weights, b.PrivateResearchModel!.Weights);
        CollectionAssert.AreEqual(a.PrivateResearchModel.Means, b.PrivateResearchModel.Means);
        Assert.IsTrue(a.PrivateResearchModel.Weights.Any(w => Math.Abs(w) > 0.01));
        foreach (var fold in a.LeaveOneRunOut)
        {
            Assert.HasCount(2, fold.FitRuns);
            Assert.IsFalse(fold.FitRuns.Contains(fold.HeldOutRunId));
            Assert.AreEqual(24, fold.TruePositive + fold.FalsePositive + fold.FalseNegative + fold.TrueNegative);
        }
        StringAssert.Contains(SpatialProbeLearning.RenderMarkdown(a), "Center-only baseline");
    }

    [TestMethod]
    public void Evaluate_InsufficientPerRunHumanClassCoverage_NeverFitsWeights()
    {
        var rows = ArtificialRows().Select(x => x.RunId == "dev-g" && x.HumanLabel == 1
            ? x with { HumanLabel = null } : x).ToArray();
        var result = SpatialProbeLearning.Evaluate(rows);

        Assert.AreEqual("NOT_READY", result.Status);
        Assert.IsNull(result.PrivateResearchModel);
        Assert.HasCount(0, result.LeaveOneRunOut);
        Assert.IsTrue(result.Blockers.Any(x => x.StartsWith("dev-g:", StringComparison.Ordinal)));
        Assert.AreEqual(4, result.Runs.Single(x => x.RunId == "dev-g").Unknown);
    }

    [TestMethod]
    public void Evaluate_HeldoutDuplicateAndNonfinitePixelEvidence_Rejects()
    {
        var rows = ArtificialRows().ToList();
        rows.Add(rows[0]);
        Assert.ThrowsExactly<InvalidDataException>(() => SpatialProbeLearning.Evaluate(rows));
        rows.RemoveAt(rows.Count - 1);
        rows.Add(rows[0] with { RunId = "run-01" });
        Assert.ThrowsExactly<InvalidDataException>(() => SpatialProbeLearning.Evaluate(rows));
        rows.RemoveAt(rows.Count - 1);
        rows.Add(rows[0] with { ImageFeatures = [double.NaN, 0, 0, 0, 0] });
        Assert.ThrowsExactly<InvalidDataException>(() => SpatialProbeLearning.Evaluate(rows));
    }

    [TestMethod]
    public void ReadPrivate_AuthenticFormatSyntheticPngsAndLabels_ExtractsImageFeaturesAndTrains()
    {
        using var source = new SyntheticReview();
        var rows = SpatialProbeImporter.ReadPrivate(source.Archive, source.LabelPaths);
        Assert.HasCount(72, rows);
        Assert.AreEqual("EXPLORATORY_TRAINED", SpatialProbeLearning.Evaluate(rows).Status);
        Assert.IsTrue(rows.All(r => r.ImageFeatures.Length == 5 && r.ImageFeatures.All(double.IsFinite)));
        Assert.IsTrue(rows.Where(r => r.Role == "spatial-control").All(r => r.HumanLabel == 0));
    }

    [TestMethod]
    public void ReadPrivate_ContradictoryHumanLocations_RejectsWithoutChangingOwnerLabel()
    {
        using var source = new SyntheticReview();
        var json = File.ReadAllText(source.LabelPaths[0]);
        using var document = JsonDocument.Parse(json);
        var run = document.RootElement;
        var probes = run.GetProperty("probes").EnumerateArray()
            .Select(x => JsonSerializer.Deserialize<Dictionary<string, object>>(x.GetRawText())!).ToList();
        // Both disjoint original circles in the SAME screenshot cannot contain the one player.
        probes[1]["label"] = "present";
        File.WriteAllText(source.LabelPaths[0], JsonSerializer.Serialize(new
        {
            schemaVersion = 1, studySeed = SpatialProbeImporter.Seed, runId = "dev-f", probes
        }));
        var error = Assert.ThrowsExactly<InvalidDataException>(() =>
            SpatialProbeImporter.ReadPrivate(source.Archive, source.LabelPaths));
        StringAssert.Contains(error.Message, "CONTRADICTORY HUMAN LABELS");
    }

    [TestMethod]
    public void ReadPrivate_UnreviewedOrMovedReference_RejectsTampering()
    {
        using var source = new SyntheticReview();
        var json = File.ReadAllText(source.LabelPaths[1]);
        var malformed = json.Replace("\"x\":195", "\"x\":194", StringComparison.Ordinal);
        File.WriteAllText(source.LabelPaths[1], malformed);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            SpatialProbeImporter.ReadPrivate(source.Archive, source.LabelPaths));
    }

    private static SpatialProbeRow[] ArtificialRows() =>
        new[] { "dev-f", "dev-g", "dev-h" }.SelectMany((run, index) =>
            Enumerable.Range(0, 24).Select(i =>
            {
                var isCenter = i % 2 == 0;
                var truth = isCenter && i < 8 ? 1 : 0;
                var contrast = truth == 1 ? 0.84 : 0.17;
                return new SpatialProbeRow(run, run + "-sample-" + i, i / 2,
                    isCenter ? "center-anchor" : "spatial-control",
                    [contrast + index * 0.01, 0.1 + index * 0.01, 0.12 + index * 0.01,
                        0.2 + contrast / 4, 0.3 + contrast / 5], truth);
            })).ToArray();

    private sealed class SyntheticReview : IDisposable
    {
        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
        private readonly string folder = Path.Combine(Path.GetTempPath(), "argus-m216-" + Guid.NewGuid().ToString("N"));
        public string Archive => Path.Combine(folder, "source-review.zip");
        public string[] LabelPaths { get; } = new string[3];

        public SyntheticReview()
        {
            Directory.CreateDirectory(folder);
            var all = new List<object>();
            var runs = new[] { "dev-f", "dev-g", "dev-h" };
            using (var zip = ZipFile.Open(Archive, ZipArchiveMode.Create))
            {
                Add(zip, "index.html", Encoding.UTF8.GetBytes("SYNTHETIC EMPTY REVIEW"));
                for (var r = 0; r < runs.Length; r++)
                {
                    var id = runs[r];
                    var pictures = new List<object>();
                    var original = new List<Probe>();
                    var human = new List<Probe>();
                    Add(zip, $"annoteren/{id}/index.html", Encoding.UTF8.GetBytes("SYNTHETIC"));
                    for (var i = 0; i < 12; i++)
                    {
                        var bytes = CreatePng((byte)(30 + r * 15 + i));
                        var filename = $"frame-{i:D4}.png";
                        Add(zip, $"annoteren/{id}/{filename}", bytes);
                        pictures.Add(new { step = i, file = filename,
                            sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() });
                        var center = new Probe($"{id}-step{i:D4}-center-anchor", i, 195, 422, 26,
                            "center-anchor", "unknown");
                        var peripheral = new Probe($"{id}-step{i:D4}-spatial-control", i, 72, 180, 26,
                            "spatial-control", "unknown");
                        original.Add(center);
                        original.Add(peripheral);
                        human.Add(center with { Label = i < 3 ? "present" : "absent" });
                        human.Add(peripheral with { Label = "absent" });
                    }
                    all.Add(new { schemaVersion = 1, runId = id, partition = "development",
                        traceSha256 = new string((char)('a' + r), 64), images = pictures,
                        candidateCount = 24, candidates = original });
                    LabelPaths[r] = Path.Combine(folder, $"{id}.json");
                    File.WriteAllText(LabelPaths[r], JsonSerializer.Serialize(new
                    {
                        schemaVersion = 1, studySeed = SpatialProbeImporter.Seed, runId = id, probes = human
                    }, Options));
                }
                Add(zip, "spatial-study-plan.json", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    schemaVersion = 1, mode = "development-only",
                    sourceSeed = "argus-m214-blind-24-bins-development-only-20261018",
                    studySeed = SpatialProbeImporter.Seed, radiusPixels = 26,
                    sampling = "SYNTHETIC", humanTruth = "unknown",
                    runIds = runs, runs = all
                }, Options)));
            }
        }

        public void Dispose() => Directory.Delete(folder, true);

        private static void Add(ZipArchive zip, string path, byte[] bytes)
        {
            using var stream = zip.CreateEntry(path, CompressionLevel.Fastest).Open();
            stream.Write(bytes);
        }

        private static byte[] CreatePng(byte gray)
        {
            const int width = 390, height = 844;
            using var raw = new MemoryStream();
            for (var y = 0; y < height; y++)
            {
                raw.WriteByte(0); // PNG filter: none.
                for (var x = 0; x < width; x++)
                {
                    var pixel = (byte)(gray + (x > 174 && x < 216 && y > 402 && y < 442 ? 90 : 0));
                    raw.WriteByte(pixel);
                    raw.WriteByte((byte)(pixel / 2));
                    raw.WriteByte((byte)(pixel / 3));
                }
            }
            using var image = new MemoryStream();
            image.Write([137, 80, 78, 71, 13, 10, 26, 10]);
            var ihdr = new byte[13];
            BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), width);
            BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4, 4), height);
            ihdr[8] = 8; ihdr[9] = 2;
            Chunk(image, "IHDR", ihdr);
            using var packed = new MemoryStream();
            using (var compression = new ZLibStream(packed, CompressionLevel.Fastest, true))
                compression.Write(raw.ToArray());
            Chunk(image, "IDAT", packed.ToArray());
            Chunk(image, "IEND", []);
            return image.ToArray();
        }

        private static void Chunk(Stream stream, string type, byte[] data)
        {
            var kind = Encoding.ASCII.GetBytes(type);
            var length = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
            stream.Write(length); stream.Write(kind); stream.Write(data);
            uint crc = 0xffffffff;
            foreach (var b in kind.Concat(data))
            {
                crc ^= b;
                for (var bit = 0; bit < 8; bit++)
                    crc = (crc & 1) == 1 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
            }
            BinaryPrimitives.WriteUInt32BigEndian(length, ~crc);
            stream.Write(length);
        }

        private sealed record Probe(string SampleId, int Step, int X, int Y, int Radius, string Role, string Label);
    }
}
