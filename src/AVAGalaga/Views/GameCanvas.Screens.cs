using Avalonia;
using Avalonia.Media;
using AVAGalaga.Engine;
using AVAGalaga.Models;

namespace AVAGalaga.Views;

public sealed partial class GameCanvas
{
    private static readonly double RightPanel = FieldLeft + Field.Width * Scale;

    /// <summary>The panels either side: scores on the left; stage, reserve fighters and flags on the right.</summary>
    private void DrawPanels(DrawingContext dc)
    {
        var g = Engine;
        double lx = FieldLeft / 2, rx = RightPanel + (ScreenWidth - RightPanel) / 2;
        bool playing = g.State is GameState.Playing or GameState.Paused or GameState.GameOver or GameState.EnterInitials or GameState.Attract;

        Text(dc, "HIGH SCORE", new Point(lx, 60), 30, Red, center: true);
        Text(dc, Math.Max(g.HighScores.TopScore, g.Score).ToString(), new Point(lx, 92), 32, White, center: true);
        if (g.State != GameState.Title || _clock % 1 < 0.6)
            Text(dc, "1UP", new Point(lx, 160), 30, Red, center: true);
        Text(dc, (playing ? g.Score : 0).ToString("00"), new Point(lx, 192), 32, White, center: true);

        if (playing)
        {
            Text(dc, "STAGE", new Point(rx, 60), 30, Red, center: true);
            Text(dc, g.Stage.ToString(), new Point(rx, 92), 32, White, center: true);
            // Reserve fighters.
            for (int i = 0; i < Math.Min(g.Reserve, 6); i++)
                DrawPanelSprite(dc, Sprites.Fighter, rx - 50 + (i % 3) * 50, 170 + (i / 3) * 46, 2.6);
            // Stage flags.
            var flags = Badges(g.Stage);
            for (int i = 0; i < flags.Count; i++)
                DrawPanelSprite(dc, Sprites.Badges[flags[i]], rx - 60 + (i % 6) * 22, 700 - (i / 6) * 34, 4);
        }
        Text(dc, "AVAGALAGA", new Point(rx, 560), 26, Dim, center: true);
        Text(dc, "M mute  P pause", new Point(rx, 590), 20, Dim, center: true);
        Text(dc, "Ctrl+S screenshot", new Point(rx, 612), 20, Dim, center: true);
    }

    /// <summary>The flags for a stage: 50s, 30s, 20s, 10s, 5s and 1s, largest first.</summary>
    public static List<int> Badges(int stage)
    {
        var flags = new List<int>();
        int[] values = [50, 30, 20, 10, 5, 1];
        int[] kinds = [5, 4, 3, 2, 1, 0];
        int left = stage;
        for (int i = 0; i < values.Length; i++)
            while (left >= values[i]) { flags.Add(kinds[i]); left -= values[i]; }
        return flags;
    }

    private static void DrawPanelSprite(DrawingContext dc, Sprites.Sprite sprite, double x, double y, double px)
    {
        using (dc.PushTransform(Matrix.CreateTranslation(-sprite.Width / 2.0, -sprite.Height / 2.0) * Matrix.CreateScale(px, px) * Matrix.CreateTranslation(x, y)))
        {
            for (int r = 0; r < sprite.Height; r++)
                for (int c = 0; c < sprite.Width; c++)
                {
                    char ch = sprite.Rows[r][c];
                    if (ch != '.') dc.FillRectangle(Brush(Sprites.Palette[ch]), new Rect(c, r, 1.05, 1.05));
                }
        }
    }

