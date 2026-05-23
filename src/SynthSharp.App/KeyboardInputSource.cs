using SynthSharp.Input;

namespace SynthSharp.App;

public sealed partial class KeyboardInputSource : IKeyboardInputSource
{
    private bool _started;

    public event EventHandler<string>? KeyPressed;
    public event EventHandler<string>? KeyReleased;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        AttachPlatformHooks();
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        DetachPlatformHooks();
    }

    private void RaiseKeyPressed(string keyToken)
    {
        KeyPressed?.Invoke(this, keyToken);
    }

    private void RaiseKeyReleased(string keyToken)
    {
        KeyReleased?.Invoke(this, keyToken);
    }

    partial void AttachPlatformHooks();

    partial void DetachPlatformHooks();
}
