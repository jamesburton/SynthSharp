namespace SynthSharp.Input;

public interface IKeyboardInputSource
{
    event EventHandler<string>? KeyPressed;

    void Start();

    void Stop();
}
