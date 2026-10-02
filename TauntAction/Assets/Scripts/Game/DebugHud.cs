using System.Text;
using UnityEngine;

/// <summary>Plain-text tuning readout: player cooldowns and each enemy's state, HP and natural-attack timer.</summary>
public class DebugHud : MonoBehaviour
{
    [SerializeField] bool show = true;
    [SerializeField] Vector2 position = new Vector2(10f, 30f);

    PlayerMotor motor;
    PlayerTaunt taunt;
    Health playerHealth;
    readonly StringBuilder sb = new StringBuilder();
    GUIStyle style;

    void Start()
    {
        motor = FindAnyObjectByType<PlayerMotor>();
        if (motor != null)
        {
            taunt = motor.GetComponent<PlayerTaunt>();
            playerHealth = motor.GetComponent<Health>();
        }
    }

    void OnGUI()
    {
        if (!show) return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            style.normal.textColor = Color.white;
        }

        sb.Clear();
        if (playerHealth != null)
            sb.AppendLine($"Player HP {playerHealth.Current:0.#}/{playerHealth.Max:0.#}{(playerHealth.IsHitInvulnerable ? "  (hit i-frames)" : "")}");
        if (motor != null) sb.AppendLine($"Dash  {Cooldown(motor.DashCooldownRemaining)}");
        if (taunt != null) sb.AppendLine($"Taunt {Cooldown(taunt.CooldownRemaining)}");

        int alive = 0;
        var enemies = FindObjectsByType<EnemyBase>();
        foreach (var e in enemies)
        {
            var h = e.GetComponent<Health>();
            if (h == null || h.IsDead) continue;
            alive++;
            float nt = e.NaturalTimeRemaining;
            string natural = nt < 0f ? "-" : $"{nt:0.0}s";
            sb.AppendLine($"{e.name}: {e.StateLabel,-8} HP {h.Current:0.#}  next attack {natural}{(e.IsLaneBlocked ? "  (lane blocked)" : "")}{(e.IsTaunted ? "  TAUNTED" : "")}");
        }
        sb.Insert(0, $"Enemies alive: {alive}\n");
        var rooms = FindAnyObjectByType<RoomManager>();
        if (rooms != null && rooms.RoomCount > 0)
            sb.Insert(0, $"Room {rooms.CurrentRoomIndex + 1}/{rooms.RoomCount}  Wave {Mathf.Min(rooms.WaveIndex + 1, rooms.WaveCount)}/{rooms.WaveCount}  (left in wave: {rooms.RemainingInWave})\n");

        GUI.Label(new Rect(position.x, position.y, 560f, 300f), sb.ToString(), style);
    }

    static string Cooldown(float t) => t > 0f ? $"{t:0.0}s" : "ready";
}
