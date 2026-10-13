using AVAGalaga.Models;

namespace AVAGalaga.Engine;

public sealed partial class GameEngine
{
    public const double BeamTime = 4.2;
    public const double BeamHalfWidth = 22;

    /// <summary>How long the tractor beam has been out (for drawing it), and the commander using it.</summary>
    public Enemy? Beamer => Enemies.FirstOrDefault(e => e.Mode == EnemyMode.Beaming && !e.Dead);

    /// <summary>0..1: how far down the beam reaches.</summary>
    public static double BeamReach(double t) => t < 1 ? t : t < BeamTime - 1 ? 1 : Math.Max(0, BeamTime - t);

    private double DiveInterval => Math.Max(0.55, 2.3 - 0.14 * Level);
    private int MaxDivers => Math.Min(2 + Level / 2, 7);
    private int ShotsPerDive => Math.Min(1 + Level / 3, 4);
    private double EnemyShotSpeed => 150 + Math.Min(70, Level * 5);
    private double DiveSpeed => 128 + Math.Min(60, Level * 4);

    private void ScheduleDives(double dt)
    {
        if (!Formation.Breathing || !PlayerAlive) return;
        _diveClock += dt;
        int left = Enemies.Count;
        double interval = left <= 4 ? DiveInterval * 0.35 : left <= 10 ? DiveInterval * 0.7 : DiveInterval;
        if (_diveClock < interval) return;
        _diveClock = 0;
        int diving = Enemies.Count(e => e.IsDiving && e.Leader is null);
        if (diving >= MaxDivers) return;
        var ready = Enemies.Where(e => e.Mode == EnemyMode.InFormation).ToList();
        if (ready.Count == 0) return;

        // Commanders lead about one dive in four; otherwise moths and drones, from the edges first.
        var commanders = ready.Where(e => e.Kind == EnemyKind.Commander).ToList();
        Enemy pick;
        if (commanders.Count > 0 && _rng.NextDouble() < 0.28) pick = commanders[_rng.Next(commanders.Count)];
        else
        {
            var others = ready.Where(e => e.Kind != EnemyKind.Commander).ToList();
            if (others.Count == 0) others = ready;
            pick = others[_rng.Next(others.Count)];
        }
        StartDive(pick);
    }

    private void StartDive(Enemy e)
    {
        Play(Sfx.Dive);
        bool leftSide = e.Col < Formation.Columns / 2;
        // Peel off: up and round, out to the side it sits on.
        var start = e.Pos;
        double heading = -90;
        e.Path = new Turtle(start.X, start.Y, heading).Turn(leftSide ? -190 : 190, 13).Forward(6).Build();
        e.PathPos = 0;
        e.Mode = EnemyMode.LeavingFormation;
        e.Speed = DiveSpeed;
        e.Looped = false;
        e.Timer = 0;
        e.EscortsShot = 0;
        e.TargetX = PlayerX + (_rng.NextDouble() - 0.5) * 40;
        e.ShotsLeft = e.Kind == EnemyKind.Commander ? Math.Max(1, ShotsPerDive - 1) : ShotsPerDive;
        e.NextShot = 0.5 + _rng.NextDouble() * 0.6;
        e.Escorts.Clear();
        e.WillBeam = false;

        if (e.Kind == EnemyKind.Commander)
        {
            bool captiveExists = Enemies.Any(x => x.HasCaptive) || Captor is not null || Enemies.Any(x => x.Kind == EnemyKind.Rogue);
            // A commander alone may come down to use its tractor beam on a lone fighter.
            if (!e.HasCaptive && !captiveExists && !Dual && Reserve >= 0 && _rng.NextDouble() < 0.45)
            {
                e.WillBeam = true;
                e.ShotsLeft = 0;
                e.TargetX = Math.Clamp(PlayerX, 30, Field.Width - 30);
            }
            else
            {
                // Otherwise it takes up to two moths from beneath it as escorts.
                var escorts = Enemies
                    .Where(x => x.Kind == EnemyKind.Moth && x.Mode == EnemyMode.InFormation && x.Row == 1 && Math.Abs(x.Col - e.Col) <= 1)
                    .Take(2).ToList();
                foreach (var x in escorts)
                {
                    x.Mode = EnemyMode.LeavingFormation;
                    x.Leader = e;
                    x.EscortOffset = x.Pos - e.Pos;
                    x.Speed = e.Speed;
                    x.ShotsLeft = 1;
                    x.NextShot = 0.8 + _rng.NextDouble() * 0.8;
                    e.Escorts.Add(x);
                }
            }
        }
    }

