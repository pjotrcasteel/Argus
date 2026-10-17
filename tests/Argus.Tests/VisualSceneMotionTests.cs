using Argus.Agents.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Argus.Tests;

[TestClass]
public sealed class VisualSceneMotionTests
{
    private const int Width = 390;
    private const int Height = 844;

    [TestMethod]
    public void EstimateRgb_TexturedBackgroundMoves_DetectsPixelTranslation()
    {
        var previous = MakeTexturedFrame();
        var current = Translate(previous, 16, -8);

        var result = VisualSceneMotion.EstimateRgb(previous, current, Width, Height);

        Assert.IsNotNull(result);
        Assert.AreEqual(16, result.X);
        Assert.AreEqual(-8, result.Y);
        Assert.IsTrue(result.Confidence >= 0.6f);
    }

    [TestMethod]
    public void EstimateRgb_UntexturedBackground_ReturnsUnknown()
    {
        var empty = new byte[Width * Height * 3];

        Assert.IsNull(VisualSceneMotion.EstimateRgb(empty, empty, Width, Height));
    }

    [TestMethod]
    public void EstimateRgb_IdenticalTexturedScreen_DoesNotInventCameraMovement()
    {
        var screenshot = MakeTexturedFrame();

        var result = VisualSceneMotion.EstimateRgb(screenshot, screenshot, Width, Height);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.X);
        Assert.AreEqual(0, result.Y);
    }

    [TestMethod]
    public void Analyze_SceneryShiftedCandidate_ReportsStationaryRelativeToBackground()
    {
        var previous = new CombatVisualObservation(Player(195, 420), [Player(105, 405)]);
        var current = new CombatVisualObservation(Player(195, 420), [Player(121, 397)]);

        var report = ThreatMotionAnalyzer.Analyze(previous, current, new VisualSceneShift(16, -8, 0.9f, 2));

        Assert.AreEqual(1, report.Matched);
        Assert.AreEqual(0, report.Moving);
        Assert.AreEqual(1, report.Stationary);
    }

    [TestMethod]
    public void Analyze_SceneCompensatedCandidateMotion_ReportsMovement()
    {
        var previous = new CombatVisualObservation(Player(195, 420), [Player(105, 405)]);
        var current = new CombatVisualObservation(Player(195, 420), [Player(138, 397)]);

        var report = ThreatMotionAnalyzer.Analyze(previous, current, new VisualSceneShift(16, -8, 0.9f, 2));

        Assert.AreEqual(1, report.Matched);
        Assert.AreEqual(1, report.Moving);
        Assert.AreEqual(0, report.Stationary);
    }

    [TestMethod]
    public void Analyze_NoReliableSceneShift_DoesNotInventThreatVelocity()
    {
        var previous = new CombatVisualObservation(Player(195, 420), [Player(105, 405)]);
        var current = new CombatVisualObservation(Player(195, 420), [Player(138, 397)]);

        var report = ThreatMotionAnalyzer.Analyze(previous, current, null);

        Assert.AreEqual(0, report.Matched);
        Assert.AreEqual(0, report.Moving);
    }

    private static VisualBlob Player(float x, float y) => new(x, y, 20, 28, 200, 0.7f);

    private static byte[] MakeTexturedFrame()
    {
        var image = new byte[Width * Height * 3];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                // Reproducible textured background, not a fake game screenshot.
                var hash = unchecked((uint)(x * 73856093 ^ y * 19349663));
                var value = (byte)(25 + hash % 180);
                var p = (y * Width + x) * 3;
                image[p] = value;
                image[p + 1] = (byte)(value / 2);
                image[p + 2] = (byte)(value / 3);
            }
        }
        return image;
    }

    private static byte[] Translate(byte[] image, int dx, int dy)
    {
        var translated = new byte[image.Length];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var sourceX = x - dx;
                var sourceY = y - dy;
                if (sourceX < 0 || sourceY < 0 || sourceX >= Width || sourceY >= Height) continue;
                var dst = (y * Width + x) * 3;
                var src = (sourceY * Width + sourceX) * 3;
                for (var c = 0; c < 3; c++) translated[dst + c] = image[src + c];
            }
        }
        return translated;
    }
}
