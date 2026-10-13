using AVAGalaga.Models;

namespace AVAGalaga.Engine;

/// <summary>
/// The computer player for attract mode (and the tests): it lines up under the enemies, fires, and
/// sidesteps shots, divers and tractor beams. It plays well, not perfectly.
/// </summary>
public sealed class AttractPilot(int seed)
{
    private readonly Random _rng = new(seed);
    private double _hesitate;

    /// <summary>How often it fires when lined up (lower makes it a weaker player).</summary>
    public double Eagerness { get; init; } = 0.6;

    public Controls Decide(GameEngine g)
    {
        if (!g.PlayerAlive) return default;
        double me = g.PlayerX;

        // How dangerous is it to stand at x?
        double Danger(double x)
        {
            double d = 0;
            foreach (var s in g.EnemyShots)
            {
                if (s.Pos.Y > Field.PlayerY + 6 || s.Pos.Y < 120) continue;
                double t = (Field.PlayerY - s.Pos.Y) / Math.Max(1, s.Velocity.Y);
                double hitX = s.Pos.X + s.Velocity.X * t;
                double gap = Math.Abs(hitX - x);
                if (gap < 14) d += (14 - gap) * (s.Pos.Y > 200 ? 3 : 1);
            }
            foreach (var e in g.Enemies)
            {
                if (!e.IsDiving && e.Mode != EnemyMode.Entering && e.Mode != EnemyMode.Passing) continue;
                if (e.Pos.Y < 170) continue;
                double gap = Math.Abs(e.Pos.X - x);
                if (gap < 22) d += (22 - gap) * 2;
            }
            if (g.Beamer is { } b && Math.Abs(b.Pos.X - x) < GameEngine.BeamHalfWidth + 14) d += 40;
            if (x < Field.PlayerMinX + 4 || x > Field.PlayerMaxX - 4) d += 5;
            return d;
        }

        // Pick a target: the lowest diver, or the formation enemy closest overhead.
        Enemy? target = g.Enemies.Where(e => e.IsDiving && e.Pos.Y > 30).OrderByDescending(e => e.Pos.Y).FirstOrDefault()
            ?? g.Enemies.OrderBy(e => Math.Abs(e.Pos.X - me)).FirstOrDefault();
        double want = target?.Pos.X ?? Field.CenterX;
        if (target is { Kind: EnemyKind.Commander, Mode: EnemyMode.Beaming }) want = target.Pos.X + (me < target.Pos.X ? -50 : 50);

        double here = Danger(me);
        double best = me, bestScore = here + Math.Abs(want - me) * 0.05;
        foreach (var dx in new[] { -24.0, -12, -6, 6, 12, 24 })
        {
            double x = Math.Clamp(me + dx, Field.PlayerMinX, Field.PlayerMaxX);
            double score = Danger(x) * 1.2 + Math.Abs(want - x) * 0.05;
            if (score < bestScore) { bestScore = score; best = x; }
        }
        if (here < 1 && Math.Abs(want - me) > 3) best = want;

        _hesitate -= Field.Step;
        bool left = best < me - 1.5, right = best > me + 1.5;
        bool lined = g.Enemies.Any(e => Math.Abs(e.Pos.X - me) < 6 && e.Pos.Y < Field.PlayerY - 20);
        bool fire = lined && _hesitate <= 0 && _rng.NextDouble() < Eagerness;
        if (fire) _hesitate = 0.12;
        return new Controls(left, right, fire);
    }
}
