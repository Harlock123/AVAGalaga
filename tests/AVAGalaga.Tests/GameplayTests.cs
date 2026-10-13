using AVAGalaga.Engine;
using AVAGalaga.Models;
using AVAGalaga.Views;

namespace AVAGalaga.Tests;

public sealed class GameplayTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "galaga-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private GameEngine NewGame(int seed = 1)
    {
        var g = new GameEngine(seed, new HighScoreTable(_dir));
        g.StartGame();
        return g;
    }

    /// <summary>Plays with no input until the formation is complete.</summary>
    private static void UntilFormed(GameEngine g)
    {
        for (int i = 0; i < 60 * 40 && !g.Formation.Breathing; i++) g.Step(default);
        Assert.True(g.Formation.Breathing);
    }

    [Fact]
    public void A_formation_has_forty_places()
    {
        var places = Formation.Places().ToList();
        Assert.Equal(40, places.Count);
        Assert.Equal(4, places.Count(p => p.Kind == EnemyKind.Commander));
        Assert.Equal(16, places.Count(p => p.Kind == EnemyKind.Moth));
        Assert.Equal(20, places.Count(p => p.Kind == EnemyKind.Drone));
        Assert.Equal(40, places.Select(p => (p.Row, p.Col)).Distinct().Count());
    }

    [Fact]
    public void The_first_stage_flies_in_forty_enemies_to_forty_places()
    {
        var g = NewGame();
        Assert.Equal("STAGE 1", g.Banner);
        UntilFormed(g);
        Assert.Equal(40, g.Enemies.Count);
        Assert.All(g.Enemies, e => Assert.True(e.Mode == EnemyMode.InFormation || e.IsDiving, $"{e.Kind} is {e.Mode}"));
        var expected = Formation.Places().Select(p => (p.Kind, p.Row, p.Col)).OrderBy(p => p).ToList();
        Assert.Equal(expected, g.Enemies.Select(e => (e.Kind, e.Row, e.Col)).OrderBy(p => p).ToList());
    }

    [Fact]
    public void Every_fourth_stage_from_the_third_is_a_challenge()
    {
        int[] challenges = [3, 7, 11, 15, 19];
        for (int s = 1; s <= 20; s++)
            Assert.Equal(challenges.Contains(s), GameEngine.IsChallengeStage(s));
    }

    [Fact]
    public void Enemies_are_worth_more_in_flight_and_commanders_more_with_escorts_down()
    {
        var g = NewGame();
        UntilFormed(g);
        Enemy Of(EnemyKind k) => g.Enemies.First(e => e.Kind == k && e.Mode == EnemyMode.InFormation);
        var drone = Of(EnemyKind.Drone);
        var moth = Of(EnemyKind.Moth);
        var commander = Of(EnemyKind.Commander);
        Assert.Equal(50, g.Points(drone));
        Assert.Equal(80, g.Points(moth));
        Assert.Equal(150, g.Points(commander));
        drone.Mode = EnemyMode.Diving;
        moth.Mode = EnemyMode.Diving;
        commander.Mode = EnemyMode.Diving;
        Assert.Equal(100, g.Points(drone));
        Assert.Equal(160, g.Points(moth));
        Assert.Equal(400, g.Points(commander));
        commander.EscortsShot = 1;
        Assert.Equal(800, g.Points(commander));
        commander.EscortsShot = 2;
        Assert.Equal(1600, g.Points(commander));
    }

    [Fact]
    public void A_commander_takes_two_hits()
    {
        var g = NewGame();
        UntilFormed(g);
        var c = g.Enemies.First(e => e.Kind == EnemyKind.Commander);
        int before = g.Score;
        g.HitEnemy(c);
        Assert.False(c.Dead);
        Assert.True(c.Hurt);
        Assert.Equal(before, g.Score);
        g.HitEnemy(c);
        Assert.True(c.Dead);
        Assert.Equal(before + 150, g.Score);
    }

    [Fact]
    public void Shooting_a_commander_in_formation_turns_its_captive_against_you()
    {
        var g = NewGame();
        UntilFormed(g);
        var c = g.Enemies.First(e => e.Kind == EnemyKind.Commander && e.Mode == EnemyMode.InFormation);
        c.HasCaptive = true;
        c.Hits = 1;
        g.HitEnemy(c);
        var rogue = Assert.Single(g.Enemies, e => e.Kind == EnemyKind.Rogue);
        Assert.Equal(-1, rogue.Row);
        Assert.Equal(1000, g.Points(rogue));
    }

    [Fact]
    public void Bonus_fighters_come_at_twenty_thousand_and_every_seventy_thousand()
    {
        var g = NewGame();
        int reserve = g.Reserve;
        g.AddScore(19990);
        Assert.Equal(reserve, g.Reserve);
        g.AddScore(20);
        Assert.Equal(reserve + 1, g.Reserve);
        g.AddScore(69980);
        Assert.Equal(reserve + 1, g.Reserve);
        g.AddScore(10);
        Assert.Equal(reserve + 2, g.Reserve);
    }

    [Fact]
    public void Fire_is_one_shot_per_press_and_two_in_the_air_at_most()
    {
        var g = NewGame();
        for (int i = 0; i < 200; i++) g.Step(default); // through the intro
        g.Step(new Controls(false, false, true));
        g.Step(new Controls(false, false, true)); // held: no second shot
        Assert.Single(g.PlayerShots);
        g.Step(default);
        g.Step(new Controls(false, false, true));
        g.Step(default);
        g.Step(new Controls(false, false, true));
        Assert.Equal(2, g.PlayerShots.Count);
        g.MakeDual();
        g.Step(default);
        g.Step(new Controls(false, false, true));
        Assert.Equal(4, g.PlayerShots.Count);
    }

    [Fact]
    public void The_same_seed_plays_the_same_game()
    {
        string Run()
        {
            var g = NewGame(seed: 21);
            var pilot = new AttractPilot(5);
            for (int i = 0; i < 60 * 90; i++) g.Step(pilot.Decide(g));
            return $"{g.Stage} {g.Score} {g.Reserve} {g.Hits} {g.ShotsFired} {g.Enemies.Count}";
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void The_demonstration_pilot_clears_stages_and_the_game_ends_in_the_table()
    {
        var g = NewGame(seed: 3);
        var pilot = new AttractPilot(8);
        for (int i = 0; i < 60 * 60 * 3 && g.State == GameState.Playing; i++) g.Step(pilot.Decide(g));
        Assert.True(g.Stage >= 4, $"reached stage {g.Stage}");
        Assert.True(g.Score > 8000);
        // Stop dodging: the game ends, and the score goes in the table.
        for (int i = 0; i < 60 * 60 * 10 && g.State == GameState.Playing; i++) g.Step(default);
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(60 * 4);
        Assert.Equal(GameState.EnterInitials, g.State);
        g.Initials!.Finish();
        g.Idle(2);
        Assert.Equal(GameState.HighScores, g.State);
        Assert.True(File.Exists(Path.Combine(_dir, "highscores.txt")));
        Assert.Equal(g.Score, HighScoreTable.Load(_dir).TopScore);
    }

    [Fact]
    public void The_title_runs_the_demonstration_then_the_high_scores()
    {
        var g = new GameEngine(5, new HighScoreTable(_dir));
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + 1)));
        Assert.Equal(GameState.Attract, g.State);
        Assert.NotNull(g.Pilot);
        g.Idle((int)(60 * (GameEngine.AttractSeconds + 1)));
        Assert.Equal(GameState.HighScores, g.State);
        g.Idle((int)(60 * (GameEngine.HighScoreSeconds + 1)));
        Assert.Equal(GameState.Title, g.State);
    }

    [Fact]
    public void A_high_score_table_keeps_the_best_ten()
    {
        var t = HighScoreTable.Load(_dir);
        Assert.Equal(HighScoreTable.Size, t.Entries.Count);
        Assert.False(t.Qualifies(100));
        Assert.Equal(0, t.Insert("ZAP", 999999, 12));
        t.Save();
        var again = HighScoreTable.Load(_dir);
        Assert.Equal("ZAP", again.Entries[0].Initials);
        Assert.Equal(12, again.Entries[0].Stage);
        Assert.Equal(HighScoreTable.Size, again.Entries.Count);
    }

    [Fact]
    public void Stage_flags_count_up_like_the_arcade()
    {
        Assert.Equal([0], GameCanvas.Badges(1));
        Assert.Equal([1], GameCanvas.Badges(5));
        Assert.Equal([3, 1, 0, 0], GameCanvas.Badges(27));
        Assert.Equal([5, 5, 2, 1, 0], GameCanvas.Badges(116));
        Assert.Equal([4, 1, 0], GameCanvas.Badges(36));
    }

    [Fact]
    public void Every_sound_is_there()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        string[] files = ["Fire", "DroneHit", "MothHit", "CommanderHurt", "CommanderDie", "FighterDie", "Dive", "Beam",
            "Captured", "Rescued", "ExtraLife", "Badge", "StageStart", "Challenge", "Perfect", "GameOver"];
        Assert.All(files, f => Assert.True(File.Exists(Path.Combine(folder, f + ".wav")), f));
    }
}
