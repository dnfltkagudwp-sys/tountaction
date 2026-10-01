using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Anything the player can taunt. Taunting means "attack the taunter now",
/// not "become able to attack". Shooter etc. implement this later.
/// </summary>
public interface ITauntable
{
    Transform transform { get; }
    bool CanBeTaunted { get; }

    /// <summary>Returns true if the taunt was accepted.</summary>
    bool Taunt(Transform taunter);
}

/// <summary>Live list of tauntable enemies, so the player doesn't search the scene every frame.</summary>
public static class TauntRegistry
{
    static readonly List<ITauntable> all = new List<ITauntable>();

    public static IReadOnlyList<ITauntable> All => all;

    public static void Register(ITauntable t)
    {
        if (!all.Contains(t)) all.Add(t);
    }

    public static void Unregister(ITauntable t) => all.Remove(t);

    // Survives play sessions when domain reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => all.Clear();
}
