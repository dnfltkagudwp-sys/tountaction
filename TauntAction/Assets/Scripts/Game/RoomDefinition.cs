using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One room: its obstacles and spawn points live as children (move them in the Scene view),
/// waves are listed here. Each wave starts once the previous wave is fully cleared.
/// </summary>
public class RoomDefinition : MonoBehaviour
{
    [Serializable]
    public class SpawnEntry
    {
        public GameObject enemyPrefab;
        public Transform spawnPoint;
    }

    [Serializable]
    public class Wave
    {
        public List<SpawnEntry> spawns = new List<SpawnEntry>();
    }

    [Tooltip("Where the player is placed when the room starts.")]
    public Transform playerStart;
    [Tooltip("Most enemies alive at once; extra spawns wait for a slot.")]
    [Min(1)] public int maxConcurrent = 3;
    public List<Wave> waves = new List<Wave>();

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (playerStart != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(playerStart.position, 0.6f);
            UnityEditor.Handles.Label(playerStart.position + Vector3.up * 1.5f, "Player");
        }

        for (int w = 0; w < waves.Count; w++)
        {
            foreach (var s in waves[w].spawns)
            {
                if (s == null || s.spawnPoint == null) continue;
                string kind = s.enemyPrefab != null ? s.enemyPrefab.name : "?";
                Gizmos.color = kind.Contains("Charger") ? new Color(1f, 0.3f, 0.2f)
                             : kind.Contains("Shooter") ? new Color(0.3f, 1f, 1f)
                             : kind.Contains("Bomber") ? new Color(0.8f, 0.4f, 1f)
                             : Color.white;
                Gizmos.DrawWireSphere(s.spawnPoint.position, 0.6f);
                UnityEditor.Handles.Label(s.spawnPoint.position + Vector3.up * 1.5f, $"W{w + 1} {kind}");
            }
        }
    }
#endif
}
