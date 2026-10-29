using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
        private float          _untilBurst;

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
        }

        private void OnDisable()
        {
            _bomb?.Dispose();
            _bomb = null;
            ClearShots();
        }

        private void OnDestroy()
        {
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
                _untilBurst = 0f;
                return;
            }
            _untilBurst -= Time.fixedDeltaTime;
            if (_untilBurst > 0f)
            {
                return;
            }
            if ((_shots == null || _shots.Length == 0) && CreatePool() == false)
            {
                return;
            }
            FireBurst();
            // Never catch up with an unbounded burst after a slow frame.
            _untilBurst = 0.04f + (float)_random.NextDouble() * 0.04f;
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
            _untilBurst = 0f;
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
#endif
    }
}
