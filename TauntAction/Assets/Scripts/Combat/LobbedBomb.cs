using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Arcing bomb: flies over everything to a fixed landing point (marked on the floor), then explodes,
/// damaging every Health in the blast, including the thrower. Lobbed, so cover doesn't stop it.
/// </summary>
public class LobbedBomb : MonoBehaviour
{
    const float FloorY = 0.06f;
    const float FlashDuration = 0.2f;

    Vector3 start;
    Vector3 landing;
    float flightTime;
    float arcHeight;
    float blastRadius;
    float damageToPlayer;
    float damageToEnemy;
    GameObject source;
    float t;
    LineRenderer marker;
    Color markerStart;
    Color markerEnd;

    public void Init(GameObject source, Vector3 start, Vector3 landing, float flightTime, float arcHeight, float blastRadius,
                     float damageToPlayer, float damageToEnemy, Color markerStart, Color markerEnd)
    {
        this.source = source;
        this.start = start;
        this.landing = new Vector3(landing.x, 0f, landing.z);
        this.flightTime = Mathf.Max(0.05f, flightTime);
        this.arcHeight = arcHeight;
        this.blastRadius = blastRadius;
        this.damageToPlayer = damageToPlayer;
        this.damageToEnemy = damageToEnemy;
        this.markerStart = markerStart;
        this.markerEnd = markerEnd;

        marker = RingVisual.Create("BombMarker", null, blastRadius, 0.12f, markerStart);
        marker.transform.position = new Vector3(this.landing.x, FloorY, this.landing.z);
        transform.position = start;
    }

    void Update()
    {
        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / flightTime);

        Vector3 flat = Vector3.Lerp(start, new Vector3(landing.x, start.y, landing.z), u);
        flat.y = Mathf.Lerp(start.y, 0.3f, u) + arcHeight * 4f * u * (1f - u);
        transform.position = flat;

        if (marker != null)
        {
            RingVisual.SetColor(marker, Color.Lerp(markerStart, markerEnd, u));
            marker.widthMultiplier = Mathf.Lerp(0.08f, 0.25f, u);
        }

        if (u >= 1f) Explode();
    }

    void Explode()
    {
        Vector3 center = new Vector3(landing.x, 1f, landing.z);
        var hitOnce = new HashSet<Health>();
        foreach (var c in Physics.OverlapSphere(center, blastRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            var victim = c.GetComponentInParent<Health>();
            if (victim == null || victim.IsDead || !hitOnce.Add(victim)) continue;
            bool isEnemy = victim.GetComponent<ITauntable>() != null;
            Vector3 dir = victim.transform.position - center; dir.y = 0f;
            victim.TakeDamage(new DamageInfo(isEnemy ? damageToEnemy : damageToPlayer, source != null ? source : gameObject, dir.normalized));
        }

        SpawnFlash();
        if (marker != null) Destroy(marker.gameObject);
        Destroy(gameObject);
    }

    void SpawnFlash()
    {
        var flash = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        flash.name = "BombFlash";
        DestroyImmediate(flash.GetComponent<Collider>()); // must not register as an obstacle
        flash.transform.position = new Vector3(landing.x, 0.05f, landing.z);
        flash.transform.localScale = new Vector3(blastRadius * 2f, 0.02f, blastRadius * 2f);
        var r = flash.GetComponent<Renderer>();
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        r.material.SetColor("_BaseColor", markerEnd);
        Destroy(flash, FlashDuration);
    }

    void OnDestroy()
    {
        // e.g. scene reload mid-flight
        if (marker != null) Destroy(marker.gameObject);
    }
}
