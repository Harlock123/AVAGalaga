using AVAGalaga.Models;

namespace AVAGalaga.Engine;

public sealed partial class GameEngine
{
    public const double PlayerSpeed = 120;
    public const double ShotSpeed = 330;
    /// <summary>Shots in the air at once, per fighter (a dual fighter fires pairs).</summary>
    public const int MaxShots = 2;

    /// <summary>The x of each fighter in play: one, or two side by side.</summary>
    public IEnumerable<double> FighterXs => !PlayerAlive ? [] : Dual ? [PlayerX - 8, PlayerX + 8] : [PlayerX];

    private void MovePlayer(double dt, Controls c)
    {
        if (!PlayerAlive) return;
        double dir = (c.Right ? 1 : 0) - (c.Left ? 1 : 0);
        double half = Dual ? 8 : 0;
        PlayerX = Math.Clamp(PlayerX + dir * PlayerSpeed * dt, Field.PlayerMinX + half, Field.PlayerMaxX - half);
    }

    private void Fire()
    {
        if (!PlayerAlive) return;
        int pairs = Dual ? 2 : 1;
        if (PlayerShots.Count >= MaxShots * pairs) return;
        foreach (var x in FighterXs)
        {
            PlayerShots.Add(new Shot { Pos = new Vec(x, Field.PlayerY - 8), Velocity = new Vec(0, -ShotSpeed) });
            ShotsFired++;
        }
        Play(Sfx.Fire);
    }

    private void UpdateShots(double dt)
    {
        foreach (var s in PlayerShots)
        {
            s.Pos += s.Velocity * dt;
            if (s.Pos.Y < -8) s.Dead = true;
        }
        foreach (var s in EnemyShots)
        {
            s.Pos += s.Velocity * dt;
            if (s.Pos.Y > Field.Height + 8) s.Dead = true;
        }
        PlayerShots.RemoveAll(s => s.Dead);
        EnemyShots.RemoveAll(s => s.Dead);
    }

    private static bool Near(Vec a, Vec b, double rx, double ry) => Math.Abs(a.X - b.X) < rx && Math.Abs(a.Y - b.Y) < ry;

    private void Collide()
    {
        // The fighter's shots against the enemies, and against a captive fighter (which loses it).
        foreach (var s in PlayerShots)
        {
            foreach (var e in Enemies)
            {
                if (e.Dead) continue;
                if (e.HasCaptive && Near(s.Pos, e.Pos + new Vec(0, -16), 6, 7))
                {
                    // Shooting your own captured fighter destroys it.
                    s.Dead = true;
                    e.HasCaptive = false;
                    Burst(e.Pos + new Vec(0, -16), fighter: true);
                    Play(Sfx.FighterDie);
                    Popup(e.Pos + new Vec(0, -16), 1000);
                    AddScore(1000);
                    break;
                }
                if (!Near(s.Pos, e.Pos, 7, 7)) continue;
                s.Dead = true;
                Hits++;
                HitEnemy(e);
                break;
            }
        }
        PlayerShots.RemoveAll(s => s.Dead);
        if (!PlayerAlive || Phase is Phase.Captured or Phase.Docking) return;

        // Enemy shots and bodies against the fighter (or either half of a dual fighter).
        foreach (var x in FighterXs.ToList())
        {
            var me = new Vec(x, Field.PlayerY);
            bool hit = false;
            foreach (var s in EnemyShots)
                if (!s.Dead && Near(s.Pos, me, 5, 6)) { s.Dead = true; hit = true; }
            foreach (var e in Enemies)
            {
                if (e.Dead || !Near(e.Pos, me, 10, 9)) continue;
                hit = true;
                HitEnemy(e, rammed: true);
            }
            if (hit) LoseFighter(x);
            if (!PlayerAlive) break;
        }
        EnemyShots.RemoveAll(s => s.Dead);
    }

    internal void HitEnemy(Enemy e, bool rammed = false)
    {
        if (e.Kind == EnemyKind.Commander && e.Hits == 0 && !IsChallenge && !rammed)
        {
            // Commanders take two hits: the first turns them purple.
            e.Hits = 1;
            Play(Sfx.CommanderHurt);
            return;
        }
        e.Dead = true;
        Burst(e.Pos);
        int points = Points(e);
        if (IsChallenge) ChallengeHits++;
        AddScore(points);
        bool diving = e.IsDiving || e.Mode is EnemyMode.Entering or EnemyMode.Homing;

        switch (e.Kind)
        {
            case EnemyKind.Drone: Play(Sfx.DroneHit); break;
            case EnemyKind.Moth: Play(Sfx.MothHit); break;
            case EnemyKind.Rogue: Play(Sfx.RogueHit); break;
            default: Play(Sfx.CommanderDie); break;
        }
        if (points >= 400 || e.Kind == EnemyKind.Rogue) Popup(e.Pos, points);

        // An escort shot down doubles its commander's bonus.
        if (e.Leader is { Dead: false } leader) leader.EscortsShot++;

        if (e.Kind == EnemyKind.Commander)
        {
            foreach (var x in e.Escorts.Where(x => x.Leader == e)) x.Leader = null;
            if (e.HasCaptive)
            {
                if (diving) Rescue(e.Pos + new Vec(0, -16));
                else
                {
                    // Shot in the formation: the captive turns on you and takes the commander's place.
                    Enemies.Add(new Enemy
                    {
                        Kind = EnemyKind.Rogue,
                        Row = -1,
                        Col = e.Col,
                        Pos = e.Pos + new Vec(0, -16),
                        Mode = EnemyMode.Homing,
                    });
                }
            }
        }
    }

    /// <summary>Points for an enemy: more in flight than in the formation; a diving commander's bonus grows with its escorts shot.</summary>
    public int Points(Enemy e)
    {
        if (IsChallenge) return 100;
        bool flying = e.Mode != EnemyMode.InFormation;
        return e.Kind switch
        {
            EnemyKind.Drone => flying ? 100 : 50,
            EnemyKind.Moth => flying ? 160 : 80,
            EnemyKind.Rogue => 1000,
            _ => !flying ? 150 : e.IsDiving ? (e.EscortsShot switch { 0 => 400, 1 => 800, _ => 1600 }) : 400,
        };
    }

    private void LoseFighter(double x)
    {
        Burst(new Vec(x, Field.PlayerY), fighter: true);
        Play(Sfx.FighterDie);
        if (Dual)
        {
            // Half of a dual fighter is lost; the other flies on alone.
            Dual = false;
            PlayerX = x == PlayerX - 8 ? PlayerX + 8 : PlayerX - 8;
            return;
        }
        PlayerAlive = false;
        PlayerShots.Clear();
        Play(Sfx.BeamStop);
        SetPhase(Phase.Respawning);
    }
}
