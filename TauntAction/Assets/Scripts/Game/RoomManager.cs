using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs the rooms in order: places the player, spawns waves (telegraphed, capped by maxConcurrent),
/// shows ROOM CLEAR and moves on. GameFlow reloads the scene on retry and tells us which room to start in.
/// </summary>
public class RoomManager : MonoBehaviour
{
    [Tooltip("Parent whose RoomDefinition children are the rooms, in sibling order.")]
    [SerializeField] Transform roomsRoot;

    [Header("Spawning")]
    [Tooltip("A floor ring marks the spot this long before the enemy appears.")]
    [SerializeField] float spawnTelegraphTime = 0.8f;
    [SerializeField] float spawnInterval = 0.5f;
    [Tooltip("Hold a spawn while the player stands closer than this to its point.")]
    [SerializeField] float minSpawnDistanceFromPlayer = 3f;
    [SerializeField] Color spawnMarkerColor = new Color(1f, 1f, 1f);

    [Header("Flow")]
    [SerializeField] float waveGap = 0.5f;
    [SerializeField] float roomClearDelay = 1.2f;
    [Tooltip("Refill the player's HP when a room starts.")]
    [SerializeField] bool refillHealthOnRoomStart = true;

    /// <summary>Room to start in after a scene reload (set by GameFlow on retry). Reset when play mode starts.</summary>
    public static int StartRoomIndex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => StartRoomIndex = 0;

    class PendingSpawn
    {
        public RoomDefinition.SpawnEntry entry;
        public float timer;
        public LineRenderer marker;
    }

    readonly List<RoomDefinition> rooms = new List<RoomDefinition>();
    readonly List<Health> alive = new List<Health>();
    readonly Queue<RoomDefinition.SpawnEntry> queued = new Queue<RoomDefinition.SpawnEntry>();
    readonly List<PendingSpawn> telegraphing = new List<PendingSpawn>();

    PlayerMotor player;
    GameFlow gameFlow;
    float spawnCooldown;
    float flowTimer;
    bool roomCleared;
    bool finished;

    public int CurrentRoomIndex { get; private set; }
    public int RoomCount => rooms.Count;
    public int WaveIndex { get; private set; }
    public int WaveCount => Room != null ? Room.waves.Count : 0;
    public int AliveCount => alive.Count;
    public int RemainingInWave => queued.Count + telegraphing.Count + alive.Count;

    RoomDefinition Room => CurrentRoomIndex >= 0 && CurrentRoomIndex < rooms.Count ? rooms[CurrentRoomIndex] : null;

    void Start()
    {
        player = FindAnyObjectByType<PlayerMotor>();
        gameFlow = FindAnyObjectByType<GameFlow>();

        if (roomsRoot != null)
            foreach (Transform child in roomsRoot)
            {
                var def = child.GetComponent<RoomDefinition>();
                if (def == null) continue;
                rooms.Add(def);
                child.gameObject.SetActive(false);
            }

        if (rooms.Count == 0) { Debug.LogWarning("[RoomManager] No rooms found."); return; }
        EnterRoom(Mathf.Clamp(StartRoomIndex, 0, rooms.Count - 1));
    }

    void EnterRoom(int index)
    {
        ClearRoomState();
        if (Room != null) Room.gameObject.SetActive(false);

        CurrentRoomIndex = index;
        Room.gameObject.SetActive(true);
        roomCleared = false;

        if (player != null && Room.playerStart != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(Room.playerStart.position, Room.playerStart.rotation);
            if (cc != null) cc.enabled = true;
            var hp = player.GetComponent<Health>();
            if (refillHealthOnRoomStart && hp != null) hp.ResetHealth();
        }

        Debug.Log($"[RoomManager] Room {index + 1}/{rooms.Count}: {Room.name}");
        WaveIndex = -1;
        StartNextWave();
    }

    void StartNextWave()
    {
        WaveIndex++;
        if (WaveIndex >= Room.waves.Count)
        {
            OnRoomCleared();
            return;
        }
        foreach (var s in Room.waves[WaveIndex].spawns)
            if (s != null && s.enemyPrefab != null && s.spawnPoint != null) queued.Enqueue(s);
        spawnCooldown = 0f;
    }

