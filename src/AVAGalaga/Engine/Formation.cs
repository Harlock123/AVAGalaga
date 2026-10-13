using AVAGalaga.Models;

namespace AVAGalaga.Engine;

/// <summary>
/// The formation: ten columns by five rows. While the enemies are still flying in it sways from side
/// to side; once they're all in, it breathes, spreading out and drawing in.
/// </summary>
public sealed class Formation
{
    public const int Columns = 10;
    public const double Spacing = 16;
    public const double TopRowY = 46;

    private double _time;

    /// <summary>True once every enemy of the stage has arrived: the formation breathes instead of swaying.</summary>
    public bool Breathing { get; set; }

    public double Sway { get; private set; }
    public double Spread { get; private set; }

    public void Update(double dt)
    {
        _time += dt;
        if (Breathing)
        {
            Sway *= Math.Max(0, 1 - dt * 3);
            Spread = (1 - Math.Cos(_time * Math.PI / 2)) / 2; // 0..1 over four seconds
        }
        else
        {
            Sway = Math.Sin(_time * Math.PI / 2.4) * 10;
            Spread = Math.Max(0, Spread - dt);
        }
    }

    public void Reset()
    {
        _time = 0;
        Breathing = false;
        Sway = 0;
        Spread = 0;
    }

    /// <summary>Where a place in the formation is now. Row −1 is just above the commanders (a captive fighter).</summary>
    public Vec Slot(int row, int col)
    {
        double grow = 1 + 0.18 * Spread;
        double x = Field.CenterX + (col - (Columns - 1) / 2.0) * Spacing * grow + Sway;
        double y = TopRowY + row * Spacing * (1 + 0.12 * Spread);
        return new Vec(x, y);
    }

    /// <summary>The places for one stage: 4 commanders, 16 moths, 20 drones.</summary>
    public static IEnumerable<(EnemyKind Kind, int Row, int Col)> Places()
    {
        for (int c = 3; c <= 6; c++) yield return (EnemyKind.Commander, 0, c);
        for (int r = 1; r <= 2; r++)
            for (int c = 1; c <= 8; c++) yield return (EnemyKind.Moth, r, c);
        for (int r = 3; r <= 4; r++)
            for (int c = 0; c <= 9; c++) yield return (EnemyKind.Drone, r, c);
    }
}