    private void UpdateEnemies(double dt)
    {
        foreach (var e in Enemies.ToList())
        {
            if (e.Dead) continue;
            e.Timer += dt;
            if (e.Leader is { } leader)
            {
                FollowLeader(e, leader, dt);
                MaybeShoot(e, dt);
                continue;
            }
            switch (e.Mode)
            {
                case EnemyMode.Entering:
                case EnemyMode.Passing:
                    if (FollowPath(e, dt))
                    {
                        if (e.Mode == EnemyMode.Passing) e.Dead = true; // gone off screen: a miss
                        else e.Mode = EnemyMode.Homing;
                    }
                    MaybeShoot(e, dt);
                    break;
                case EnemyMode.Homing:
                case EnemyMode.Returning:
                    Home(e, dt);
                    break;
                case EnemyMode.InFormation:
                    e.Pos = Formation.Slot(e.Row, e.Col);
                    e.Heading = TurnToward(e.Heading, -Math.PI / 2, 8 * dt);
                    break;
                case EnemyMode.LeavingFormation:
                    if (FollowPath(e, dt)) e.Mode = EnemyMode.Diving;
                    break;
                case EnemyMode.Diving:
                    Dive(e, dt);
                    MaybeShoot(e, dt);
                    break;
                case EnemyMode.Beaming:
                    Beam(e, dt);
                    break;
            }
        }
        Enemies.RemoveAll(e => e.Dead);
    }

    /// <summary>Moves along the path; returns true at its end.</summary>
    private static bool FollowPath(Enemy e, double dt)
    {
        var path = e.Path!;
        e.PathPos += e.Speed * dt / 2; // points are 2 px apart
        int i = (int)e.PathPos;
        if (i >= path.Points.Count - 1)
        {
            e.Pos = path.End;
            return true;
        }
        var a = path.Points[i];
        var b = path.Points[i + 1];
        double f = e.PathPos - i;
        var next = a + (b - a) * f;
        var d = b - a;
        if (d.Length > 0.01) e.Heading = d.Angle;
        e.Pos = next;
        return false;
    }

    /// <summary>Flies straight to its place in the formation, and settles into it.</summary>
    private void Home(Enemy e, double dt)
    {
        var slot = Formation.Slot(e.Row, e.Col);
        var d = slot - e.Pos;
        double step = (e.Mode == EnemyMode.Returning ? DiveSpeed : 160) * dt;
        if (d.Length <= step)
        {
            e.Pos = slot;
            e.Mode = EnemyMode.InFormation;
            e.Escorts.Clear();
            return;
        }
        e.Heading = TurnToward(e.Heading, d.Angle, 9 * dt);
        e.Pos += d.Normalized * step;
    }

