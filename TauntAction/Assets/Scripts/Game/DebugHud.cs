using UnityEngine;

/// <summary>Top-left controls reminder.</summary>
public class DebugHud : MonoBehaviour
{
    [SerializeField] bool show = true;
    [SerializeField] Vector2 position = new Vector2(10f, 10f);

    const string Controls =
        "WASD  이동\n" +
        "Space  대시\n" +
        "좌클릭  도발\n" +
        "R  방 다시 하기\n" +
        "Shift+R  처음부터";

    GUIStyle style;

    void OnGUI()
    {
        if (!show) return;
        if (style == null) style = UiFont.Label(15, new Color(1f, 1f, 1f, 0.8f));
        GUI.Label(new Rect(position.x, position.y, 300f, 120f), Controls, style);
    }
}