    void OnRoomCleared()
    {
        roomCleared = true;
        if (CurrentRoomIndex >= rooms.Count - 1)
        {
            finished = true;
            if (gameFlow != null) gameFlow.AllRoomsCleared();
            return;
        }
        flowTimer = roomClearDelay;
    }

    void Update()
    {
        if (Room == null || finished) return;
        float dt = Time.deltaTime;

        if (roomCleared)
        {
            flowTimer -= dt;
            if (flowTimer <= 0f) EnterRoom(CurrentRoomIndex + 1);
            return;
        }

        alive.RemoveAll(h => h == null || h.IsDead);

        // Start telegraphs while there's room under the cap.
        spawnCooldown -= dt;
        if (queued.Count > 0 && spawnCooldown <= 0f && alive.Count + telegraphing.Count < Room.maxConcurrent)
        {
            var next = queued.Peek();
            if (!PlayerTooClose(next.spawnPoint.position))
            {
                queued.Dequeue();
                telegraphing.Add(new PendingSpawn
                {
                    entry = next,
                    timer = spawnTelegraphTime,
                    marker = MakeMarker(next.spawnPoint.position),
                });
                spawnCooldown = spawnInterval;
            }
        }

        for (int i = telegraphing.Count - 1; i >= 0; i--)
        {
            var p = telegraphing[i];
            p.timer -= dt;
            if (p.marker != null)
                p.marker.widthMultiplier = Mathf.Lerp(0.04f, 0.2f, 1f - Mathf.Clamp01(p.timer / spawnTelegraphTime));
            if (p.timer > 0f) continue;
            Spawn(p.entry);
            if (p.marker != null) Destroy(p.marker.gameObject);
            telegraphing.RemoveAt(i);
        }

        if (queued.Count == 0 && telegraphing.Count == 0 && alive.Count == 0)
        {
            flowTimer -= dt;
            if (flowTimer <= -waveGap)
            {
                flowTimer = 0f;
                StartNextWave();
            }
        }
        else flowTimer = 0f;
    }

    void Spawn(RoomDefinition.SpawnEntry entry)
    {
        Vector3 pos = entry.spawnPoint.position;
        pos.y = 1f;
        Quaternion rot = Quaternion.identity;
        if (player != null)
        {
            Vector3 to = player.transform.position - pos; to.y = 0f;
            if (to.sqrMagnitude > 0.001f) rot = Quaternion.LookRotation(to, Vector3.up);
        }
        var go = Instantiate(entry.enemyPrefab, pos, rot);
        go.name = entry.enemyPrefab.name;
        var hp = go.GetComponent<Health>();
        if (hp != null) alive.Add(hp);
    }

    bool PlayerTooClose(Vector3 point)
    {
        if (player == null) return false;
        Vector3 d = player.transform.position - point; d.y = 0f;
        return d.magnitude < minSpawnDistanceFromPlayer;
    }

    LineRenderer MakeMarker(Vector3 point)
    {
        var ring = RingVisual.Create("SpawnMarker", null, 0.9f, 0.04f, spawnMarkerColor);
        ring.transform.position = new Vector3(point.x, 0.06f, point.z);
        return ring;
    }

    /// <summary>Drop leftovers from the previous room: enemies, telegraphs, bullets, bombs.</summary>
    void ClearRoomState()
    {
        foreach (var h in alive) if (h != null) Destroy(h.gameObject);
        alive.Clear();
        queued.Clear();
        foreach (var p in telegraphing) if (p.marker != null) Destroy(p.marker.gameObject);
        telegraphing.Clear();
        foreach (var b in FindObjectsByType<Projectile>()) Destroy(b.gameObject);
        foreach (var b in FindObjectsByType<LobbedBomb>()) Destroy(b.gameObject);
        flowTimer = 0f;
    }

    void OnGUI()
    {
        if (!roomCleared || finished) return;
        var style = UiFont.Label(48, new Color(0.4f, 1f, 0.5f), TextAnchor.MiddleCenter, FontStyle.Bold);
        GUI.Label(new Rect(0f, Screen.height * 0.35f, Screen.width, 80f), $"{CurrentRoomIndex + 1}번 방 정화", style);
    }
}
