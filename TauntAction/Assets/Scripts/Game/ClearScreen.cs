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
    GUIStyle big, small, hint;

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
        if (big == null)
        {
            big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 72, fontStyle = FontStyle.Bold };
            big.normal.textColor = new Color(1f, 0.85f, 0.35f);
            small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 26 };
            small.normal.textColor = Color.white;
            hint = new GUIStyle(small) { fontSize = 20 };
            hint.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
        }

        int t = Mathf.FloorToInt(RunStats.PlayTime);
        float w = Screen.width, h = Screen.height;
        GUI.Label(new Rect(0f, h * 0.12f, w, 100f), "ALL SEALS RESTORED", big);
        GUI.Label(new Rect(0f, h * 0.12f + 95f, w, 40f), $"Time {t / 60:00}:{t % 60:00}     Deaths {RunStats.Deaths}", small);
        GUI.Label(new Rect(0f, h * 0.88f, w, 40f), "R / Enter  Play again", hint);
    }
}
