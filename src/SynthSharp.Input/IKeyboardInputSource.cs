namespace SynthSharp.Input;

public interface IKeyboardInputSource
{
    event EventHandler<string>? KeyPressed;
    event EventHandler<string>? KeyReleased;

    void Start();

    void Stop();
}
