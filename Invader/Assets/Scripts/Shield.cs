using System.Collections.Generic;
using UnityEngine;

namespace UnityInvader
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public sealed class Shield : MonoBehaviour, ISweptHitReceiver
    {
        private SpriteRenderer _renderer;
        private BoxCollider2D  _collider;
        private Texture2D      _texture;
        private Sprite         _sprite;
        private Color32[]      _initialPixels;
        private Color32[]      _displayPixels;
        private bool[]         _solid;
        private bool           _playing;
        private bool           _dirty;

        public const int Width  = 22;
        public const int Height = 16;

        private static readonly List<Collider2D> _candidates  = new List<Collider2D>(16);
        // Hand-authored provisional crater widths, from one pixel behind impact into the wall.
        private static readonly int[]           _playerMask  = { 1, 2, 3, 2, 2, 1 };
        private static readonly int[]           _enemyMask   = { 2, 3, 3, 2, 2, 1, 1 };

        private void Awake()
        {
            InitializePixels();
            BeginWave();
        }

        private void OnEnable()
        {
            if (_collider != null)
            {
                _collider.enabled = _playing;
            }
        }

        private void LateUpdate()
        {
            if (_dirty)
            {
                UpdateTexture();
            }
        }

        private void OnDisable()
        {
            if (_collider != null)
            {
                _collider.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_sprite != null)
            {
                Destroy(_sprite);
            }
            if (_texture != null)
            {
                Destroy(_texture);
            }
        }

        internal void BeginWave()
        {
            for (int index = 0; index < _solid.Length; index++)
            {
                _solid[index] = _initialPixels[index].a != 0;
            }
            UpdateTexture();
        }

        internal void SetPlaying(bool inPlaying)
        {
            _playing = inPlaying;
            if (_collider != null)
            {
                _collider.enabled = inPlaying && isActiveAndEnabled;
            }
        }

        public bool TryReceiveHit(in HitInfo inHit)
        {
            if (TryFindShotImpact(in inHit, out var pixel, out _) == false)
            {
                return false;
            }
            Carve(pixel, inHit.Direction.y > 0f);
            return true;
        }

        public bool TryGetHitDistance(in HitInfo inHit, out float outDistance)
        {
            return TryFindShotImpact(in inHit, out _, out outDistance);
        }

        internal static void ErodeByInvader(Bounds inStartBounds, Vector2 inMovement)
        {
            // Approximate the collider's whole path, including the 8px downward step.
            var swept     = inStartBounds;
            swept.center += (Vector3)inMovement * 0.5f;
            swept.size   += new Vector3(Mathf.Abs(inMovement.x), Mathf.Abs(inMovement.y), 0f);
            var filter    = new ContactFilter2D { useTriggers = true };
            int count     = Physics2D.OverlapBox(swept.center, swept.size, 0f, filter, _candidates);
            for (int index = 0; index < count; index++)
            {
                if (_candidates[index].TryGetComponent<Shield>(out var shield))
                {
                    shield.EraseOverlappingPixels(swept);
                }
            }
            _candidates.Clear();
        }

        private bool TryFindShotImpact(in HitInfo inHit, out Vector2Int outPixel, out float outDistance)
        {
            outPixel    = default;
            outDistance = 0f;
            // Existing pixel scan supports vertical projectiles only.
            if (inHit.Direction.x != 0f || inHit.Direction.y == 0f)
            {
                return false;
            }
            return TryFindImpact(inHit.Start, inHit.End, inHit.Size, inHit.Direction.y > 0f,
                out outPixel, out outDistance);
        }

        private void InitializePixels()
        {
            _renderer      = GetComponent<SpriteRenderer>();
            _collider      = GetComponent<BoxCollider2D>();
            _initialPixels = _renderer.sprite.texture.GetPixels32();
            _solid         = new bool[Width * Height];
            _displayPixels = new Color32[Width * Height];
            _texture       = new Texture2D(Width, Height, TextureFormat.RGBA32, false);

            _texture.name       = "Shield damage " + name;
            _texture.filterMode = FilterMode.Point;
            _texture.wrapMode   = TextureWrapMode.Clamp;

            _sprite = Sprite.Create(_texture, new Rect(0f, 0f, Width, Height), Vector2.zero,
                1f / ArcadeViewport.UnitsPerPixel, 0, SpriteMeshType.FullRect);
            _renderer.sprite = _sprite;
        }

        private bool TryFindImpact(Vector2 inStart, Vector2 inEnd, Vector2 inSize, bool inFromBelow,
            out Vector2Int outPixel, out float outDistance)
        {
            outPixel    = default;
            outDistance = 0f;
            if (_playing == false || isActiveAndEnabled == false || _solid == null)
            {
                return false;
            }
            // Saved shields are axis-aligned, with unit scale and bottom-left origin.
            var origin  = (Vector2)transform.position;
            var low     = (Vector2.Min(inStart, inEnd) - inSize * 0.5f - origin) / ArcadeViewport.UnitsPerPixel;
            var high    = (Vector2.Max(inStart, inEnd) + inSize * 0.5f - origin) / ArcadeViewport.UnitsPerPixel;
            int minX    = Mathf.Max(0, Mathf.FloorToInt(low.x + 0.0001f));
            int maxX    = Mathf.Min(Width - 1, Mathf.CeilToInt(high.x - 0.0001f) - 1);
            int minY    = Mathf.Max(0, Mathf.FloorToInt(low.y + 0.0001f));
            int maxY    = Mathf.Min(Height - 1, Mathf.CeilToInt(high.y - 0.0001f) - 1);
            float shotX = (inStart.x - origin.x) / ArcadeViewport.UnitsPerPixel;
            for (int row = 0; row <= maxY - minY; row++)
            {
                int y           = inFromBelow ? minY + row : maxY - row;
                int hitX        = -1;
                float xDistance = float.PositiveInfinity;
                for (int x = minX; x <= maxX; x++)
                {
                    float distance = Mathf.Abs(x + 0.5f - shotX);
                    if (_solid[y * Width + x] && distance < xDistance)
                    {
                        hitX      = x;
                        xDistance = distance;
                    }
                }
                if (hitX >= 0)
                {
                    outPixel    = new Vector2Int(hitX, y);
                    float edge  = origin.y + (inFromBelow ? y : y + 1) * ArcadeViewport.UnitsPerPixel;
                    outDistance = Mathf.Max(0f, inFromBelow ? edge - inStart.y - inSize.y * 0.5f : inStart.y - inSize.y * 0.5f - edge);
                    return true;
                }
            }
            return false;
        }

        private void EraseOverlappingPixels(Bounds inBounds)
        {
            if (_playing == false || isActiveAndEnabled == false || _solid == null)
            {
                return;
            }
            var origin = (Vector2)transform.position;
            var low    = ((Vector2)inBounds.min - origin) / ArcadeViewport.UnitsPerPixel;
            var high   = ((Vector2)inBounds.max - origin) / ArcadeViewport.UnitsPerPixel;
            int minX   = Mathf.Max(0, Mathf.FloorToInt(low.x + 0.0001f));
            int maxX   = Mathf.Min(Width - 1, Mathf.CeilToInt(high.x - 0.0001f) - 1);
            int minY   = Mathf.Max(0, Mathf.FloorToInt(low.y + 0.0001f));
            int maxY   = Mathf.Min(Height - 1, Mathf.CeilToInt(high.y - 0.0001f) - 1);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int index = y * Width + x;
                    if (_solid[index])
                    {
                        _solid[index] = false;
                        _dirty        = true;
                    }
                }
            }
        }

        private void Carve(Vector2Int inImpact, bool inFromBelow)
        {
            var mask      = inFromBelow ? _playerMask : _enemyMask;
            int direction = inFromBelow ? 1 : -1;
            for (int row = 0; row < mask.Length; row++)
            {
                int y = inImpact.y + (row - 1) * direction;
                if (y < 0 || y >= Height)
                {
                    continue;
                }
                for (int x = Mathf.Max(0, inImpact.x - mask[row]); x <= Mathf.Min(Width - 1, inImpact.x + mask[row]); x++)
                {
                    _solid[y * Width + x] = false;
                }
            }
            _dirty = true;
        }

        private void UpdateTexture()
        {
            for (int index = 0; index < _solid.Length; index++)
            {
                _displayPixels[index] = _solid[index] ? _initialPixels[index] : new Color32(0, 0, 0, 0);
            }
            _texture.SetPixels32(_displayPixels);
            _texture.Apply(false, false);
            _dirty = false;
        }
    }
}
