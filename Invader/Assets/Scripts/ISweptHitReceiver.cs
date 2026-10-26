namespace UnityInvader
{
    // Precise receivers that must be tested along a projectile's traveled interval,
    // rather than merely on entering a broad-phase Collider2D trigger.
    public interface ISweptHitReceiver : IHitReceiver
    {
        bool TryGetHitDistance(in HitInfo inHit, out float outDistance);
    }
}
