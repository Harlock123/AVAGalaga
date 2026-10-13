using AVAGalaga.Models;

namespace AVAGalaga.Engine;

public sealed partial class GameEngine
{
    /// <summary>An enemy waiting to fly in: when, which path, and where it goes.</summary>
    private sealed record Arrival(double At, EnemyKind Kind, int Row, int Col, FlightPath Path, int Wave);

    private readonly List<Arrival> _arrivals = [];
    private double _waveClock;
    private double _diveClock;

    /// <summary>The stage number counting only ordinary stages (1, 2, 4→3, ...): it sets the difficulty.</summary>
    public int Level => Stage - (Stage + 1) / 4;

    private void NextStage()
    {
        Stage++;
        Enemies.Clear();
        PlayerShots.Clear();
        EnemyShots.Clear();
        _arrivals.Clear();
        Formation.Reset();
        _waveClock = 0;
        _diveClock = 0;
        ChallengeHits = 0;
        ChallengeBonus = 0;
        PlayerAlive = true;
        PlayerX = Math.Clamp(PlayerX, Field.PlayerMinX + (Dual ? 8 : 0), Field.PlayerMaxX - (Dual ? 8 : 0));
        if (IsChallenge) PlanChallenge();
        else PlanStage();
        SetPhase(Phase.StageIntro);
        Banner = IsChallenge ? "CHALLENGING STAGE" : $"STAGE {Stage}";
        Banner2 = "";
        Play(IsChallenge ? Sfx.ChallengeStart : Sfx.StageStart);
        Play(Sfx.Badge);
    }

    /// <summary>
    /// Forty enemies in five waves of eight. Each wave flies one path (or a mirrored pair) in single
    /// file, and each enemy knows its place in the formation.
    /// </summary>
    private void PlanStage()
    {
        // Variety from stage to stage: the waves' paths change as the stages go by.
        var variant = (Stage - 1) % 3;
        FlightPath Path(int kind, bool right) => ((kind + variant) % 3) switch
        {
            0 => PathLibrary.TopLoop(right),
            1 => PathLibrary.SideSweep(right),
            _ => PathLibrary.CornerS(right),
        };
        const double gap = 0.13;
        double t = 0;
        // Wave 1: four drones and four moths, side by side down the middle.
        for (int i = 0; i < 4; i++)
        {
            Add(t + i * gap, EnemyKind.Drone, 3 + i / 2, 4 + i % 2, Path(0, false), 0);
            Add(t + i * gap, EnemyKind.Moth, 1 + i / 2, 4 + i % 2, Path(0, true), 0);
        }
        // Wave 2: the commanders, each with a moth.
        t += 3.2;
        int[] mothCols = [3, 6, 3, 6];
        for (int i = 0; i < 4; i++)
        {
            Add(t + i * 2 * gap, EnemyKind.Commander, 0, 3 + i, Path(1, false), 1);
            Add(t + (i * 2 + 1) * gap, EnemyKind.Moth, 1 + i / 2, mothCols[i], Path(1, false), 1);
        }
        // Wave 3: eight moths from the other side.
        t += 3.2;
        int[] cols3 = [1, 2, 7, 8];
        for (int i = 0; i < 8; i++)
            Add(t + i * gap, EnemyKind.Moth, 1 + i / 4, cols3[i % 4], Path(1, true), 2);
        // Waves 4 and 5: the drones on each flank.
        t += 3.2;
        for (int i = 0; i < 8; i++)
            Add(t + i * gap, EnemyKind.Drone, 3 + i / 4, 6 + i % 4, Path(2, true), 3);
        t += 3.2;
        for (int i = 0; i < 8; i++)
            Add(t + i * gap, EnemyKind.Drone, 3 + i / 4, i % 4, Path(2, false), 4);
    }

    /// <summary>A challenge stage: five waves of eight looping through without shooting, never landing.</summary>
    private void PlanChallenge()
    {
        int round = (Stage - 3) / 4;
        EnemyKind[] kinds = [EnemyKind.Drone, EnemyKind.Moth, EnemyKind.Commander, EnemyKind.Moth, EnemyKind.Drone];
        for (int w = 0; w < 5; w++)
        {
            int pattern = w + round;
            var kind = kinds[(w + round) % kinds.Length];
            for (int i = 0; i < 4; i++)
            {
                Add(w * 3.4 + i * 0.16, kind, -1, 0, PathLibrary.Challenge(pattern, false), w);
                Add(w * 3.4 + i * 0.16, kind, -1, 0, PathLibrary.Challenge(pattern, true), w);
            }
        }
    }

