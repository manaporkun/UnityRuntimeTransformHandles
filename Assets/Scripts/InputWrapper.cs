using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

/// <summary>
/// Input abstraction layer that supports both Unity's legacy Input system and the new Input System.
/// Automatically detects which system is available and uses the appropriate API.
/// </summary>
public static class InputWrapper
{
    /// <summary>
    /// Gets the current mouse position in screen coordinates.
    /// </summary>
    public static Vector2 MousePosition
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current?.position.ReadValue() ?? Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }
    }

    /// <summary>
    /// Returns true during the frame the user pressed the given mouse button.
    /// </summary>
    /// <param name="button">0 = left, 1 = right, 2 = middle</param>
    public static bool GetMouseButtonDown(int button)
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null) return false;

        return button switch
        {
            0 => mouse.leftButton.wasPressedThisFrame,
            1 => mouse.rightButton.wasPressedThisFrame,
            2 => mouse.middleButton.wasPressedThisFrame,
            _ => false
        };
#else
        return Input.GetMouseButtonDown(button);
#endif
    }

    /// <summary>
    /// Returns true while the user holds down the given mouse button.
    /// </summary>
    /// <param name="button">0 = left, 1 = right, 2 = middle</param>
    public static bool GetMouseButton(int button)
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null) return false;

        return button switch
        {
            0 => mouse.leftButton.isPressed,
            1 => mouse.rightButton.isPressed,
            2 => mouse.middleButton.isPressed,
            _ => false
        };
#else
        return Input.GetMouseButton(button);
#endif
    }

    /// <summary>
    /// Returns true during the frame the user releases the given mouse button.
    /// </summary>
    /// <param name="button">0 = left, 1 = right, 2 = middle</param>
    public static bool GetMouseButtonUp(int button)
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null) return false;

        return button switch
        {
            0 => mouse.leftButton.wasReleasedThisFrame,
            1 => mouse.rightButton.wasReleasedThisFrame,
            2 => mouse.middleButton.wasReleasedThisFrame,
            _ => false
        };
#else
        return Input.GetMouseButtonUp(button);
#endif
    }

    /// <summary>
    /// Returns true while the user holds down the specified key.
    /// </summary>
    public static bool GetKey(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard == null) return false;

        var control = KeyCodeToKey(key, keyboard);
        return control?.isPressed ?? false;
#else
        return Input.GetKey(key);
#endif
    }

    /// <summary>
    /// Returns true during the frame the user starts pressing the specified key.
    /// </summary>
    public static bool GetKeyDown(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard == null) return false;

        var control = KeyCodeToKey(key, keyboard);
        return control?.wasPressedThisFrame ?? false;
#else
        return Input.GetKeyDown(key);
#endif
    }

    /// <summary>
    /// Returns true during the frame the user releases the specified key.
    /// </summary>
    public static bool GetKeyUp(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard == null) return false;

        var control = KeyCodeToKey(key, keyboard);
        return control?.wasReleasedThisFrame ?? false;
#else
        return Input.GetKeyUp(key);
#endif
    }

    /// <summary>
    /// Returns the value of the virtual axis identified by axisName.
    /// Supports "Mouse X", "Mouse Y", and "Mouse ScrollWheel".
    /// </summary>
    public static float GetAxis(string axisName)
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null) return 0f;

        return axisName switch
        {
            "Mouse X" => mouse.delta.x.ReadValue() * 0.1f,
            "Mouse Y" => mouse.delta.y.ReadValue() * 0.1f,
            "Mouse ScrollWheel" => mouse.scroll.y.ReadValue() * 0.01f,
            "Horizontal" => GetKeyboardAxis(Keyboard.current?.aKey, Keyboard.current?.dKey),
            "Vertical" => GetKeyboardAxis(Keyboard.current?.sKey, Keyboard.current?.wKey),
            _ => 0f
        };
