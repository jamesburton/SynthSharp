using Microsoft.Maui.Platform;

namespace SynthSharp.App;

public sealed partial class KeyboardInputSource
{
#if WINDOWS
    private Microsoft.UI.Xaml.FrameworkElement? _keyboardElement;
    private readonly HashSet<string> _heldKeys = new(StringComparer.OrdinalIgnoreCase);
    private Microsoft.Maui.Controls.Window? _appWindow;
    private EventHandler? _activatedHandler;
    private Microsoft.UI.Xaml.Input.KeyEventHandler? _keyDownHandler;
    private Microsoft.UI.Xaml.Input.KeyEventHandler? _keyUpHandler;
#endif

    partial void AttachPlatformHooks()
    {
#if WINDOWS
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var appWindow = Application.Current?.Windows.FirstOrDefault();
            if (appWindow is null)
            {
                return;
            }

            _appWindow = appWindow;

            // If the native handler is already wired up, attach immediately. Otherwise
            // wait for Window.Activated, which fires after the WinUI handler is ready
            // and after the MAUI page's OnAppearing — this is the most reliable
            // attachment point. Subscribing to Activated unconditionally would race
            // if the window has already been activated by the time Start() runs.
            if (TryAttachKeyboardHandlers(appWindow))
            {
                return;
            }

            _activatedHandler = (_, _) =>
            {
                if (TryAttachKeyboardHandlers(appWindow) && _appWindow is not null && _activatedHandler is not null)
                {
                    _appWindow.Activated -= _activatedHandler;
                    _activatedHandler = null;
                }
            };
            appWindow.Activated += _activatedHandler;
        });
#endif
    }

    partial void DetachPlatformHooks()
    {
#if WINDOWS
        if (_appWindow is not null && _activatedHandler is not null)
        {
            _appWindow.Activated -= _activatedHandler;
        }

        _activatedHandler = null;
        _appWindow = null;

        if (_keyboardElement is null)
        {
            _heldKeys.Clear();
            return;
        }

        // Routed-event handlers added via AddHandler must be removed via RemoveHandler;
        // -= would do nothing because the delegates were boxed into KeyEventHandler instances.
        if (_keyDownHandler is not null)
        {
            _keyboardElement.RemoveHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent, _keyDownHandler);
            _keyDownHandler = null;
        }

        if (_keyUpHandler is not null)
        {
            _keyboardElement.RemoveHandler(Microsoft.UI.Xaml.UIElement.KeyUpEvent, _keyUpHandler);
            _keyUpHandler = null;
        }

        _keyboardElement = null;
        _heldKeys.Clear();
#endif
    }

#if WINDOWS
    private bool TryAttachKeyboardHandlers(Microsoft.Maui.Controls.Window appWindow)
    {
        if (_keyboardElement is not null)
        {
            return true; // already attached
        }

        if (appWindow.Handler?.PlatformView is not MauiWinUIWindow nativeWindow
            || nativeWindow.Content is not Microsoft.UI.Xaml.FrameworkElement keyboardElement)
        {
            return false;
        }

        // Use AddHandler with handledEventsToo: true so we still receive KeyDown / KeyUp
        // when focused child controls (Buttons, Pickers, Entries) mark the event Handled.
        // Without this flag, focusing a Picker or Entry would silently swallow our hotkeys.
        _keyDownHandler = OnNativeWindowKeyDown;
        _keyUpHandler = OnNativeWindowKeyUp;

        keyboardElement.AddHandler(
            Microsoft.UI.Xaml.UIElement.KeyDownEvent,
            _keyDownHandler,
            handledEventsToo: true);
        keyboardElement.AddHandler(
            Microsoft.UI.Xaml.UIElement.KeyUpEvent,
            _keyUpHandler,
            handledEventsToo: true);

        _keyboardElement = keyboardElement;
        return true;
    }
#endif

#if WINDOWS
    private void OnNativeWindowKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
    {
        if (IsTextInputFocused(args.OriginalSource))
        {
            // Special-case: Escape releases focus so the user can play pads again without clicking.
            if (args.Key == Windows.System.VirtualKey.Escape && sender is Microsoft.UI.Xaml.FrameworkElement root)
            {
                root.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                args.Handled = true;
            }

            // Don't raise — the text input owns this keystroke.
            return;
        }

        var token = ToToken(args.Key);
        if (token.Length > 0 && _heldKeys.Add(token))
        {
            RaiseKeyPressed(token);
        }
    }

    private void OnNativeWindowKeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
    {
        // Always attempt to remove from _heldKeys and raise KeyReleased, even when focus is on a text
        // input. This handles the edge case where a key is pressed (pad voice starts), the user then
        // clicks a TextBox (focus moves), and releases the key — without this the KeyDown suppression
        // would leave the voice sustained indefinitely.
        var token = ToToken(args.Key);
        if (token.Length > 0 && _heldKeys.Remove(token))
        {
            RaiseKeyReleased(token);
        }
    }

    /// <summary>Returns <see langword="true"/> when <paramref name="originalSource"/> is a control that accepts text input.</summary>
    /// <remarks>
    /// When the focused element is a text-input control, keystrokes should not trigger pad voices.
    /// An editable <see cref="Microsoft.UI.Xaml.Controls.ComboBox"/> is included because it accepts
    /// free-text entry; a non-editable ComboBox (used by the MAUI Picker) is excluded.
    /// </remarks>
    /// <param name="originalSource">The <see cref="Microsoft.UI.Xaml.Input.KeyRoutedEventArgs.OriginalSource"/> of the routed key event.</param>
    private static bool IsTextInputFocused(object? originalSource) => originalSource switch
    {
        Microsoft.UI.Xaml.Controls.TextBox => true,
        Microsoft.UI.Xaml.Controls.RichEditBox => true,
        Microsoft.UI.Xaml.Controls.PasswordBox => true,
        Microsoft.UI.Xaml.Controls.AutoSuggestBox => true,
        // ComboBox in editable mode is also text-input-like. The MAUI Picker maps to a non-editable
        // ComboBox, so this only fires when someone explicitly enables editing on a ComboBox.
        Microsoft.UI.Xaml.Controls.ComboBox cb when cb.IsEditable => true,
        _ => false,
    };

    private static string ToToken(Windows.System.VirtualKey key)
    {
        return key switch
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
    }
#endif
}
