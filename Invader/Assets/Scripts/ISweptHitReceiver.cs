namespace UnityInvader
{
    // Query without mutation. Only the nearest candidate receives TryReceiveHit.
    // Trigger dispatch excludes these receivers to avoid double processing.
    public interface ISweptHitReceiver : IHitReceiver
    {
        bool TryGetHitDistance(in HitInfo inHit, out float outDistance);
    }
}