    /// <summary>The dive: steer down at the fighter, weaving; drones loop once on the way.</summary>
    private void Dive(Enemy e, double dt)
    {
        if (e.Path is not null && e.Looped && e.PathPos < e.Path.Points.Count - 1)
        {
            FollowPath(e, dt);
            return;
        }
        if (e.WillBeam && e.Pos.Y >= 168)
        {
            e.Mode = EnemyMode.Beaming;
            e.Timer = 0;
            Play(Sfx.BeamStart);
            return;
        }
        if (e.Kind == EnemyKind.Drone && !e.Looped && e.Pos.Y > 150 && Level >= 2)
        {
            e.Looped = true;
            bool cw = e.Pos.X < PlayerX;
            var t = new Turtle(e.Pos.X, e.Pos.Y, e.Heading * 180 / Math.PI).Turn(cw ? 300 : -300, 18);
            e.Path = t.Build();
            e.PathPos = 0;
            return;
        }
        double weave = e.Kind == EnemyKind.Moth ? Math.Sin(e.Timer * 3.2) * 34 : e.Kind == EnemyKind.Rogue ? Math.Sin(e.Timer * 2) * 20 : 0;
        if (e.WillBeam) weave = 0;
        // Track the fighter a little while still high up.
        if (e.Pos.Y < 140 && PlayerAlive && !e.WillBeam) e.TargetX += (PlayerX - e.TargetX) * Math.Min(1, dt * 0.8);
        var aim = new Vec(Math.Clamp(e.TargetX + weave, 6, Field.Width - 6), e.Pos.Y + 80);
        e.Heading = TurnToward(e.Heading, (aim - e.Pos).Angle, 3.2 * dt);
        e.Pos += Vec.FromAngle(e.Heading) * (e.Speed * dt);
        if (e.Pos.Y > Field.Height + 16 || e.Pos.X < -24 || e.Pos.X > Field.Width + 24)
            ReturnHome(e);
    }

    /// <summary>Off the bottom: back in from the top, down to its place.</summary>
    private void ReturnHome(Enemy e)
    {
        var slot = Formation.Slot(e.Row, e.Col);
        e.Pos = new Vec(slot.X, -12);
        e.Heading = Math.PI / 2;
        e.Mode = EnemyMode.Returning;
        e.WillBeam = false;
        e.Path = null;
        foreach (var x in e.Escorts.Where(x => !x.Dead && x.Leader == e))
        {
            x.Leader = null;
            x.Pos = new Vec(Formation.Slot(x.Row, x.Col).X, -12);
            x.Heading = Math.PI / 2;
            x.Mode = EnemyMode.Returning;
        }
        e.Escorts.Clear();
    }

    private void FollowLeader(Enemy e, Enemy leader, double dt)
    {
        if (leader.Dead || leader.Mode is EnemyMode.InFormation or EnemyMode.Returning or EnemyMode.Homing)
        {
            // The leader is gone: carry on alone.
            e.Leader = null;
            e.Mode = EnemyMode.Diving;
            e.TargetX = PlayerX;
            return;
        }
        var want = leader.Pos + e.EscortOffset;
        var d = want - e.Pos;
        e.Pos += d * Math.Min(1, dt * 10);
        e.Heading = leader.Heading;
        e.Mode = leader.Mode == EnemyMode.LeavingFormation ? EnemyMode.LeavingFormation : EnemyMode.Diving;
    }

    private void MaybeShoot(Enemy e, double dt)
    {
        if (e.ShotsLeft <= 0 || !PlayerAlive || IsChallenge) return;
        e.NextShot -= dt;
        if (e.NextShot > 0 || e.Pos.Y < 40 || e.Pos.Y > Field.PlayerY - 70) return;
        e.ShotsLeft--;
        e.NextShot = 0.35 + _rng.NextDouble() * 0.5;
        double speed = EnemyShotSpeed;
        double time = (Field.PlayerY - e.Pos.Y) / speed;
        double vx = Math.Clamp((PlayerX - e.Pos.X) / Math.Max(0.3, time), -55, 55);
        EnemyShots.Add(new Shot { Pos = e.Pos + new Vec(0, 6), Velocity = new Vec(vx, speed) });
    }

    // ----- the tractor beam --------------------------------------------------------------------

    private void Beam(Enemy e, double dt)
    {
        e.Heading = TurnToward(e.Heading, Math.PI / 2, 6 * dt);
        double t = e.Timer;
        if (Captor is null && PlayerAlive && Phase == Phase.Fighting && t > 1.0 && t < BeamTime - 1
            && Math.Abs(PlayerX - e.Pos.X) < BeamHalfWidth * BeamReach(t))
        {
            // Caught: the fighter is pulled up, spinning, to sit above the commander.
            Captor = e;
            PlayerAlive = false;
            CaptiveFlight = new Vec(PlayerX, Field.PlayerY);
            CaptiveSpin = 0;
            PlayerShots.Clear();
            Banner = "";
            SetPhase(Phase.Captured);
            Play(Sfx.Captured);
            return;
        }
        if (Captor == e) return; // holding the beam while the capture plays out
        if (t >= BeamTime)
        {
            Play(Sfx.BeamStop);
            e.WillBeam = false;
            e.Mode = EnemyMode.Diving;
            e.TargetX = e.Pos.X;
        }
    }

