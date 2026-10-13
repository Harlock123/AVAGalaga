namespace AVAGalaga.Models;

/// <summary>
/// The playfield is the arcade's portrait screen, 224 wide by 288 tall, with y growing downwards.
/// The view scales it up and puts the score panels either side.
/// </summary>
public static class Field
{
    public const double Width = 224;
    public const double Height = 288;
    public const double CenterX = Width / 2;

    /// <summary>Where the fighter flies, and how far it can go either way.</summary>
    public const double PlayerY = 264;
    public const double PlayerMinX = 10;
    public const double PlayerMaxX = Width - 10;

    /// <summary>The fixed step the game runs at: 60 updates a second, like the arcade.</summary>
    public const double Step = 1.0 / 60;
}

/// <summary>A 2D point or vector.</summary>
public readonly record struct Vec(double X, double Y)
{
    public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec operator *(Vec a, double k) => new(a.X * k, a.Y * k);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public Vec Normalized => Length < 1e-9 ? new Vec(0, 0) : this * (1 / Length);
    public static Vec FromAngle(double radians) => new(Math.Cos(radians), Math.Sin(radians));
    public double Angle => Math.Atan2(Y, X);
    public Vec MirrorX => new(Field.Width - X, Y);

    // The derived properties are Vecs too, so the record's own ToString would recurse forever.
    public override string ToString() => $"({X:0.#}, {Y:0.#})";
}
