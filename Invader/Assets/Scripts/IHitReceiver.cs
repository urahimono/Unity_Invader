using UnityEngine;

namespace UnityInvader
{
    public interface IHitReceiver
    {
        bool TryReceiveHit(Component inSource);
    }
}
