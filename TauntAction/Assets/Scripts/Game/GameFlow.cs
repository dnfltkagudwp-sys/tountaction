using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Minimal win/lose + quick restart for playtesting.
/// With a RoomManager: CLEAR after the last room, R retries the current room, Shift+R restarts from room 1.
/// Without one: CLEAR when every enemy in the scene is dead, R reloads the scene.
/// FAIL when the player dies.
/// </summary>
public class GameFlow : MonoBehaviour
{
    public enum Result { Playing, Clear, Fail }

    [Tooltip("Seconds after the result before the game freezes, so the final hit is visible.")]
    [SerializeField] float freezeDelay = 0.5f;
    [Tooltip("After the last room: show ALL ROOMS CLEAR this long, then load the clear scene. Empty = stay here.")]
    [SerializeField] string clearScene = "Clear";
    [SerializeField] float clearSceneDelay = 1.5f;

    readonly List<Health> enemies = new List<Health>();
    Health player;
    RoomManager rooms;
    float freezeTimer = -1f;
    float clearTimer = -1f;

    public Result Current { get; private set; } = Result.Playing;

    void Start()
    {
        var motor = FindAnyObjectByType<PlayerMotor>();
        if (motor != null) player = motor.GetComponent<Health>();
        if (player != null) player.Died += _ => Finish(Result.Fail);

        rooms = FindAnyObjectByType<RoomManager>();
        if (rooms != null) return; // rooms report their own clear

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

    /// <summary>Called by RoomManager after the last room.</summary>
    public void AllRoomsCleared() => Finish(Result.Clear);

    void Finish(Result r)
    {
        // First result wins (e.g. the last enemy and the player dying in the same charge).
        if (Current != Result.Playing) return;
        Current = r;
        freezeTimer = freezeDelay;
        if (r == Result.Fail) RunStats.Deaths++;
        if (r == Result.Clear && rooms != null && !string.IsNullOrEmpty(clearScene)) clearTimer = clearSceneDelay;
        Debug.Log($"[GameFlow] {r}");
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame)
        {
            if (kb.shiftKey.isPressed) RestartRun();
            else Restart();
            return;
        }

        if (Current == Result.Playing) RunStats.PlayTime += Time.deltaTime;

        if (freezeTimer >= 0f)
        {
            freezeTimer -= Time.unscaledDeltaTime;
            if (freezeTimer < 0f) Time.timeScale = 0f;
        }

        if (clearTimer >= 0f)
        {
            clearTimer -= Time.unscaledDeltaTime;
            if (clearTimer < 0f)
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(clearScene);
            }
        }
    }

    /// <summary>Retry the current room (or the whole run once everything is cleared).</summary>
    void Restart()
    {
        bool fresh = rooms == null || Current == Result.Clear;
        RoomManager.StartRoomIndex = fresh ? 0 : rooms.CurrentRoomIndex;
        if (fresh) RunStats.Reset();
        Reload();
    }

    void RestartRun()
    {
        RoomManager.StartRoomIndex = 0;
        RunStats.Reset();
        Reload();
    }

    static void Reload()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnGUI()
    {
        if (Current == Result.Playing) return; // controls are on the HUD

        var big = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 64,
            fontStyle = FontStyle.Bold,
        };
        big.normal.textColor = Current == Result.Clear ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.35f, 0.3f);
        var small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
        small.normal.textColor = Color.white;

        string title = Current == Result.Clear ? (rooms != null ? "ALL ROOMS CLEAR" : "CLEAR") : "FAIL";
        string hint = Current == Result.Clear ? "Press R to play again"
                    : rooms != null ? $"R: retry room {rooms.CurrentRoomIndex + 1}   Shift+R: from room 1"
                    : "Press R to restart";

        float cy = Screen.height * 0.4f;
        GUI.Label(new Rect(0f, cy - 50f, Screen.width, 100f), title, big);
        GUI.Label(new Rect(0f, cy + 50f, Screen.width, 40f), hint, small);
    }
}
