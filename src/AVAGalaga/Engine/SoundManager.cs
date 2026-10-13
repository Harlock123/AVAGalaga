using AVAGalaga.Audio;

namespace AVAGalaga.Engine;

/// <summary>
/// Plays the game's sounds through OpenAL Soft, which mixes as many at once as the game needs. The
/// tractor beam's hum loops on its own channel; the stage tunes play on the music channel. In a
/// virtual machine a larger mixing buffer keeps the sound from breaking up.
/// </summary>
public sealed class SoundManager : IDisposable
{
    private readonly IAudioEngine _engine;
    private readonly string _folder;

    public SoundManager(bool silent = false)
    {
        _folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        _engine = silent
            ? new NullAudioEngine("silent")
            : OpenAlAudioEngine.CreateOrSilent(AudioBuffer.PeriodFrames(AudioBuffer.Automatic, VirtualMachine.Detect()));
        _engine.SetVolumes(0.9f, 0.8f, 0.7f);
        _engine.SetAmbienceVolume(0.45f);
    }

    public bool IsAvailable => _engine.IsAvailable;

    public bool IsMuted
    {
        get => _muted;
        set
        {
            _muted = value;
            if (value) StopAll();
        }
    }
    private bool _muted;

    public void Play(Sfx sfx)
    {
        if (_muted) return;
        switch (sfx)
        {
            case Sfx.BeamStart: _engine.PlayAmbience(Path.Combine(_folder, "Beam.wav")); return;
            case Sfx.BeamStop: _engine.StopAmbience(); return;
            case Sfx.StageStart: _engine.PlayMusic(Path.Combine(_folder, "StageStart.wav"), loop: false); return;
            case Sfx.ChallengeStart: _engine.PlayMusic(Path.Combine(_folder, "Challenge.wav"), loop: false); return;
            case Sfx.Perfect: _engine.PlayMusic(Path.Combine(_folder, "Perfect.wav"), loop: false); return;
            case Sfx.GameOver: _engine.PlayMusic(Path.Combine(_folder, "GameOver.wav"), loop: false); return;
        }
        string? file = sfx switch
        {
            Sfx.Fire => "Fire.wav",
            Sfx.DroneHit => "DroneHit.wav",
            Sfx.MothHit => "MothHit.wav",
            Sfx.CommanderHurt => "CommanderHurt.wav",
            Sfx.CommanderDie => "CommanderDie.wav",
            Sfx.RogueHit => "CommanderDie.wav",
            Sfx.FighterDie => "FighterDie.wav",
            Sfx.Dive => "Dive.wav",
            Sfx.Captured => "Captured.wav",
            Sfx.Rescued => "Rescued.wav",
            Sfx.ExtraLife => "ExtraLife.wav",
            Sfx.Badge => "Badge.wav",
            _ => null,
        };
        if (file is not null) _engine.PlayEffect(Path.Combine(_folder, file), sfx == Sfx.Fire ? 0.7f : 1f);
    }

    public void StopAll()
    {
        _engine.StopAmbience();
        _engine.StopMusic();
    }

    public void Dispose() => _engine.Dispose();
}
