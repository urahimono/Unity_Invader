using UnityEngine;

namespace UnityInvader
{
    public interface IHitReceiver
    {
        HitTargetType TargetType { get; }

        bool TryReceiveHit(Component inSource);
    }
}
