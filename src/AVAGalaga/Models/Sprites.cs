namespace AVAGalaga.Models;

/// <summary>
/// The game's pixel art: each sprite a grid of colour letters ('.' is see-through), drawn facing up
/// and turned to face the way it flies. These are this game's own drawings, in the manner of the
/// early 80s.
/// </summary>
public static class Sprites
{
    /// <summary>Colour letters to ARGB.</summary>
    public static readonly IReadOnlyDictionary<char, uint> Palette = new Dictionary<char, uint>
    {
        ['W'] = 0xFFF2F2F2, // white
        ['R'] = 0xFFFF2A2A, // red
        ['B'] = 0xFF2E6BFF, // blue
        ['C'] = 0xFF39D8FF, // cyan
        ['Y'] = 0xFFFFE135, // yellow
        ['G'] = 0xFF2BD14F, // green
        ['D'] = 0xFF178A35, // dark green
        ['P'] = 0xFFB85CFF, // purple
        ['O'] = 0xFFFF9A2A, // orange
        ['M'] = 0xFFFF4FD8, // magenta
        ['K'] = 0xFF8A8A8A, // grey
    };

    public sealed record Sprite(string[] Rows)
    {
        public int Width => Rows[0].Length;
        public int Height => Rows.Length;
    }

    private static Sprite S(params string[] rows)
    {
        if (rows.Any(r => r.Length != rows[0].Length))
            throw new ArgumentException("Sprite rows must be the same width.");
        return new Sprite(rows);
    }

    /// <summary>The fighter, white with red fins and a blue canopy.</summary>
    public static readonly Sprite Fighter = S(
        ".......W.......",
        ".......W.......",
        "......WWW......",
        "......WWW......",
        "...R..WWW..R...",
        "...R..WBW..R...",
        "...W.WBBBW.W...",
        "...WWWBBBWWW...",
        "..WWWWWBWWWWW..",
        ".RWWWWWWWWWWWR.",
        ".RWW.WWWWW.WWR.",
        ".R...W.R.W...R.",
        ".....R...R.....");

    /// <summary>A fighter in the tractor beam's grip: the same, in red.</summary>
    public static readonly Sprite Captive = S(
        ".......R.......",
        ".......R.......",
        "......RRR......",
        "......RRR......",
        "...W..RRR..W...",
        "...W..RKR..W...",
        "...R.RKKKR.R...",
        "...RRRKKKRRR...",
        "..RRRRRKRRRRR..",
        ".WRRRRRRRRRRRW.",
        ".WRR.RRRRR.RRW.",
        ".W...R.W.R...W.",
        ".....W...W.....");

    // The drone: a yellow-and-blue wasp. Two frames, wings up and wings down.
    public static readonly Sprite[] Drone =
    [
        S(
            "..B.........B..",
            "..BB.......BB..",
            "...BB.YYY.BB...",
            "....BYRYRYB....",
            ".....YYYYY.....",
            "...BBYRYRYBB...",
            "..BBB.YYY.BBB..",
            ".BBB...Y...BBB.",
            ".BB....Y....BB.",
            "......Y.Y......"),
        S(
            "...............",
            ".....BYYYB.....",
            "...BBYRYRYBB...",
            "..BBBYYYYYBBB..",
            ".BBB.YRYRY.BBB.",
            ".BB...YYY...BB.",
            ".B.....Y.....B.",
            ".......Y.......",
            "......Y.Y......",
            "..............."),
    ];

    // The moth: red and white, broad wings.
    public static readonly Sprite[] Moth =
    [
        S(
            "......C.C......",
            ".......C.......",
            "..RR..RRR..RR..",
            ".RWWR.RRR.RWWR.",
            "RWWWWRRYRRWWWWR",
            "RWWWWRRRRRWWWWR",
            ".RWWR.RYR.RWWR.",
            "..RR..RRR..RR..",
            ".......R.......",
            "......R.R......"),
        S(
            "......C.C......",
            ".......C.......",
            ".......R.......",
            "..RRR.RRR.RRR..",
            ".RWWWRRYRRWWWR.",
            ".RWWWRRRRRWWWR.",
            "..RRR.RYR.RRR..",
            ".......R.......",
            ".......R.......",
            "......R.R......"),
    ];

