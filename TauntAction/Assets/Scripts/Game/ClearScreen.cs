using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>End screen: clear time and deaths, the shaman taunting now and then, R / Enter to play again.</summary>
public class ClearScreen : MonoBehaviour
{
    [SerializeField] string gameScene = "SampleScene";
    [Tooltip("Optional: plays its Taunt state every few seconds.")]
    [SerializeField] Animator showcase;
    [SerializeField] float tauntEvery = 3f;

    float timer = 1f;
    GUIStyle title, subtitle, small, hint;

    void Update()
    {
        if (showcase != null)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = tauntEvery;
                showcase.CrossFadeInFixedTime(CharacterAnimator.TauntState, 0.1f);
            }
        }

        var kb = Keyboard.current;
        if (kb != null && (kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
        {
            RunStats.Reset();
            RoomManager.StartRoomIndex = 0;
            SceneManager.LoadScene(gameScene);
        }
    }

    void OnGUI()
    {
        if (title == null)
        {
            title = UiFont.Label(84, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleCenter, FontStyle.Bold);
            subtitle = UiFont.Label(24, new Color(1f, 0.85f, 0.35f, 0.75f), TextAnchor.MiddleCenter);
            small = UiFont.Label(26, Color.white, TextAnchor.MiddleCenter);
            hint = UiFont.Label(20, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
        }

        int t = Mathf.FloorToInt(RunStats.PlayTime);
        float w = Screen.width, h = Screen.height, y = h * 0.08f;
        GUI.Label(new Rect(0f, y, w, 110f), "이이제귀", title);
        GUI.Label(new Rect(0f, y + 100f, w, 36f), "以夷制鬼  ·  모든 봉인을 되찾았다", subtitle);
        GUI.Label(new Rect(0f, y + 145f, w, 40f), $"클리어 시간 {t / 60:00}:{t % 60:00}      쓰러진 횟수 {RunStats.Deaths}", small);
        GUI.Label(new Rect(0f, h * 0.88f, w, 40f), "R / Enter  다시 하기", hint);
    }
}
