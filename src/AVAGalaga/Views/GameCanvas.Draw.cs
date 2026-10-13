using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVAGalaga.Engine;
using AVAGalaga.Models;

namespace AVAGalaga.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVAGalaga/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
        {
            b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
            BrushCache[argb] = b;
        }
        return b;
    }

    private static readonly IBrush White = Brush(0xFFF2F2F2);
    private static readonly IBrush Red = Brush(0xFFFF3030);
    private static readonly IBrush Yellow = Brush(0xFFFFE135);
    private static readonly IBrush Cyan = Brush(0xFF39D8FF);
    private static readonly IBrush Blue = Brush(0xFF3C6BFF);
    private static readonly IBrush Dim = Brush(0xFF7A7A8A);

    /// <summary>Playfield coordinates to screen coordinates.</summary>
    private static Point P(double x, double y) => new(FieldLeft + x * Scale, y * Scale);
    private static Point P(Vec v) => P(v.X, v.Y);

    private void DrawScreen(DrawingContext dc)
    {
        // The playfield's edge and its stars.
        var field = new Rect(FieldLeft, 0, Field.Width * Scale, Field.Height * Scale);
        using (dc.PushClip(field))
        {
            DrawStars(dc);
            switch (Engine.State)
            {
                case GameState.Title:
                    DrawTitle(dc);
                    break;
                case GameState.HighScores:
                    DrawHighScores(dc);
                    break;
                default:
                    DrawPlay(dc);
                    break;
            }
        }
        dc.DrawRectangle(null, new Pen(Brush(0xFF1A1A30), 2), field);
        DrawPanels(dc);
    }

    private void DrawStars(DrawingContext dc)
    {
        foreach (var s in Engine.Stars)
        {
            if (s.Phase > 0.72) continue; // twinkling: each star is off for part of its cycle
            var p = P(s.X, s.Y);
            dc.FillRectangle(Brush(s.Color), new Rect(p.X, p.Y, Scale * 0.8, Scale * 0.8));
        }
    }

    private int Flap => (int)(_clock * 2.6) % 2;

    private void DrawPlay(DrawingContext dc)
    {
        var g = Engine;
        // The tractor beam goes under everything.
        if (g.Beamer is { } beamer) DrawBeam(dc, beamer);

        foreach (var e in g.Enemies)
        {
            var sprite = e.Kind switch
            {
                EnemyKind.Drone => Sprites.Drone[Flap],
                EnemyKind.Moth => Sprites.Moth[Flap],
                EnemyKind.Commander => e.Hurt ? Sprites.CommanderHurt[Flap] : Sprites.Commander[Flap],
                _ => Sprites.Captive,
            };
            DrawSprite(dc, sprite, e.Pos, e.Heading + Math.PI / 2);
            if (e.HasCaptive) DrawSprite(dc, Sprites.Captive, e.Pos + new Vec(0, -16), e.Heading + Math.PI / 2);
        }

        if (g.CaptiveFlight is { } cf)
            DrawSprite(dc, g.Phase == Phase.Captured ? Sprites.Captive : Sprites.Fighter, cf, g.CaptiveSpin);

        foreach (var x in g.FighterXs)
            DrawSprite(dc, Sprites.Fighter, new Vec(x, Field.PlayerY), 0);

        foreach (var s in g.PlayerShots) DrawSprite(dc, Sprites.PlayerShot, s.Pos, 0);
        foreach (var s in g.EnemyShots) DrawSprite(dc, Sprites.EnemyShot, s.Pos, 0);

        foreach (var fx in g.Effects)
        {
            switch (fx.Kind)
            {
                case EffectKind.EnemyBurst:
                {
                    var frames = Sprites.Burst;
                    int i = Math.Min(frames.Length - 1, (int)(fx.Age / fx.Life * frames.Length));
                    DrawSprite(dc, frames[i], fx.Pos, 0, 1.3);
                    break;
                }
                case EffectKind.FighterBurst:
                {
                    var frames = Sprites.FighterBurst;
                    int i = Math.Min(frames.Length - 1, (int)(fx.Age / fx.Life * frames.Length));
                    DrawSprite(dc, frames[i], fx.Pos, fx.Age * 3, 1.8);
                    break;
                }
                case EffectKind.Score:
                    Text(dc, fx.Text, P(fx.Pos.X, fx.Pos.Y - fx.Age * 6), 24, Cyan, center: true);
                    break;
            }
        }

        if (g.Banner.Length > 0)
            Text(dc, g.Banner, P(Field.CenterX, 140), 34, g.Banner.StartsWith("FIGHTER") || g.Banner.StartsWith("GAME") ? Red : Cyan, center: true);
        if (g.Banner2.Length > 0)
            Text(dc, g.Banner2, P(Field.CenterX, 160), 30, Yellow, center: true);

        switch (g.State)
        {
            case GameState.Paused:
                Text(dc, "PAUSED", P(Field.CenterX, 120), 44, White, center: true);
                Text(dc, "ESC to quit, any other key to resume", P(Field.CenterX, 140), 22, Dim, center: true);
                break;
            case GameState.Attract:
                Text(dc, "DEMONSTRATION", P(Field.CenterX, 200), 26, Red, center: true);
                if (_clock % 1 < 0.6) Text(dc, "PRESS FIRE TO PLAY", P(Field.CenterX, 212), 24, White, center: true);
                break;
            case GameState.GameOver:
                Text(dc, $"SHOTS FIRED {g.ShotsFired}", P(Field.CenterX, 170), 24, Yellow, center: true);
                Text(dc, $"NUMBER OF HITS {g.Hits}", P(Field.CenterX, 180), 24, Yellow, center: true);
                Text(dc, $"HIT-MISS RATIO {g.HitRatio:0.0} %", P(Field.CenterX, 190), 24, White, center: true);
                break;
            case GameState.EnterInitials:
                DrawInitials(dc);
                break;
        }
    }

    /// <summary>The beam: bands of light widening down to the fighter's row, shimmering.</summary>
    private void DrawBeam(DrawingContext dc, Enemy beamer)
    {
        double reach = GameEngine.BeamReach(beamer.Timer);
        double top = beamer.Pos.Y + 8;
        double bottom = top + (Field.PlayerY + 10 - top) * reach;
        int bands = 12;
        for (int i = 0; i < bands; i++)
        {
            double y0 = top + (bottom - top) * i / bands;
            double y1 = top + (bottom - top) * (i + 0.6) / bands;
            double w0 = 4 + GameEngine.BeamHalfWidth * reach * i / bands;
            bool bright = ((int)(beamer.Timer * 12) + i) % 3 == 0;
            var brush = Brush(bright ? 0xC060E0FFu : 0x8030A0FFu);
            var a = P(beamer.Pos.X - w0, y0);
            var b = P(beamer.Pos.X + w0, y1);
            dc.FillRectangle(brush, new Rect(a, b));
        }
    }

    /// <summary>Draws a sprite centred on a playfield point, turned by <paramref name="angle"/> radians.</summary>
    private static void DrawSprite(DrawingContext dc, Sprites.Sprite sprite, Vec at, double angle, double scale = 1)
    {
        double px = Scale * scale;
        var c = P(at);
        var m = Matrix.CreateTranslation(-sprite.Width / 2.0, -sprite.Height / 2.0)
                * Matrix.CreateScale(px, px)
                * Matrix.CreateRotation(angle)
                * Matrix.CreateTranslation(c.X, c.Y);
        using (dc.PushTransform(m))
        {
            for (int y = 0; y < sprite.Height; y++)
            {
                var row = sprite.Rows[y];
                int x = 0;
                while (x < row.Length)
                {
                    char ch = row[x];
                    if (ch == '.') { x++; continue; }
                    int run = 1;
                    while (x + run < row.Length && row[x + run] == ch) run++;
                    // A hair of overlap hides seams between pixels when turned.
                    dc.FillRectangle(Brush(Sprites.Palette[ch]), new Rect(x, y, run + 0.06, 1.06));
                    x += run;
                }
            }
        }
    }

    private static void Text(DrawingContext dc, string text, Point at, double size, IBrush brush, bool center = false, bool right = false)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, brush);
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }
}