    /// <summary>The title: the name, what each enemy is worth, and how to start.</summary>
    private void DrawTitle(DrawingContext dc)
    {
        Text(dc, "AVAGALAGA", P(Field.CenterX, 40), 80, Yellow, center: true);
        Text(dc, "AFTER THE 1981 ARCADE CLASSIC", P(Field.CenterX, 58), 22, Dim, center: true);
        Text(dc, "-- SCORE --", P(Field.CenterX, 84), 30, Cyan, center: true);
        Text(dc, "IN FORMATION   DIVING", P(Field.CenterX + 18, 98), 22, Dim, center: true);
        Row(Sprites.Drone[Flap], 112, "50", "100");
        Row(Sprites.Moth[Flap], 130, "80", "160");
        Row(Sprites.Commander[Flap], 148, "150", "400-1600");
        Row(Sprites.Captive, 166, "", "1000");
        Text(dc, "TWO HITS TO DOWN A COMMANDER.", P(Field.CenterX, 184), 22, White, center: true);
        Text(dc, "LET ONE CAPTURE YOUR FIGHTER, THEN SHOOT", P(Field.CenterX, 194), 22, White, center: true);
        Text(dc, "IT AS IT DIVES TO WIN A DUAL FIGHTER.", P(Field.CenterX, 203), 22, White, center: true);
        Text(dc, $"BONUS FIGHTER AT {GameEngine.FirstExtraLife}", P(Field.CenterX, 220), 24, Yellow, center: true);
        Text(dc, $"AND EVERY {GameEngine.ExtraLifeEvery} PTS", P(Field.CenterX, 229), 24, Yellow, center: true);
        if (_clock % 1 < 0.65)
            Text(dc, "PRESS FIRE OR ENTER TO START", P(Field.CenterX, 248), 28, White, center: true);
        Text(dc, "ARROWS MOVE   SPACE FIRES   ESC QUITS", P(Field.CenterX, 262), 20, Dim, center: true);
        Text(dc, "© 2026 LONNIE WATSON", P(Field.CenterX, 278), 20, Dim, center: true);

        void Row(Sprites.Sprite s, double y, string still, string moving)
        {
            DrawSprite(dc, s, new Vec(Field.CenterX - 60, y), 0);
            Text(dc, still, P(Field.CenterX + 2, y), 26, White, center: true);
            Text(dc, moving, P(Field.CenterX + 62, y), 26, White, center: true);
        }
    }

    private void DrawHighScores(DrawingContext dc)
    {
        Text(dc, "THE GALACTIC HEROES", P(Field.CenterX, 40), 34, Red, center: true);
        Text(dc, "SCORE        NAME   STAGE", P(Field.CenterX, 62), 24, Dim, center: true);
        var table = Engine.HighScores.Entries;
        for (int i = 0; i < table.Count; i++)
        {
            var e = table[i];
            bool fresh = i == Engine.NewEntryIndex;
            if (fresh && _clock % 0.5 > 0.35) continue;
            var brush = fresh ? Yellow : i == 0 ? Cyan : White;
            string place = i switch { 0 => "1ST", 1 => "2ND", 2 => "3RD", _ => $"{i + 1}TH" };
            Text(dc, $"{place,-4} {e.Score,8}   {e.Initials}   {e.Stage,3}", P(Field.CenterX, 82 + i * 17), 28, brush, center: true);
        }
        if (_clock % 1 < 0.65)
            Text(dc, "PRESS FIRE TO PLAY", P(Field.CenterX, 262), 28, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var entry = Engine.Initials;
        if (entry is null) return;
        Text(dc, "ENTER YOUR INITIALS !", P(Field.CenterX, 110), 32, Red, center: true);
        Text(dc, $"SCORE {Engine.Score}", P(Field.CenterX, 124), 28, White, center: true);
        for (int i = 0; i < HighScoreTable.InitialsLength; i++)
        {
            var at = P(Field.CenterX - 16 + i * 16, 146);
            bool cursor = i == entry.Cursor;
            Text(dc, entry.Initials[i].ToString(), at, 56, cursor ? Yellow : White, center: true);
            if (cursor && _clock % 0.6 < 0.4)
                dc.FillRectangle(Yellow, new Rect(at.X - 14, at.Y + 26, 28, 4));
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE.  ENTER WHEN DONE", P(Field.CenterX, 172), 20, Dim, center: true);
    }
}
