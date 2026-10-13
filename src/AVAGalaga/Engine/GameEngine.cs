using AVAGalaga.Models;

namespace AVAGalaga.Engine;

/// <summary>The game's screens, from the title through a game to the high-score table.</summary>
public enum GameState { Title, Attract, Playing, Paused, GameOver, EnterInitials, HighScores }

/// <summary>What is happening within a game.</summary>
public enum Phase
{
    /// <summary>"STAGE n", then "READY": the enemies fly in.</summary>
    StageIntro,
    Fighting,
    /// <summary>The fighter is lost: wait for the divers to go home, then the next fighter.</summary>
    Respawning,
    /// <summary>The fighter is being pulled up into a commander's beam.</summary>
    Captured,
    /// <summary>A rescued fighter is coming down to join the one in play.</summary>
    Docking,
    /// <summary>After a challenge stage: the hits and the bonus.</summary>
    ChallengeResult,
    /// <summary>A moment's pause between stages.</summary>
    StageClear,
}

/// <summary>The four controls, as the engine sees them each step.</summary>
public record struct Controls(bool Left, bool Right, bool Fire, bool Start = false);

/// <summary>Sounds the engine asks for; the host decides how they sound.</summary>
public enum Sfx
{
    Fire, DroneHit, MothHit, CommanderHurt, CommanderDie, FighterDie, Dive, BeamStart, BeamStop, Captured,
    Rescued, StageStart, ChallengeStart, Perfect, ExtraLife, GameOver, Badge, RogueHit,
}

/// <summary>
/// The whole game, with no UI: call <see cref="Update"/> with the elapsed time and the controls, and
/// draw what it exposes. It runs in fixed 60 Hz steps, so it plays the same at any frame rate, and
/// a given seed always plays the same way.
/// </summary>
public sealed partial class GameEngine
{
    public const int StartingReserve = 2;
    public const int FirstExtraLife = 20000;
    public const int ExtraLifeEvery = 70000;
    public const double TitleIdleSeconds = 15;
    public const double AttractSeconds = 45;
    public const double HighScoreSeconds = 9;

    private readonly Random _rng;
    private double _accumulator;
    private bool _firePrev;
    private bool _startPrev;

    public GameEngine(int seed = 1981, HighScoreTable? table = null)
    {
        _rng = new Random(seed);
        HighScores = table ?? HighScoreTable.Load();
        SeedStars();
    }

    public event Action<Sfx>? Sound;

    public GameState State { get; private set; } = GameState.Title;
    public Phase Phase { get; private set; } = Phase.StageIntro;
    public double PhaseTime { get; private set; }
    public double StateTime { get; private set; }
    public HighScoreTable HighScores { get; }
    public InitialsEntry? Initials { get; private set; }
    /// <summary>The place in the table just earned (flashes on the high-score screen), or −1.</summary>
    public int NewEntryIndex { get; private set; } = -1;

    public int Score { get; private set; }
    public int Stage { get; private set; }
    public int Reserve { get; private set; }
    private int _nextExtraLife;

    public bool IsChallenge => IsChallengeStage(Stage);
    public static bool IsChallengeStage(int stage) => stage >= 3 && (stage - 3) % 4 == 0;

    /// <summary>Counts every stage played, challenge or not (they all earn a flag).</summary>
    public int ShotsFired { get; private set; }
    public int Hits { get; private set; }

    // The fighter.
    public double PlayerX { get; private set; } = Field.CenterX;
    public bool PlayerAlive { get; private set; }
    public bool Dual { get; private set; }

    public Formation Formation { get; } = new();
    public List<Enemy> Enemies { get; } = [];
    public List<Shot> PlayerShots { get; } = [];
    public List<Shot> EnemyShots { get; } = [];
    public List<Effect> Effects { get; } = [];
    public List<Star> Stars { get; } = [];

    /// <summary>A message across the middle of the playfield ("STAGE 1", "READY", "FIGHTER CAPTURED").</summary>
    public string Banner { get; private set; } = "";
    public string Banner2 { get; private set; } = "";

    // Capture and rescue animations.
    public Vec? CaptiveFlight { get; private set; }
    public double CaptiveSpin { get; private set; }
    public Enemy? Captor { get; private set; }

    // Challenge stage tallies.
    public int ChallengeHits { get; private set; }
    public int ChallengeBonus { get; private set; }

    public bool Silent { get; set; }

    /// <summary>The attract-mode pilot, while the demonstration plays.</summary>
    public AttractPilot? Pilot { get; private set; }

    private void Play(Sfx s)
    {
        if (!Silent && State != GameState.Attract) Sound?.Invoke(s);
    }

    /// <summary>Advances the game by real time; the work is done in fixed steps.</summary>
    public void Update(double dt, Controls controls)
    {
        _accumulator += Math.Min(dt, 0.25);
        while (_accumulator >= Field.Step)
        {
            _accumulator -= Field.Step;
            Step(controls);
        }
    }

