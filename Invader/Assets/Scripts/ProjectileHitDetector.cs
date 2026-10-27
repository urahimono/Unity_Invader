using System.Collections.Generic;
using UnityEngine;

namespace UnityInvader
{
    internal static class ProjectileHitDetector
    {
        private static readonly List<Collider2D> _candidates = new List<Collider2D>(16);

        internal static bool TryResolveTrigger(Collider2D inOther, in HitInfo inHit)
        {
            if (inOther.TryGetComponent<IHitReceiver>(out var receiver) == false || receiver is ISweptHitReceiver)
            {
                return false;
            }
            return receiver.TryReceiveHit(in inHit);
        }

        internal static bool TryResolveSwept(in HitInfo inHit)
        {
            var center = (inHit.Start + inHit.End) * 0.5f;
            var size   = inHit.Size + new Vector2(Mathf.Abs(inHit.End.x - inHit.Start.x), Mathf.Abs(inHit.End.y - inHit.Start.y));
            var filter = new ContactFilter2D { useTriggers = true };
            int count  = Physics2D.OverlapBox(center, size, 0f, filter, _candidates);
            ISweptHitReceiver nearest         = null;
            float             nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                if (_candidates[index].TryGetComponent<ISweptHitReceiver>(out var receiver) &&
                    receiver.TryGetHitDistance(in inHit, out float distance) && distance < nearestDistance)
                {
                    nearest         = receiver;
                    nearestDistance = distance;
                }
            }
            // Clear before the receiver may synchronously notify gameplay owners.
            _candidates.Clear();
            return nearest != null && nearest.TryReceiveHit(in inHit);
        }
    }
}
