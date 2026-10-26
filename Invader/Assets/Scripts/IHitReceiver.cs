namespace UnityInvader
{
    public interface IHitReceiver
    {
        bool TryReceiveHit(in HitInfo inHit);
    }
}
