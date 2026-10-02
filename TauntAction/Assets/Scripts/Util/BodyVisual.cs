using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Finds the renderers that make up a character's look. Prefers a child named "Visual" (swap models there;
/// the root keeps the collider and scripts). Without one, falls back to the root's own renderer, so helper
/// visuals added as children (lanes, rings) are never picked up.
/// </summary>
public static class BodyVisual
{
    public const string ChildName = "Visual";

    public static Renderer[] Find(Transform root)
    {
        var list = new List<Renderer>();
        var visual = root.Find(ChildName);
        if (visual != null)
        {
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
                if (r is MeshRenderer || r is SkinnedMeshRenderer) list.Add(r);
        }
        else
        {
            var own = root.GetComponent<Renderer>();
            if (own != null) list.Add(own);
        }
        return list.ToArray();
    }

    /// <summary>Instanced materials for tinting (one per material slot across all body renderers).</summary>
    public static Material[] InstanceMaterials(Renderer[] renderers)
    {
        var mats = new List<Material>();
        foreach (var r in renderers) mats.AddRange(r.materials);
        return mats.ToArray();
    }

    public static void SetColor(Material[] mats, string property, Color c)
    {
        foreach (var m in mats)
            if (m != null && m.HasProperty(property)) m.SetColor(property, c);
    }

    public static void SetVisible(Renderer[] renderers, bool visible)
    {
        foreach (var r in renderers) if (r != null) r.enabled = visible;
    }
}
