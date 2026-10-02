using UnityEngine;

/// <summary>
/// Korean-capable font for the IMGUI texts, loaded from the OS at runtime (Windows fonts can't be shipped in a build).
/// Falls back to Unity's default font if none of these is installed.
/// </summary>
public static class UiFont
{
    static readonly string[] Candidates = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR" };
    static Font font;
    static bool tried;

    public static Font Get()
    {
        if (!tried)
        {
            tried = true;
            var installed = Font.GetOSInstalledFontNames();
            foreach (var name in Candidates)
                if (System.Array.IndexOf(installed, name) >= 0)
                {
                    font = Font.CreateDynamicFontFromOSFont(name, 16);
                    break;
                }
        }
        return font;
    }

    /// <summary>A label style using the Korean font.</summary>
    public static GUIStyle Label(int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal)
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor, fontStyle = style };
        var f = Get();
        if (f != null) s.font = f;
        s.normal.textColor = color;
        return s;
    }
}