    private void Add(double at, EnemyKind kind, int row, int col, FlightPath path, int wave) =>
        _arrivals.Add(new Arrival(at, kind, row, col, path, wave));

    /// <summary>The game while it is being played (or demonstrated).</summary>
    private void Simulate(double dt, Controls controls, bool firePressed)
    {
        PhaseTime += dt;
        Formation.Update(dt);
        UpdateEffects(dt);

        switch (Phase)
        {
            case Phase.StageIntro:
                if (PhaseTime > 1.6) Banner = IsChallenge ? "CHALLENGING STAGE" : "READY";
                if (PhaseTime > 2.6)
                {
                    Banner = "";
                    SetPhase(Phase.Fighting);
                }
                MovePlayer(dt, controls);
                break;
            case Phase.Fighting:
                MovePlayer(dt, controls);
                if (firePressed) Fire();
                LaunchArrivals(dt);
                if (!IsChallenge) ScheduleDives(dt);
                break;
            case Phase.Respawning:
                LaunchArrivals(dt);
                if (PhaseTime > 2.5 && !Enemies.Any(e => e.IsDiving))
                {
                    if (Reserve == 0)
                    {
                        GameOver();
                        return;
                    }
                    Reserve--;
                    PlayerAlive = true;
                    PlayerX = Field.CenterX;
                    Banner = "READY";
                    SetPhase(Phase.StageIntro);
                    PhaseTime = 1.6;
                }
                break;
            case Phase.Captured:
                UpdateCapture(dt);
                break;
            case Phase.Docking:
                UpdateDocking(dt);
                break;
            case Phase.ChallengeResult:
                if (PhaseTime > 4.5)
                {
                    Banner = Banner2 = "";
                    NextStage();
                    return;
                }
                break;
            case Phase.StageClear:
                MovePlayer(dt, controls);
                if (PhaseTime > 1.2)
                {
                    NextStage();
                    return;
                }
                break;
        }

        UpdateEnemies(dt);
        UpdateShots(dt);
        if (State is not (GameState.Playing or GameState.Attract)) return;
        Collide();
        CheckStageOver();
    }

    private void LaunchArrivals(double dt)
    {
        _waveClock += dt;
        for (int i = _arrivals.Count - 1; i >= 0; i--)
        {
            var a = _arrivals[i];
            if (a.At > _waveClock) continue;
            _arrivals.RemoveAt(i);
            var e = new Enemy
            {
                Kind = a.Kind,
                Row = a.Row,
                Col = a.Col,
                Wave = a.Wave,
                Path = a.Path,
                Pos = a.Path.Points[0],
                Mode = IsChallenge ? EnemyMode.Passing : EnemyMode.Entering,
                Speed = IsChallenge ? 150 : 135 + Math.Min(40, Level * 3),
            };
            // From the second stage the enemies shoot as they fly in.
            if (!IsChallenge && Level >= 2 && _rng.NextDouble() < Math.Min(0.5, 0.08 * Level))
            {
                e.ShotsLeft = 1;
                e.NextShot = 0.6 + _rng.NextDouble();
            }
            Enemies.Add(e);
        }
        // When everyone is in place the formation starts to breathe and the dives begin.
        if (_arrivals.Count == 0 && !Formation.Breathing && Enemies.Count > 0
            && Enemies.All(e => e.Mode is EnemyMode.InFormation or EnemyMode.Passing || e.IsDiving))
            Formation.Breathing = true;
    }

    private void CheckStageOver()
    {
        if (Phase is not (Phase.Fighting or Phase.Respawning)) return;
        if (_arrivals.Count > 0 || Enemies.Count > 0 || Captor is not null) return;
        if (!PlayerAlive && Phase == Phase.Respawning) return;
        if (IsChallenge)
        {
            ChallengeBonus = ChallengeHits == 40 ? 10000 : ChallengeHits * 100;
            AddScore(ChallengeBonus);
            Banner = ChallengeHits == 40 ? "PERFECT !" : "NUMBER OF HITS";
            Banner2 = ChallengeHits == 40 ? $"SPECIAL BONUS {ChallengeBonus}" : $"{ChallengeHits}     BONUS {ChallengeBonus}";
            Play(ChallengeHits == 40 ? Sfx.Perfect : Sfx.StageStart);
            SetPhase(Phase.ChallengeResult);
        }
        else SetPhase(Phase.StageClear);
    }
}
