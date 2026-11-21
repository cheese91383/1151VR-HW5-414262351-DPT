using UnityEngine;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 統一讀取鍵盤/滑鼠輸入。
// 不管 Project Settings > Player > Active Input Handling 設成
// 「Input Manager (Old)」、「Input System Package (New)」或「Both」都能用,不用改程式。
public static class GameInput
{
    // WASD / 方向鍵,回傳 (-1~1, -1~1)
    public static Vector2 Move()
    {
        float x = 0f, y = 0f;
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  y -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    y += 1f;
#elif ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) x -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) x += 1f;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) y -= 1f;
            if (k.wKey.isPressed || k.upArrowKey.isPressed) y += 1f;
        }
#endif
        return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }

    // 按住 Shift 跑步
    public static bool RunHeld()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#elif ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && k.shiftKey.isPressed;
#else
        return false;
#endif
    }

    // Esc 取消
    public static bool CancelPressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#elif ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return false;
#endif
    }

    // 空白鍵 或 滑鼠左鍵 是否按住(蓄力用)
    public static bool ThrowHeld()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0);
#elif ENABLE_INPUT_SYSTEM
        bool key = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        bool mouse = Mouse.current != null && Mouse.current.leftButton.isPressed;
        return key || mouse;
#else
        return false;
#endif
    }

    // 空白鍵 或 滑鼠左鍵 丟球(按下的那一格才回傳 true)
    public static bool ThrowPressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
#elif ENABLE_INPUT_SYSTEM
        bool key = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        bool mouse = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        return key || mouse;
#else
        return false;
#endif
    }
}
