using AVAGalaga.Models;

namespace AVAGalaga.Engine;

public enum EnemyKind { Drone, Moth, Commander, Rogue }

/// <summary>What an enemy is doing.</summary>
public enum EnemyMode
{
    /// <summary>Flying in along its entry path.</summary>
    Entering,
    /// <summary>Flying straight to its place in the formation.</summary>
    Homing,
    /// <summary>In its place, waiting to dive.</summary>
    InFormation,
    /// <summary>Peeling off: the loop out of the formation.</summary>
    LeavingFormation,
    /// <summary>Diving at the fighter.</summary>
    Diving,
    /// <summary>A commander hovering low with its tractor beam out.</summary>
    Beaming,
    /// <summary>Off the bottom and coming back down from the top.</summary>
    Returning,
    /// <summary>A challenge stage flier, which never joins a formation.</summary>
    Passing,
}

public sealed class Enemy
{
    public required EnemyKind Kind { get; init; }
    /// <summary>Formation place: row 0 commanders, rows 1-2 moths, rows 3-4 drones.</summary>
    public int Row { get; init; }
    public int Col { get; init; }
    public Vec Pos { get; set; }
    /// <summary>Direction of travel in radians (−π/2 = up, the way they face in formation).</summary>
    public double Heading { get; set; } = -Math.PI / 2;
    public EnemyMode Mode { get; set; }
    public int Hits { get; set; }
    public FlightPath? Path { get; set; }
    public double PathPos { get; set; }
    public double Speed { get; set; } = 120;
    /// <summary>Seconds before it starts its entry (waves come in a line).</summary>
    public double Delay { get; set; }
    public double Timer { get; set; }
    /// <summary>Where a dive is steering.</summary>
    public double TargetX { get; set; }
    public int ShotsLeft { get; set; }
    public double NextShot { get; set; }
    /// <summary>A diving commander's escorts, and how many of them were shot during the dive.</summary>
    public List<Enemy> Escorts { get; } = [];
    public Enemy? Leader { get; set; }
    public Vec EscortOffset { get; set; }
    public int EscortsShot { get; set; }
    /// <summary>A commander holding a captured fighter above it.</summary>
    public bool HasCaptive { get; set; }
    /// <summary>A commander that will use its tractor beam on this dive.</summary>
    public bool WillBeam { get; set; }
    /// <summary>Has looped once near the bottom on this dive (drones do).</summary>
    public bool Looped { get; set; }
    public bool Dead { get; set; }
    /// <summary>Which wave it entered with (challenge stages score the waves).</summary>
    public int Wave { get; init; }

    public bool IsDiving => Mode is EnemyMode.LeavingFormation or EnemyMode.Diving or EnemyMode.Beaming or EnemyMode.Returning;
    public bool Hurt => Kind == EnemyKind.Commander && Hits > 0;
}

public sealed class Shot
{
    public Vec Pos { get; set; }
    public Vec Velocity { get; init; }
    public bool Dead { get; set; }
}

/// <summary>An explosion, a score popping up, or a message: things that play out and vanish.</summary>
public sealed class Effect
{
    public required EffectKind Kind { get; init; }
    public Vec Pos { get; set; }
    public double Age { get; set; }
    public double Life { get; init; } = 0.5;
    public string Text { get; init; } = "";
    public bool Done => Age >= Life;
}

public enum EffectKind { EnemyBurst, FighterBurst, Score }

public sealed class Star
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Speed { get; init; }
    public uint Color { get; init; }
    public double Phase { get; set; }
    public double Blink { get; init; }
}
