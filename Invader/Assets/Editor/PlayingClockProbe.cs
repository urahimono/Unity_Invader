using System;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace UnityInvader.Editor
{
    // Test-only observer of completed game frames. Restores the original loop on disposal.
    public sealed class PlayingClockProbe : IDisposable
    {
        public int Frames { get; private set; }

        public double Elapsed { get; private set; }

        private readonly PlayerLoopSystem _original;

        public PlayingClockProbe()
        {
            _original  = PlayerLoop.GetCurrentPlayerLoop();
            var loop   = _original;
            var stages = (PlayerLoopSystem[])loop.subSystemList.Clone();
            for (int index = 0; index < stages.Length; index++)
            {
                if (stages[index].type != typeof(PostLateUpdate))
                {
                    continue;
                }
                var observers = stages[index].subSystemList;
                Array.Resize(ref observers, observers.Length + 1);
                observers[observers.Length - 1] = new PlayerLoopSystem { type = typeof(PlayingClockProbe), updateDelegate = Observe };
                stages[index].subSystemList     = observers;
                loop.subSystemList              = stages;
                PlayerLoop.SetPlayerLoop(loop);
                return;
            }
            throw new InvalidOperationException("PostLateUpdate player-loop stage missing.");
        }

        public void Dispose() => PlayerLoop.SetPlayerLoop(_original);

        private void Observe()
        {
            Elapsed += Time.deltaTime;
            Frames++;
        }
    }
}
