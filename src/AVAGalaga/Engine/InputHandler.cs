using Avalonia.Input;

namespace AVAGalaga.Engine;

/// <summary>Keyboard and gamepad, boiled down to the cabinet's controls: left, right and fire.</summary>
public sealed class InputHandler : IDisposable
{
    private readonly HashSet<Key> _pressedKeys = [];
    private readonly ControllerManager _controller = new();
    private bool _mouseFire;

    public void OnKeyDown(Key key) => _pressedKeys.Add(key);
    public void OnKeyUp(Key key) => _pressedKeys.Remove(key);
    public void OnMouse(bool down) => _mouseFire = down;

    public bool IsKeyPressed(Key key) => _pressedKeys.Contains(key);

    public void Update() => _controller.Update();

    public bool IsControllerConnected => _controller.IsConnected;

    public Controls Controls => new(
        Left: IsKeyPressed(Key.Left) || IsKeyPressed(Key.A) || _controller.DPadLeft || _controller.LeftStickX < -0.4f,
        Right: IsKeyPressed(Key.Right) || IsKeyPressed(Key.D) || _controller.DPadRight || _controller.LeftStickX > 0.4f,
        Fire: IsKeyPressed(Key.Space) || IsKeyPressed(Key.LeftCtrl) || IsKeyPressed(Key.RightCtrl) || IsKeyPressed(Key.Up)
              || IsKeyPressed(Key.W) || _mouseFire || _controller.ButtonA || _controller.ButtonX || _controller.ButtonB || _controller.RightTrigger > 0.3f,
        Start: IsKeyPressed(Key.Enter) || _controller.ButtonStart);

    // Controller-only menu inputs, for initials entry (the keyboard is handled key by key there).
    public bool PadUp => _controller.DPadUp || _controller.LeftStickY < -0.5f;
    public bool PadDown => _controller.DPadDown || _controller.LeftStickY > 0.5f;
    public bool PadLeft => _controller.DPadLeft || _controller.LeftStickX < -0.5f;
    public bool PadRight => _controller.DPadRight || _controller.LeftStickX > 0.5f;
    public bool PadAccept => _controller.ButtonX || _controller.ButtonB || _controller.ButtonA;
    public bool PadDone => _controller.ButtonStart;
    public bool PadPause => _controller.ButtonStart;
    public bool PadBack => _controller.ButtonBack;

    public void Dispose() => _controller.Dispose();
}
