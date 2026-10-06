using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>Title screen: game name, controls, the shaman taunting now and then. Enter / Space / click starts a run.</summary>
public class StartScreen : MonoBehaviour
{
    [SerializeField] string gameScene = "SampleScene";
    [Tooltip("Optional: plays its Taunt state every few seconds.")]
    [SerializeField] Animator showcase;
    [SerializeField] float tauntEvery = 3f;

    const string Controls = "WASD 이동     Space 대시     좌클릭 도발";

    float timer = 1f;
    float blink;
    GUIStyle title, subtitle, body, prompt;

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
        blink += Time.deltaTime;

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        bool start = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                  || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        if (start)
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
            title = UiFont.Label(96, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleCenter, FontStyle.Bold);
            subtitle = UiFont.Label(24, new Color(1f, 0.85f, 0.35f, 0.75f), TextAnchor.MiddleCenter);
            body = UiFont.Label(20, new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleCenter);
            prompt = UiFont.Label(26, Color.white, TextAnchor.MiddleCenter);
        }

        float w = Screen.width, h = Screen.height, y = h * 0.08f;
        GUI.Label(new Rect(0f, y, w, 120f), "이이제귀", title);
        GUI.Label(new Rect(0f, y + 110f, w, 36f), "以夷制鬼  ·  귀신으로 귀신을 친다", subtitle);
        GUI.Label(new Rect(0f, h * 0.93f, w, 30f), Controls, body);

        // Slow pulse so the prompt reads as "press something".
        var c = prompt.normal.textColor;
        c.a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(blink * 2.5f));
        prompt.normal.textColor = c;
        GUI.Label(new Rect(0f, h * 0.86f, w, 40f), "Enter / 클릭  시작", prompt);
    }
}
