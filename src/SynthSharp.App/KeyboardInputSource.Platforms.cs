using Microsoft.Maui.Platform;

namespace SynthSharp.App;

public sealed partial class KeyboardInputSource
{
#if WINDOWS
    private Microsoft.UI.Xaml.FrameworkElement? _keyboardElement;
#endif

    partial void AttachPlatformHooks()
    {
#if WINDOWS
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var appWindow = Application.Current?.Windows.FirstOrDefault();
            if (appWindow?.Handler?.PlatformView is not MauiWinUIWindow nativeWindow
                || nativeWindow.Content is not Microsoft.UI.Xaml.FrameworkElement keyboardElement)
            {
                return;
            }

            _keyboardElement = keyboardElement;
            _keyboardElement.KeyDown += OnNativeWindowKeyDown;
        });
#endif
    }

    partial void DetachPlatformHooks()
    {
#if WINDOWS
        if (_keyboardElement is null)
        {
            return;
        }

        _keyboardElement.KeyDown -= OnNativeWindowKeyDown;
        _keyboardElement = null;
#endif
    }

#if WINDOWS
    private void OnNativeWindowKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
    {
        var token = args.Key switch
        {
            Windows.System.VirtualKey.Number0 => "0",
            Windows.System.VirtualKey.Number1 => "1",
            Windows.System.VirtualKey.Number2 => "2",
            Windows.System.VirtualKey.Number3 => "3",
            Windows.System.VirtualKey.Number4 => "4",
            Windows.System.VirtualKey.Number5 => "5",
            Windows.System.VirtualKey.Number6 => "6",
            Windows.System.VirtualKey.Number7 => "7",
            Windows.System.VirtualKey.Number8 => "8",
            Windows.System.VirtualKey.Number9 => "9",
            Windows.System.VirtualKey.A => "A",
            Windows.System.VirtualKey.B => "B",
            Windows.System.VirtualKey.C => "C",
            Windows.System.VirtualKey.D => "D",
            Windows.System.VirtualKey.E => "E",
            Windows.System.VirtualKey.F => "F",
            Windows.System.VirtualKey.G => "G",
            Windows.System.VirtualKey.H => "H",
            Windows.System.VirtualKey.I => "I",
            Windows.System.VirtualKey.J => "J",
            Windows.System.VirtualKey.K => "K",
            Windows.System.VirtualKey.L => "L",
            Windows.System.VirtualKey.M => "M",
            Windows.System.VirtualKey.N => "N",
            Windows.System.VirtualKey.O => "O",
            Windows.System.VirtualKey.P => "P",
            Windows.System.VirtualKey.Q => "Q",
            Windows.System.VirtualKey.R => "R",
            Windows.System.VirtualKey.S => "S",
            Windows.System.VirtualKey.T => "T",
            Windows.System.VirtualKey.U => "U",
            Windows.System.VirtualKey.V => "V",
            Windows.System.VirtualKey.W => "W",
            Windows.System.VirtualKey.X => "X",
            Windows.System.VirtualKey.Y => "Y",
            Windows.System.VirtualKey.Z => "Z",
            Windows.System.VirtualKey.Space => "Space",
            Windows.System.VirtualKey.LeftShift => "LeftShift",
            Windows.System.VirtualKey.RightShift => "RightShift",
            Windows.System.VirtualKey.Tab => "Tab",
            _ => string.Empty,
        };

        if (token.Length > 0)
        {
            RaiseKeyPressed(token);
        }
    }
#endif
}
