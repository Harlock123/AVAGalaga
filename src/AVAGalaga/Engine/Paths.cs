using AVAGalaga.Models;

namespace AVAGalaga.Engine;

/// <summary>
/// A flight path: points a few pixels apart that an enemy follows at its own speed. Paths are drawn
/// with turtle steps (go forward; turn through an arc), which keeps the loops smooth.
/// </summary>
public sealed class FlightPath
{
    public FlightPath(IReadOnlyList<Vec> points) => Points = points;

    public IReadOnlyList<Vec> Points { get; }
    public Vec End => Points[^1];

    public FlightPath Mirrored() => new([.. Points.Select(p => p.MirrorX)]);

    /// <summary>Cuts the path off once it has crossed the playfield and left it again.</summary>
    public FlightPath TrimmedOffscreen(double margin = 18)
    {
        static bool On(Vec p, double m) => p.X > -m && p.X < Field.Width + m && p.Y > -m && p.Y < Field.Height + m;
        bool entered = false;
        for (int i = 0; i < Points.Count; i++)
        {
            if (On(Points[i], 0)) entered = true;
            else if (entered && !On(Points[i], margin)) return new([.. Points.Take(i + 1)]);
        }
        return this;
    }

    public FlightPath Shifted(double dx, double dy) => new([.. Points.Select(p => new Vec(p.X + dx, p.Y + dy))]);
}

/// <summary>Builds a path by walking and turning, like a turtle. Angles are in degrees; 90 is straight down.</summary>
public sealed class Turtle
{
    private const double StepLength = 2;
    private readonly List<Vec> _points = [];
    private Vec _pos;
    private double _heading;

    public Turtle(double x, double y, double headingDegrees)
    {
        _pos = new Vec(x, y);
        _heading = headingDegrees * Math.PI / 180;
        _points.Add(_pos);
    }

    public Vec Position => _pos;
    public double HeadingDegrees => _heading * 180 / Math.PI;

    public Turtle Forward(double length)
    {
        int steps = Math.Max(1, (int)(length / StepLength));
        var dir = Vec.FromAngle(_heading);
        for (int i = 0; i < steps; i++)
        {
            _pos += dir * (length / steps);
            _points.Add(_pos);
        }
        return this;
    }

    /// <summary>Turns through <paramref name="degrees"/> (positive: clockwise on screen) on a circle of the given radius.</summary>
    public Turtle Turn(double degrees, double radius)
    {
        double sweep = degrees * Math.PI / 180;
        double sign = Math.Sign(sweep);
        // The centre lies to the right of travel for a clockwise turn, to the left otherwise.
        var side = new Vec(-Math.Sin(_heading), Math.Cos(_heading)) * (sign * radius);
        var centre = _pos + side;
        double start = (_pos - centre).Angle;
        int steps = Math.Max(2, (int)(Math.Abs(sweep) * radius / StepLength));
        for (int i = 1; i <= steps; i++)
        {
            double a = start + sweep * i / steps;
            _pos = centre + Vec.FromAngle(a) * radius;
            _points.Add(_pos);
        }
        _heading += sweep;
        return this;
    }

    /// <summary>Heads straight for a point.</summary>
    public Turtle To(double x, double y)
    {
        var d = new Vec(x, y) - _pos;
        _heading = d.Angle;
        return Forward(d.Length);
    }

    public FlightPath Build() => new([.. _points]);
}

/// <summary>The entry and challenge paths, drawn for the left side and mirrored for the right.</summary>
public static class PathLibrary
{
    /// <summary>In from the top, down the middle, then a loop out to the side.</summary>
    public static FlightPath TopLoop(bool right)
    {
        var p = new Turtle(Field.CenterX - 12, -16, 90)
            .Forward(118)
            .Turn(60, 30)
            .Turn(240, 26)
            .Forward(12)
            .Build();
        return right ? p.Mirrored() : p;
    }

    /// <summary>In from the lower side, a wide sweep up and round.</summary>
    public static FlightPath SideSweep(bool right)
    {
        var p = new Turtle(-16, 236, -25)
            .Forward(70)
            .Turn(-150, 46)
            .Turn(-110, 30)
            .Forward(18)
            .Build();
        return right ? p.Mirrored() : p;
    }

    /// <summary>In from a top corner, across and down in an S.</summary>
    public static FlightPath CornerS(bool right)
    {
        var p = new Turtle(40, -16, 70)
            .Forward(60)
            .Turn(70, 40)
            .Turn(-200, 34)
            .Forward(10)
            .Build();
        return right ? p.Mirrored() : p;
    }

    /// <summary>Challenge stage paths: big loops across the screen that leave again.</summary>
    public static FlightPath Challenge(int pattern, bool right)
    {
        var t = (pattern % 5) switch
        {
            0 => new Turtle(Field.CenterX - 20, -16, 90).Forward(120).Turn(-300, 40).Forward(40).Turn(60, 50).Forward(200),
            1 => new Turtle(-16, 220, -30).Forward(80).Turn(-240, 44).Forward(40).Turn(180, 30).Forward(260),
            2 => new Turtle(30, -16, 75).Forward(90).Turn(330, 36).Forward(80).Turn(-90, 60).Forward(220),
            3 => new Turtle(-16, 120, 0).Forward(70).Turn(360, 34).Forward(60).Turn(-45, 60).Forward(240),
            _ => new Turtle(Field.CenterX - 40, -16, 90).Forward(60).Turn(-180, 30).Turn(360, 30).Forward(80).Turn(90, 40).Forward(260),
        };
        var p = t.Build().TrimmedOffscreen();
        return right ? p.Mirrored() : p;
    }
}
