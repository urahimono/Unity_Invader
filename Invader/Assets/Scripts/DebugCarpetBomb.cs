using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;
#endif

namespace UnityInvader
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameplayDriver))]
    [DefaultExecutionOrder(-100)]
    public sealed class DebugCarpetBomb : MonoBehaviour
    {
        [SerializeField]
        private PlayerShot _shotPrefab;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private GameplayDriver _driver;
        private InputAction    _bomb;
        private PlayerShot[]   _shots;
        private int            _laneIndex = LaneCount;
        private int            _waveNumber;
        private bool           _bombReady = true;

        private CancellationTokenSource _burstCancellation;
        private UniTaskCompletionSource _burstGate;

        private const int PoolSize   = 96;
        private const int LaneCount  = 23;
        private const int BurstSize  = 3;

        private readonly int[]         _lanes  = new int[LaneCount];
        private readonly System.Random _random = new System.Random();

        private void Awake()
        {
            _driver = GetComponent<GameplayDriver>();
        }

        private void OnEnable()
        {
            _bomb = new InputAction("DebugCarpetBomb", InputActionType.Button, "<Keyboard>/b");
            _bomb.Enable();
            _driver.GameStarting += ResetForNewGame;
        }

        private void OnDisable()
        {
            _bomb?.Dispose();
            _bomb = null;
            _driver.GameStarting -= ResetForNewGame;
            ClearShots();
        }

        private void OnDestroy()
        {
            StopBurstLoop();
            if (_shots == null)
            {
                return;
            }
            foreach (var shot in _shots)
            {
                if (shot != null)
                {
                    shot.FlightEnded -= ResolveShot;
                }
            }
        }

        private void LateUpdate()
        {
            // Also clear transitions made by Driver.Update before the next rendered frame.
            if (IsPlaying() == false || _waveNumber != _driver.WaveNumber)
            {
                ClearShots();
            }
        }

        private void FixedUpdate()
        {
            UpdateBombardment();
        }

        private void ResolveShot(HitTargetType inTargetType)
        {
            _driver.NotifyShotResolved(inTargetType);
        }

        private bool IsPlaying()
        {
            return _driver != null && _driver.isActiveAndEnabled && _driver.CurrentState == GameState.Playing;
        }

        private void UpdateBombardment()
        {
            if (IsPlaying() == false)
            {
                ClearShots();
                return;
            }
            if (_waveNumber != _driver.WaveNumber)
            {
                ClearShots();
                _waveNumber = _driver.WaveNumber;
            }
            if (_bomb.IsPressed() == false)
            {
                StopBurstLoop();
                _bombReady  = true;
                return;
            }
            if (_bombReady == false)
            {
                return;
            }
            if ((_shots == null || _shots.Length == 0) && CreatePool() == false)
            {
                return;
            }
            if (_burstCancellation == null)
            {
                _burstCancellation = new CancellationTokenSource();
                RunBurstLoopAsync(_burstCancellation, _waveNumber).Forget(HandleBurstException);
                return;
            }
            // Resume inline here, preserving this component's FixedUpdate execution order.
            UniTaskCompletionSource gate = _burstGate;
            _burstGate = null;
            gate?.TrySetResult();
        }

        private async UniTask RunBurstLoopAsync(CancellationTokenSource inCancellation, int inWaveNumber)
        {
            CancellationToken token = inCancellation.Token;
            try
            {
                while (CanContinueBurst(inCancellation, inWaveNumber))
                {
                    FireBurst();
                    float interval = 0.04f + (float)_random.NextDouble() * 0.04f;
                    double nextBurstAt = Time.fixedTimeAsDouble + interval;
                    // Fixed clock avoids Delay's rendered-frame skip during multiple physics steps.
                    await UniTask.WaitUntil(() => Time.fixedTimeAsDouble >= nextBurstAt,
                        PlayerLoopTiming.FixedUpdate, token, cancelImmediately: true);
                    if (CanContinueBurst(inCancellation, inWaveNumber) == false)
                    {
                        return;
                    }
                    _burstGate = new UniTaskCompletionSource();
                    await _burstGate.Task;
                    // Only UpdateBombardment releases this gate, after checking input and State.
                }
            }
            catch (System.OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Release, State/Wave exit, reset and destruction are normal cancellation paths.
            }
            finally
            {
                if (_burstCancellation == inCancellation)
                {
                    _burstCancellation = null;
                    _burstGate = null;
                }
                inCancellation.Dispose();
            }
        }

        private bool CanContinueBurst(CancellationTokenSource inCancellation, int inWaveNumber)
        {
            return _burstCancellation == inCancellation && isActiveAndEnabled && IsPlaying() &&
                _driver.WaveNumber == inWaveNumber && _bombReady && _bomb != null && _bomb.IsPressed();
        }

        private void StopBurstLoop()
        {
            CancellationTokenSource cancellation = _burstCancellation;
            UniTaskCompletionSource gate = _burstGate;
            _burstCancellation = null;
            _burstGate = null;
            if (cancellation != null)
            {
                CancellationToken token = cancellation.Token;
                cancellation.Cancel();
                // The gate wait must also finish; do not leave an abandoned pending task.
                gate?.TrySetCanceled(token);
            }
            // RunBurstLoopAsync owns Dispose in its finally, even on immediate cancellation.
        }

        private void HandleBurstException(System.Exception inException)
        {
            Debug.LogException(inException, this);
            enabled = false;
        }

        private bool CreatePool()
        {
            if (_shotPrefab == null)
            {
                Debug.LogError("Debug Carpet Bomb requires the existing PlayerShot prefab.", this);
                enabled = false;
                return false;
            }
            _shots = new PlayerShot[PoolSize];
            for (int index = 0; index < _shots.Length; index++)
            {
                var shot      = Instantiate(_shotPrefab, transform);
                shot.name     = $"Debug PlayerShot {index}";
                shot.FlightEnded += ResolveShot;
                _shots[index] = shot;
            }
            ShuffleLanes();
            return true;
        }

        private void FireBurst()
        {
            int launched = 0;
            foreach (var shot in _shots)
            {
                float x      = 2f + _lanes[_laneIndex] * 10f - ArcadeViewport.ScreenWidth / 2f;
                var position = new Vector2(x, 8f - ArcadeViewport.ScreenHeight / 2f) * ArcadeViewport.UnitsPerPixel;
                if (shot.TryLaunch(position))
                {
                    _laneIndex++;
                    launched++;
                    if (_laneIndex >= LaneCount)
                    {
                        ShuffleLanes();
                    }
                }
                if (launched >= BurstSize)
                {
                    break;
                }
            }
        }

        private void ShuffleLanes()
        {
            for (int index = 0; index < LaneCount; index++)
            {
                _lanes[index] = index;
            }
            for (int index = LaneCount - 1; index > 0; index--)
            {
                int other     = _random.Next(index + 1);
                int lane      = _lanes[index];
                _lanes[index] = _lanes[other];
                _lanes[other] = lane;
            }
            _laneIndex = 0;
        }

        private void ClearShots()
        {
            StopBurstLoop();
            if (_shots == null)
            {
                return;
            }
            foreach (var shot in _shots)
            {
                if (shot != null)
                {
                    shot.ResetFlight();
                }
            }
        }

        private void ResetForNewGame()
        {
            ClearShots();
            _bombReady  = false;
            _waveNumber = 0;
            if (_shots != null)
            {
                ShuffleLanes();
            }
        }
#endif
    }
}