    private void UpdateCapture(double dt)
    {
        // After the capture: the message, then the next fighter.
        if (Captor is null)
        {
            if (PhaseTime > 3.2)
            {
                Banner = "";
                SetPhase(Phase.Respawning);
                PhaseTime = 2.5;
            }
            return;
        }
        var captor = Captor;
        CaptiveSpin += dt * 9;
        if (PhaseTime < 0.6) return; // a moment in the beam before it lifts
        var target = captor.Pos + new Vec(0, -16);
        var at = CaptiveFlight!.Value;
        var d = target - at;
        if (d.Length > 1.5)
        {
            CaptiveFlight = at + d.Normalized * Math.Min(d.Length, 55 * dt);
            return;
        }
        Banner = "FIGHTER CAPTURED";
        Play(Sfx.BeamStop);
        captor.HasCaptive = true;
        captor.WillBeam = false;
        captor.Mode = EnemyMode.Homing;
        CaptiveFlight = null;
        Captor = null;
        PhaseTime = 0.6;
    }

    /// <summary>A commander holding a captive was shot while diving: the captive comes down to join the fighter.</summary>
    private void Rescue(Vec from)
    {
        Play(Sfx.Rescued);
        if (!PlayerAlive)
        {
            // Nobody to join: it becomes the next fighter instead.
            Reserve++;
            return;
        }
        CaptiveFlight = from;
        CaptiveSpin = 0;
        EnemyShots.Clear();
        SetPhase(Phase.Docking);
    }

    private void UpdateDocking(double dt)
    {
        var at = CaptiveFlight!.Value;
        CaptiveSpin += dt * 7;
        // The fighter slides to make room; the rescued one comes down beside it.
        double side = PlayerX > Field.Width - 30 ? -16 : 16;
        var target = new Vec(PlayerX + side, Field.PlayerY);
        var d = target - at;
        if (d.Length > 1.5)
        {
            CaptiveFlight = at + d.Normalized * Math.Min(d.Length, 110 * dt);
            return;
        }
        CaptiveFlight = null;
        Dual = true;
        PlayerX = Math.Clamp(PlayerX + side / 2, Field.PlayerMinX + 8, Field.PlayerMaxX - 8);
        SetPhase(Phase.Fighting);
    }

    private static double TurnToward(double from, double to, double maxStep)
    {
        double diff = Math.IEEERemainder(to - from, 2 * Math.PI);
        return from + Math.Clamp(diff, -maxStep, maxStep);
    }
}

public sealed partial class GameEngine
{
    // ----- hooks for tests and screenshots -----------------------------------------------------

    /// <summary>Sends an enemy diving now; a commander can be told to use its beam.</summary>
    internal void ForceDive(Enemy e, bool beam = false)
    {
        StartDive(e);
        if (e.Kind == EnemyKind.Commander)
        {
            foreach (var x in e.Escorts) { x.Leader = null; x.Mode = EnemyMode.InFormation; }
            e.Escorts.Clear();
            e.WillBeam = beam;
            if (beam) { e.ShotsLeft = 0; e.TargetX = PlayerX; }
        }
    }

    /// <summary>Jumps to the start of a stage.</summary>
    internal void JumpToStage(int stage)
    {
        Stage = stage - 1;
        NextStage();
    }

    /// <summary>Gives the player a dual fighter.</summary>
    internal void MakeDual() => Dual = true;

    /// <summary>Plays one step with no input.</summary>
    internal void Idle(int steps)
    {
        for (int i = 0; i < steps; i++) Step(default);
    }
}
