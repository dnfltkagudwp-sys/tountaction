using UnityEngine;

/// <summary>Top-left controls reminder.</summary>
public class DebugHud : MonoBehaviour
{
    [SerializeField] bool show = true;
    [SerializeField] Vector2 position = new Vector2(10f, 10f);

    const string Controls =
        "WASD  Move\n" +
        "Space  Dash\n" +
        "Left click  Taunt\n" +
        "R  Retry room\n" +
        "Shift+R  Restart from room 1";

    GUIStyle style;

    void OnGUI()
    {
        if (!show) return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            style.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
        }
        GUI.Label(new Rect(position.x, position.y, 300f, 110f), Controls, style);
    }
}
