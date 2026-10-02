using UnityEngine;

/// <summary>Numbers for the clear screen. Survives room retries (scene reloads); reset when a run starts over.</summary>
public static class RunStats
{
    public static float PlayTime;
    public static int Deaths;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        PlayTime = 0f;
        Deaths = 0;
    }
}
