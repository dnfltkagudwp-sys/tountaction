using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Flat debug ring drawn on the floor with a LineRenderer.</summary>
public static class RingVisual
{
    public static LineRenderer Create(string name, Transform parent, float radius, float width, Color color, int segments = 48)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        // Local XY becomes the floor plane; TransformZ keeps the line flat instead of camera-facing.
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.alignment = LineAlignment.TransformZ;
        lr.widthMultiplier = width;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        SetColor(lr, color);

        lr.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }
        return lr;
    }

    public static void SetColor(LineRenderer lr, Color c) => lr.material.SetColor("_BaseColor", c);
}
