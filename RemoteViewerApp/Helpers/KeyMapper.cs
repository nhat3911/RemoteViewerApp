using System.Windows.Forms;

namespace RemoteViewerApp.Helpers;

/// <summary>
/// Chuyển đổi WinForms Keys enum sang chuỗi Key/Code mà server KeyboardEventDto yêu cầu.
/// Server dùng Key (tên key) và Code (mã vật lý), tương tự Web KeyboardEvent.
/// </summary>
public static class KeyMapper
{
    /// <summary>
    /// Trả về (Key, Code) tương ứng với WinForms Keys value.
    /// Key = tên phím (e.g. "Enter", "A", "F5")
    /// Code = mã vật lý (e.g. "Enter", "KeyA", "F5")
    /// </summary>
    public static (string Key, string Code) Map(Keys keyCode)
    {
        // Tách modifier keys ra khỏi keyCode chính
        var key = keyCode & Keys.KeyCode;

        return key switch
        {
            // ── Chữ cái ──────────────────────────────────────────
            Keys.A => ("a", "KeyA"), Keys.B => ("b", "KeyB"),
            Keys.C => ("c", "KeyC"), Keys.D => ("d", "KeyD"),
            Keys.E => ("e", "KeyE"), Keys.F => ("f", "KeyF"),
            Keys.G => ("g", "KeyG"), Keys.H => ("h", "KeyH"),
            Keys.I => ("i", "KeyI"), Keys.J => ("j", "KeyJ"),
            Keys.K => ("k", "KeyK"), Keys.L => ("l", "KeyL"),
            Keys.M => ("m", "KeyM"), Keys.N => ("n", "KeyN"),
            Keys.O => ("o", "KeyO"), Keys.P => ("p", "KeyP"),
            Keys.Q => ("q", "KeyQ"), Keys.R => ("r", "KeyR"),
            Keys.S => ("s", "KeyS"), Keys.T => ("t", "KeyT"),
            Keys.U => ("u", "KeyU"), Keys.V => ("v", "KeyV"),
            Keys.W => ("w", "KeyW"), Keys.X => ("x", "KeyX"),
            Keys.Y => ("y", "KeyY"), Keys.Z => ("z", "KeyZ"),

            // ── Số hàng trên ──────────────────────────────────────
            Keys.D0 => ("0", "Digit0"), Keys.D1 => ("1", "Digit1"),
            Keys.D2 => ("2", "Digit2"), Keys.D3 => ("3", "Digit3"),
            Keys.D4 => ("4", "Digit4"), Keys.D5 => ("5", "Digit5"),
            Keys.D6 => ("6", "Digit6"), Keys.D7 => ("7", "Digit7"),
            Keys.D8 => ("8", "Digit8"), Keys.D9 => ("9", "Digit9"),

            // ── Numpad ────────────────────────────────────────────
            Keys.NumPad0 => ("0", "Numpad0"), Keys.NumPad1 => ("1", "Numpad1"),
            Keys.NumPad2 => ("2", "Numpad2"), Keys.NumPad3 => ("3", "Numpad3"),
            Keys.NumPad4 => ("4", "Numpad4"), Keys.NumPad5 => ("5", "Numpad5"),
            Keys.NumPad6 => ("6", "Numpad6"), Keys.NumPad7 => ("7", "Numpad7"),
            Keys.NumPad8 => ("8", "Numpad8"), Keys.NumPad9 => ("9", "Numpad9"),
            Keys.Multiply  => ("*",  "NumpadMultiply"),
            Keys.Add       => ("+",  "NumpadAdd"),
            Keys.Subtract  => ("-",  "NumpadSubtract"),
            Keys.Decimal   => (".",  "NumpadDecimal"),
            Keys.Divide    => ("/",  "NumpadDivide"),

            // ── Phím điều hướng ───────────────────────────────────
            Keys.Left  => ("ArrowLeft",  "ArrowLeft"),
            Keys.Right => ("ArrowRight", "ArrowRight"),
            Keys.Up    => ("ArrowUp",    "ArrowUp"),
            Keys.Down  => ("ArrowDown",  "ArrowDown"),
            Keys.Home  => ("Home",  "Home"),
            Keys.End   => ("End",   "End"),
            Keys.Prior => ("PageUp",   "PageUp"),
            Keys.Next  => ("PageDown", "PageDown"),
            Keys.Insert=> ("Insert", "Insert"),
            Keys.Delete=> ("Delete", "Delete"),

            // ── Phím đặc biệt ─────────────────────────────────────
            Keys.Enter      => ("Enter",     "Enter"),
            Keys.Escape     => ("Escape",    "Escape"),
            Keys.Space      => (" ",         "Space"),
            Keys.Tab        => ("Tab",       "Tab"),
            Keys.Back       => ("Backspace", "Backspace"),
            Keys.CapsLock   => ("CapsLock",  "CapsLock"),
            Keys.NumLock    => ("NumLock",   "NumLock"),
            Keys.Scroll     => ("ScrollLock","ScrollLock"),
            Keys.Pause      => ("Pause",     "Pause"),
            Keys.PrintScreen=> ("PrintScreen","PrintScreen"),

            // ── Modifier (WinForms cũng gửi riêng) ───────────────
            Keys.LControlKey or Keys.RControlKey or Keys.ControlKey
                            => ("Control", "ControlLeft"),
            Keys.LShiftKey  or Keys.RShiftKey or Keys.ShiftKey
                            => ("Shift",   "ShiftLeft"),
            Keys.LMenu      or Keys.RMenu or Keys.Menu
                            => ("Alt",     "AltLeft"),
            Keys.LWin       or Keys.RWin
                            => ("Meta",    "MetaLeft"),

            // ── Function keys ─────────────────────────────────────
            Keys.F1  => ("F1",  "F1"),  Keys.F2  => ("F2",  "F2"),
            Keys.F3  => ("F3",  "F3"),  Keys.F4  => ("F4",  "F4"),
            Keys.F5  => ("F5",  "F5"),  Keys.F6  => ("F6",  "F6"),
            Keys.F7  => ("F7",  "F7"),  Keys.F8  => ("F8",  "F8"),
            Keys.F9  => ("F9",  "F9"),  Keys.F10 => ("F10", "F10"),
            Keys.F11 => ("F11", "F11"), Keys.F12 => ("F12", "F12"),

            // ── Dấu câu / ký tự đặc biệt ─────────────────────────
            Keys.OemMinus      => ("-", "Minus"),
            Keys.Oemplus       => ("=", "Equal"),
            Keys.OemOpenBrackets => ("[", "BracketLeft"),
            Keys.OemCloseBrackets => ("]", "BracketRight"),
            Keys.OemSemicolon  => (";", "Semicolon"),
            Keys.OemQuotes     => ("'", "Quote"),
            Keys.Oemcomma      => (",", "Comma"),
            Keys.OemPeriod     => (".", "Period"),
            Keys.OemQuestion   => ("/", "Slash"),
            Keys.OemBackslash  => ("\\", "Backslash"),
            Keys.Oemtilde      => ("`", "Backquote"),

            _ => (key.ToString(), key.ToString())
        };
    }
}
