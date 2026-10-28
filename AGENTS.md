# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing the game, its controls and
what the tests check; this file covers how the code is put together and the rules that keep it working.

## What this is

A shoot-'em-up in the style of Namco's 1981 arcade game Galaga, in C# on .NET 10 and Avalonia 12. One
self-contained executable per platform (Windows, macOS and Linux, x64 and ARM). The pixel art is this game's own,
drawn in code (`Models/Sprites.cs`), and the sounds are synthesised by `tools/make_sounds.py`.

## Commands

```bash
dotnet build AVAGalaga.slnx                 # the SDK version is in global.json (10.0.400 or later)
dotnet test AVAGalaga.slnx                  # every test runs headless: no window opens
dotnet run --project src/AVAGalaga          # play it; --no-audio for silence
GALAGA_SHOTS=$PWD/Images dotnet test tests/AVAGalaga.Tests --filter ScreenshotTests   # refresh the README's pictures
python3 tools/make_sounds.py                # rebuild src/AVAGalaga/Sounds (standard-library Python only)
```

To tune the flight paths, write their points out and plot them (`plot_paths.sh` needs ImageMagick's `magick`):

```bash
GALAGA_PATHS=/tmp/paths dotnet test tests/AVAGalaga.Tests --filter PathTests && tools/plot_paths.sh /tmp/paths
```

`Directory.Build.props` sets `TreatWarningsAsErrors`, nullable reference types and implicit usings for every
project: a warning fails the build, so keep it at zero.

## Layout

- `src/AVAGalaga/Engine/`: the game, with no Avalonia drawing in it.
  - `GameEngine.cs` runs the states (title, attract mode, play, high scores), holds `Controls` and runs the
    fixed-step loop.
  - `GameEngine.Stage.cs` builds the stages, ordinary and challenge, and the waves that fly in.
  - `GameEngine.Enemies.cs` is the enemies: diving, firing and the commanders' tractor beam.
  - `GameEngine.Player.cs` is the fighter, its shots, capture and the dual fighter.
  - `Formation.cs` is the ten-by-five formation that sways and breathes.
  - `Paths.cs` draws the flight paths with turtle steps (`FlightPath`, `Turtle`, `PathLibrary`).
  - `Entities.cs` has the enemies, shots, effects and stars.
  - `AttractPilot.cs` is the computer player, used by the attract mode and the tests.
  - `InputHandler.cs` (keys and mouse) and `ControllerManager.cs` (SDL2 gamepads) produce `Controls`.
  - `HighScoreTable.cs`, `InitialsEntry.cs` and `SoundManager.cs` do what their names say.
- `src/AVAGalaga/Models/`: `Geometry.cs` (the playfield measures `Field` and `Vec`) and `Sprites.cs` (the pixel art).
- `src/AVAGalaga/Views/`: `GameCanvas` draws the screen and runs the 60 Hz timer. `GameCanvas.Draw.cs` draws
  play, and `GameCanvas.Screens.cs` the panels, title, high scores and initials entry.
- `src/AVAGalaga/Audio/`: the OpenAL Soft audio engine, shared with the author's other AVA games.
- `tests/AVAGalaga.Tests/`: xUnit v3 with `Avalonia.Headless.XUnit`: gameplay, path and screenshot tests.
- `tools/`: `make_sounds.py` and `plot_paths.sh`.

## Rules that keep it working

- **Fixed steps.** `GameEngine.Update` runs the game in fixed 1/60 s steps (`Field.Step`), independent of the frame
  rate, so a seed always plays the same game. Draw random numbers from the engine's seeded `_rng`
  (`new Random(seed)`), never from `Random.Shared`.
- **Engine first, drawing second.** Gameplay belongs in `Engine/` and `Models/`, where tests can reach it without
  a window. `Views/` only reads the game and draws it.
- **Input goes through `InputHandler`.** `Controls` is `Left`, `Right`, `Fire` and `Start`: the fighter only moves
  sideways, and Up is a fire key. Menus read the pad through `PadUp`/`PadDown` (stick up is `LeftStickY < -0.5`).
  In `Turtle`, 90 degrees is straight down the screen.
- **SDL under one lock.** `ControllerManager` starts, polls and stops SDL only inside `lock (Sdl)`. The tests
  create many handlers in parallel, and unlocked SDL calls corrupt its heap.
- **Audio.** OpenAL Soft through Silk.NET. `Directory.Build.targets` swaps in the official Windows `soft_oal.dll`
  from `Audio/native/`, which needs no Visual C++ runtime. In a virtual machine `OpenAlConfig` writes a larger
  mixing period to `ALSOFT_CONF` so the sound doesn't break up.
- **Tests never touch the player's files.** High scores live in the app-data folder (`AVAGalaga`); tests load the
  table from a fresh folder under the system temp path, and the screenshot tests use a silent `SoundManager`.
- **Line endings.** Keep each file's existing line endings (`Engine/ControllerManager.cs` is CRLF) so diffs show
  only real changes.

## Making a change

- Add or update tests with every gameplay change. `dotnet test` must pass, with no warnings.
- If the change shows on screen, refresh the screenshots with the command above, and look at them.
- Update `README.md` (features, controls, the test list) to match.
- New sounds go into `tools/make_sounds.py`, not as hand-edited WAV files.

## CI and releases

`.github/workflows/build.yml` builds and tests every push. It then publishes self-contained single-file builds for
win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64. Every run keeps the builds as workflow artifacts.
Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag makes a proper release.