    // The commander: green, armoured, two hits to kill. Hurt, it turns purple.
    private static readonly string[] CommanderA =
    [
        ".....DGGGD.....",
        "....GGYGYGG....",
        "....GGGGGGG....",
        "..D..GGGGG..D..",
        ".DGD.GGWGG.DGD.",
        "DGGGDGGGGGDGGGD",
        "DGGGGGGGGGGGGGD",
        "DG.GG.GGG.GG.GD",
        ".D..G..G..G..D.",
        ".......G.......",
        "......G.G......",
    ];

    private static readonly string[] CommanderB =
    [
        ".....DGGGD.....",
        "....GGYGYGG....",
        "....GGGGGGG....",
        ".....GGGGG.....",
        "..DGDGGWGGDGD..",
        ".DGGGGGGGGGGGD.",
        "DGGGGGGGGGGGGGD",
        "G..GG.GGG.GG..G",
        "...G...G...G...",
        ".......G.......",
        "......G.G......",
    ];

    public static readonly Sprite[] Commander = [S(CommanderA), S(CommanderB)];

    public static readonly Sprite[] CommanderHurt =
    [
        S([.. CommanderA.Select(r => r.Replace('G', 'P').Replace('D', 'B'))]),
        S([.. CommanderB.Select(r => r.Replace('G', 'P').Replace('D', 'B'))]),
    ];

    /// <summary>The fighter's shot.</summary>
    public static readonly Sprite PlayerShot = S(
        ".W.",
        "RWR",
        "RWR",
        ".R.",
        ".R.");

    /// <summary>An enemy's shot.</summary>
    public static readonly Sprite EnemyShot = S(
        ".Y.",
        "YWY",
        ".R.",
        ".R.");

    /// <summary>Explosion frames: a flash, a burst, scattered sparks.</summary>
    public static readonly Sprite[] Burst =
    [
        S(
            "...............",
            "...............",
            "......Y.Y......",
            ".....YWWWY.....",
            "....YWWWWWY....",
            ".....YWWWY.....",
            "......Y.Y......",
            "..............."),
        S(
            "..R....Y....R..",
            "...R.YYWYY.R...",
            "....YWWWWWY....",
            ".RYWWWYOYWWWYR.",
            "....YWWWWWY....",
            "...R.YYWYY.R...",
            "..R....Y....R..",
            "..............."),
        S(
            "R......Y......R",
            ".O...R...R...O.",
            "...Y...O...Y...",
            ".R..O.....O..R.",
            "...Y...O...Y...",
            ".O...R...R...O.",
            "R......Y......R",
            "..............."),
        S(
            "R......O......R",
            "...............",
            "..O.........O..",
            "R.............R",
            "..O.........O..",
            "...............",
            "R......O......R",
            "..............."),
    ];

    /// <summary>The fighter's explosion: bigger, and red and white.</summary>
    public static readonly Sprite[] FighterBurst =
    [
        S(
            ".....W.R.W.....",
            "...R.WWRWW.R...",
            "..W.RWWWWWR.W..",
            ".RWWWW.R.WWWWR.",
            "..W.RWWWWWR.W..",
            "...R.WWRWW.R...",
            ".....W.R.W.....",
            "..............."),
        S(
            "R...W..R..W...R",
            "..R..WW.WW..R..",
            ".W..R.....R..W.",
            "R.W.........W.R",
            ".W..R.....R..W.",
            "..R..WW.WW..R..",
            "R...W..R..W...R",
            "..............."),
    ];

    /// <summary>The flags beside the score, one kind per stage count (1, 5, 10, 20, 30, 50).</summary>
    public static readonly Sprite[] Badges =
    [
        S("W....", "WRR..", "WRRRR", "WRR..", "W....", "W...."),
        S("W....", "WBB..", "WBBBB", "WBB..", "W....", "W...."),
        S("W....", "WYY..", "WYYYY", "WRR..", "W....", "W...."),
        S("W....", "WGG..", "WGGGG", "WYY..", "W....", "W...."),
        S("W....", "WPP..", "WPPPP", "WCC..", "W....", "W...."),
        S("W....", "WOO..", "WOOOO", "WMM..", "W....", "W...."),
    ];
}
