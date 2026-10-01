using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Minimal win/lose + quick restart for playtesting.
/// CLEAR when every enemy is dead, FAIL when the player dies, R reloads the scene at any time.
/// </summary>
public class GameFlow : MonoBehaviour
{
    public enum Result { Playing, Clear, Fail }

    [Tooltip("Seconds after the result before the game freezes, so the final hit is visible.")]
    [SerializeField] float freezeDelay = 0.5f;

    readonly List<Health> enemies = new List<Health>();
    Health player;
    float freezeTimer = -1f;

    public Result Current { get; private set; } = Result.Playing;

    void Start()
    {
        var motor = FindAnyObjectByType<PlayerMotor>();
        if (motor != null) player = motor.GetComponent<Health>();
        if (player != null) player.Died += _ => Finish(Result.Fail);

        // Every Health that isn't the player counts as an enemy.
        foreach (var h in FindObjectsByType<Health>())
        {
            if (h == player) continue;
            enemies.Add(h);
            h.Died += OnEnemyDied;
        }
    }

    void OnDestroy() => Time.timeScale = 1f;

    void OnEnemyDied(Health _)
    {
        foreach (var e in enemies)
            if (e != null && !e.IsDead) return;
        Finish(Result.Clear);
    }

    void Finish(Result r)
    {
        // First result wins (e.g. the last enemy and the player dying in the same charge).
        if (Current != Result.Playing) return;
        Current = r;
        freezeTimer = freezeDelay;
        Debug.Log($"[GameFlow] {r}");
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame)
        {
            Restart();
            return;
        }

        if (freezeTimer >= 0f)
        {
            freezeTimer -= Time.unscaledDeltaTime;
            if (freezeTimer < 0f) Time.timeScale = 0f;
        }
    }

    void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnGUI()
    {
        if (Current == Result.Playing)
        {
            GUI.Label(new Rect(10f, 10f, 200f, 20f), "R: restart");
            return;
        }

        var big = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 64,
            fontStyle = FontStyle.Bold,
        };
        big.normal.textColor = Current == Result.Clear ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.35f, 0.3f);
        var small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
        small.normal.textColor = Color.white;

        float cy = Screen.height * 0.4f;
        GUI.Label(new Rect(0f, cy - 50f, Screen.width, 100f), Current == Result.Clear ? "CLEAR" : "FAIL", big);
        GUI.Label(new Rect(0f, cy + 50f, Screen.width, 40f), "Press R to restart", small);
    }
}
