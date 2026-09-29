using UnityEngine;

public struct DamageInfo
{
    public float Amount;
    public GameObject Source;
    public Vector3 Direction;

    public DamageInfo(float amount, GameObject source, Vector3 direction)
    {
        Amount = amount;
        Source = source;
        Direction = direction;
    }
}

public interface IDamageable
{
    bool IsDead { get; }
    void TakeDamage(DamageInfo info);
}