    /// <summary>One 1/60 s step.</summary>
    public void Step(Controls controls)
    {
        double dt = Field.Step;
        StateTime += dt;
        bool firePressed = controls.Fire && !_firePrev;
        bool startPressed = controls.Start && !_startPrev;
        _firePrev = controls.Fire;
        _startPrev = controls.Start;
        UpdateStars(dt);

        switch (State)
        {
            case GameState.Title:
                Formation.Update(dt);
                if (startPressed || firePressed) StartGame();
                else if (StateTime > TitleIdleSeconds) StartAttract();
                break;
            case GameState.Attract:
                var pilot = Pilot!.Decide(this);
                Simulate(dt, pilot, pilot.Fire && !_pilotFirePrev);
                _pilotFirePrev = pilot.Fire;
                if (startPressed || firePressed) { StartGame(); break; }
                if (StateTime > AttractSeconds || State == GameState.GameOver) ShowHighScores(-1);
                break;
            case GameState.Playing:
                Simulate(dt, controls, firePressed);
                break;
            case GameState.Paused:
                break;
            case GameState.GameOver:
                UpdateEffects(dt);
                Formation.Update(dt);
                if (StateTime > 3)
                {
                    if (HighScores.Qualifies(Score))
                    {
                        State = GameState.EnterInitials;
                        StateTime = 0;
                        Initials = new InitialsEntry(HighScores.LastInitials);
                    }
                    else ShowHighScores(-1);
                }
                break;
            case GameState.EnterInitials:
                if (Initials is { IsDone: true } done)
                {
                    int place = HighScores.Insert(done.Initials, Score, Stage);
                    HighScores.Save();
                    ShowHighScores(place);
                }
                break;
            case GameState.HighScores:
                if (startPressed || firePressed) StartGame();
                else if (StateTime > HighScoreSeconds) ReturnToTitle();
                break;
        }
    }

    private bool _pilotFirePrev;

    public void StartGame()
    {
        Pilot = null;
        BeginGame(GameState.Playing);
    }

    private void StartAttract()
    {
        Pilot = new AttractPilot(_rng.Next());
        BeginGame(GameState.Attract);
    }

    private void BeginGame(GameState state)
    {
        State = state;
        StateTime = 0;
        Score = 0;
        Stage = 0;
        Reserve = StartingReserve;
        _nextExtraLife = FirstExtraLife;
        ShotsFired = 0;
        Hits = 0;
        Dual = false;
        Captor = null;
        CaptiveFlight = null;
        NewEntryIndex = -1;
        Effects.Clear();
        NextStage();
    }

    public void ReturnToTitle()
    {
        State = GameState.Title;
        StateTime = 0;
        Pilot = null;
        Enemies.Clear();
        PlayerShots.Clear();
        EnemyShots.Clear();
        Effects.Clear();
        Banner = Banner2 = "";
        Formation.Reset();
    }

    private void ShowHighScores(int place)
    {
        State = GameState.HighScores;
        StateTime = 0;
        NewEntryIndex = place;
        Pilot = null;
        Enemies.Clear();
        PlayerShots.Clear();
        EnemyShots.Clear();
        Banner = Banner2 = "";
    }

    public void Pause()
    {
        if (State == GameState.Playing) { State = GameState.Paused; Play(Sfx.BeamStop); }
    }

    public void Resume()
    {
        if (State == GameState.Paused) State = GameState.Playing;
    }

    private void GameOver()
    {
        Play(Sfx.GameOver);
        Play(Sfx.BeamStop);
        if (State == GameState.Attract)
        {
            ShowHighScores(-1);
            return;
        }
        State = GameState.GameOver;
        StateTime = 0;
        Banner = "GAME OVER";
        Banner2 = "";
    }

    private void SetPhase(Phase phase)
    {
        Phase = phase;
        PhaseTime = 0;
    }

    internal void AddScore(int points)
    {
        Score += points;
        while (Score >= _nextExtraLife)
        {
            Reserve = Math.Min(Reserve + 1, 8);
            _nextExtraLife = _nextExtraLife == FirstExtraLife ? FirstExtraLife + ExtraLifeEvery : _nextExtraLife + ExtraLifeEvery;
            Play(Sfx.ExtraLife);
        }
    }

    /// <summary>Hit ratio, as shown after a game.</summary>
    public double HitRatio => ShotsFired == 0 ? 0 : 100.0 * Hits / ShotsFired;

    // ----- stars -------------------------------------------------------------------------------

    private static readonly uint[] StarColors = [0xFFFFFFFF, 0xFFFF5050, 0xFF50A0FF, 0xFFFFE050, 0xFF60FF80, 0xFFFF80FF, 0xFF80FFFF];

    private void SeedStars()
    {
        for (int i = 0; i < 70; i++)
        {
            Stars.Add(new Star
            {
                X = _rng.NextDouble() * Field.Width,
                Y = _rng.NextDouble() * Field.Height,
                Speed = 18 + _rng.NextDouble() * 34,
                Color = StarColors[_rng.Next(StarColors.Length)],
                Phase = _rng.NextDouble(),
                Blink = 0.6 + _rng.NextDouble() * 1.6,
            });
        }
    }

    /// <summary>The stars drift down, and stop while the fighter is captured, as on the arcade.</summary>
    private void UpdateStars(double dt)
    {
        bool still = State is GameState.Playing or GameState.Attract && Phase is Phase.Captured;
        foreach (var s in Stars)
        {
            s.Phase = (s.Phase + dt / s.Blink) % 1;
            if (still) continue;
            s.Y += s.Speed * dt;
            if (s.Y > Field.Height)
            {
                s.Y -= Field.Height;
                s.X = _rng.NextDouble() * Field.Width;
            }
        }
    }

    // ----- effects -----------------------------------------------------------------------------

    private void UpdateEffects(double dt)
    {
        foreach (var e in Effects) e.Age += dt;
        Effects.RemoveAll(e => e.Done);
    }

    private void Burst(Vec at, bool fighter = false) =>
        Effects.Add(new Effect { Kind = fighter ? EffectKind.FighterBurst : EffectKind.EnemyBurst, Pos = at, Life = fighter ? 1.0 : 0.45 });

    private void Popup(Vec at, int points) =>
        Effects.Add(new Effect { Kind = EffectKind.Score, Pos = at, Life = 1.2, Text = points.ToString() });
}