#else
        return Input.GetAxis(axisName);
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static float GetKeyboardAxis(KeyControl negative, KeyControl positive)
    {
        float value = 0f;
        if (negative?.isPressed == true) value -= 1f;
        if (positive?.isPressed == true) value += 1f;
        return value;
    }

    private static KeyControl KeyCodeToKey(KeyCode keyCode, Keyboard keyboard)
    {
        return keyCode switch
        {
            KeyCode.LeftControl => keyboard.leftCtrlKey,
            KeyCode.RightControl => keyboard.rightCtrlKey,
            KeyCode.LeftShift => keyboard.leftShiftKey,
            KeyCode.RightShift => keyboard.rightShiftKey,
            KeyCode.LeftAlt => keyboard.leftAltKey,
            KeyCode.RightAlt => keyboard.rightAltKey,
            KeyCode.Space => keyboard.spaceKey,
            KeyCode.Return => keyboard.enterKey,
            KeyCode.Escape => keyboard.escapeKey,
            KeyCode.Tab => keyboard.tabKey,
            KeyCode.Backspace => keyboard.backspaceKey,
            KeyCode.Delete => keyboard.deleteKey,
            KeyCode.UpArrow => keyboard.upArrowKey,
            KeyCode.DownArrow => keyboard.downArrowKey,
            KeyCode.LeftArrow => keyboard.leftArrowKey,
            KeyCode.RightArrow => keyboard.rightArrowKey,
            KeyCode.A => keyboard.aKey,
            KeyCode.B => keyboard.bKey,
            KeyCode.C => keyboard.cKey,
            KeyCode.D => keyboard.dKey,
            KeyCode.E => keyboard.eKey,
            KeyCode.F => keyboard.fKey,
            KeyCode.G => keyboard.gKey,
            KeyCode.H => keyboard.hKey,
            KeyCode.I => keyboard.iKey,
            KeyCode.J => keyboard.jKey,
            KeyCode.K => keyboard.kKey,
            KeyCode.L => keyboard.lKey,
            KeyCode.M => keyboard.mKey,
            KeyCode.N => keyboard.nKey,
            KeyCode.O => keyboard.oKey,
            KeyCode.P => keyboard.pKey,
            KeyCode.Q => keyboard.qKey,
            KeyCode.R => keyboard.rKey,
            KeyCode.S => keyboard.sKey,
            KeyCode.T => keyboard.tKey,
            KeyCode.U => keyboard.uKey,
            KeyCode.V => keyboard.vKey,
            KeyCode.W => keyboard.wKey,
            KeyCode.X => keyboard.xKey,
            KeyCode.Y => keyboard.yKey,
            KeyCode.Z => keyboard.zKey,
            KeyCode.Alpha0 => keyboard.digit0Key,
            KeyCode.Alpha1 => keyboard.digit1Key,
            KeyCode.Alpha2 => keyboard.digit2Key,
            KeyCode.Alpha3 => keyboard.digit3Key,
            KeyCode.Alpha4 => keyboard.digit4Key,
            KeyCode.Alpha5 => keyboard.digit5Key,
            KeyCode.Alpha6 => keyboard.digit6Key,
            KeyCode.Alpha7 => keyboard.digit7Key,
            KeyCode.Alpha8 => keyboard.digit8Key,
            KeyCode.Alpha9 => keyboard.digit9Key,
            KeyCode.F1 => keyboard.f1Key,
            KeyCode.F2 => keyboard.f2Key,
            KeyCode.F3 => keyboard.f3Key,
            KeyCode.F4 => keyboard.f4Key,
            KeyCode.F5 => keyboard.f5Key,
            KeyCode.F6 => keyboard.f6Key,
            KeyCode.F7 => keyboard.f7Key,
            KeyCode.F8 => keyboard.f8Key,
            KeyCode.F9 => keyboard.f9Key,
            KeyCode.F10 => keyboard.f10Key,
            KeyCode.F11 => keyboard.f11Key,
            KeyCode.F12 => keyboard.f12Key,
            _ => null
        };
    }
#endif
}
