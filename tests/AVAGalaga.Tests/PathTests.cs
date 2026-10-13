using AVAGalaga.Engine;
using AVAGalaga.Models;

namespace AVAGalaga.Tests;

public sealed class PathTests
{
    /// <summary>Every path stays near the playfield.</summary>
    [Fact]
    public void Paths_stay_near_the_playfield()
    {
        var paths = new List<(string Name, FlightPath Path)>
        {
            ("top", PathLibrary.TopLoop(false)), ("top-r", PathLibrary.TopLoop(true)),
            ("side", PathLibrary.SideSweep(false)), ("side-r", PathLibrary.SideSweep(true)),
            ("corner", PathLibrary.CornerS(false)), ("corner-r", PathLibrary.CornerS(true)),
        };
        for (int i = 0; i < 5; i++) paths.Add(($"challenge{i}", PathLibrary.Challenge(i, false)));
        // For tuning: write the points out, to be plotted (see tools/plot_paths.sh).
        var dir = Environment.GetEnvironmentVariable("GALAGA_PATHS");
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
            foreach (var (name, p) in paths)
                File.WriteAllLines(Path.Combine(dir, name + ".txt"), p.Points.Select(q => $"{q.X:0.0},{q.Y:0.0}"));
        }
        foreach (var (name, p) in paths)
            Assert.All(p.Points, q => Assert.True(q.X is > -40 and < Field.Width + 40 && q.Y is > -40 and < Field.Height + 40, $"{name} strays to {q}"));
        // Entry paths end on screen, in the upper part, so the flight to the formation is short.
        foreach (var (name, p) in paths.Take(6))
            Assert.True(p.End.Y is > 20 and < 230 && p.End.X is > 0 and < Field.Width, $"{name} ends at {p.End}");

    }
}
