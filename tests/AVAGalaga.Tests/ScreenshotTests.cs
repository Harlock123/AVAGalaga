using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVAGalaga.Engine;
using AVAGalaga.Models;
using AVAGalaga.Views;

namespace AVAGalaga.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set GALAGA_SHOTS to a
/// folder to save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "galaga-ui-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed, HighScoreTable.Load(_dir));
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var dir = Environment.GetEnvironmentVariable("GALAGA_SHOTS");
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVAGalaga-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    private static void Play(GameEngine g, AttractPilot pilot, int steps)
    {
        for (int i = 0; i < steps && g.State == GameState.Playing; i++) g.Step(pilot.Decide(g));
    }

    [AvaloniaFact]
    public void The_title_and_the_first_wave()
    {
        var (w, c, g) = Open();
        g.Idle(90);
        Shot(w, c, "title");
        g.StartGame();
        // The intro, then a second and a half into the first wave's flight.
        g.Idle(60 * 4);
        Assert.NotEmpty(g.Enemies);
        Shot(w, c, "entry");
        w.Close();
    }

    [AvaloniaFact]
    public void The_formation_breathes_and_dives()
    {
        var (w, c, g) = Open();
        g.StartGame();
        // Hold fire and dodge, so the whole formation is there to see.
        var pilot = new AttractPilot(5) { Eagerness = 0 };
        for (int i = 0; i < 60 * 40 && !(g.Formation.Breathing && g.Enemies.Count(e => e.IsDiving) >= 2); i++) g.Step(pilot.Decide(g));
        Assert.True(g.Formation.Breathing);
        Play(g, pilot, 25);
        Shot(w, c, "formation");
        w.Close();
    }

    [AvaloniaFact]
    public void The_tractor_beam_captures_and_a_rescue_makes_a_dual_fighter()
    {
        var (w, c, g) = Open();
        g.StartGame();
        for (int i = 0; i < 60 * 30 && !g.Formation.Breathing; i++) g.Step(default);
        var commander = g.Enemies.First(e => e.Kind == EnemyKind.Commander && e.Mode == EnemyMode.InFormation);
        g.ForceDive(commander, beam: true);
        // Stand still under it until the beam is out.
        for (int i = 0; i < 60 * 8 && g.Beamer is null; i++)
        {
            double dx = commander.TargetX - g.PlayerX;
            g.Step(new AVAGalaga.Engine.Controls(dx < -1, dx > 1, false));
        }
        Assert.NotNull(g.Beamer);
        g.Idle(70);
        Shot(w, c, "beam");
        for (int i = 0; i < 60 * 8 && !commander.HasCaptive; i++) g.Step(default);
        Assert.True(commander.HasCaptive);
        g.Idle(60);
        Shot(w, c, "captured");

        // Back in play: the commander dives with its captive; shoot it down to win the fighter back.
        for (int i = 0; i < 60 * 10 && !(g.PlayerAlive && g.Phase == Phase.Fighting); i++) g.Step(default);
        for (int i = 0; i < 60 * 10 && commander.Mode != EnemyMode.InFormation; i++) g.Step(default);
        g.ForceDive(commander);
        commander.Hits = 1;
        for (int i = 0; i < 60 * 12 && !g.Dual; i++)
        {
            bool alive = g.Enemies.Contains(commander);
            double target = alive ? commander.Pos.X : g.PlayerX;
            double dx = target - g.PlayerX;
            bool fire = alive && Math.Abs(dx) < 5 && commander.Pos.Y > 60 && i % 6 == 0;
            g.Step(new AVAGalaga.Engine.Controls(dx < -2, dx > 2, fire));
        }
        Assert.True(g.Dual, "the rescued fighter should dock");
        g.Idle(30);
        Shot(w, c, "dual");
        w.Close();
    }

    [AvaloniaFact]
    public void The_challenging_stage()
    {
        var (w, c, g) = Open();
        g.StartGame();
        g.JumpToStage(3);
        Assert.True(g.IsChallenge);
        // Hold fire for the picture, into the second wave's loops; then shoot for the tally.
        Play(g, new AttractPilot(9) { Eagerness = 0 }, 60 * 7 + 20);
        Assert.Contains(g.Enemies, e => e.Pos.Y is > 20 and < 250);
        Shot(w, c, "challenge");
        var pilot = new AttractPilot(9) { Eagerness = 0.5 };
        for (int i = 0; i < 60 * 30 && g.Phase != Phase.ChallengeResult; i++) g.Step(pilot.Decide(g));
        Assert.Equal(Phase.ChallengeResult, g.Phase);
        g.Idle(30);
        Shot(w, c, "challenge-result");
        w.Close();
    }

    [AvaloniaFact]
    public void Game_over_initials_and_the_high_scores()
    {
        var (w, c, g) = Open(seed: 4);
        g.StartGame();
        var pilot = new AttractPilot(2) { Eagerness = 0.9 };
        for (int i = 0; i < 60 * 60 * 5 && g.Score < 9000; i++) g.Step(pilot.Decide(g));
        // Then stop dodging: the game soon ends.
        for (int i = 0; i < 60 * 60 * 10 && g.State == GameState.Playing; i++) g.Step(default);
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(30);
        Shot(w, c, "game-over");
        g.Idle(60 * 3);
        Assert.Equal(GameState.EnterInitials, g.State);
        g.Initials!.Type('A');
        g.Initials.Type('V');
        Shot(w, c, "initials");
        g.Initials.Type('A');
        g.Idle(5);
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal("AVA", g.HighScores.Entries[g.NewEntryIndex].Initials);
        g.Idle(20);
        Shot(w, c, "high-scores");
        w.Close();
    }
}
