using UnityEngine;

namespace SlapMania.Presentation
{
    /// The day-one capsule stand-in (M08). Colour tint plus a positional punch, all on
    /// unscaled time so it keeps reading during hitstop. It should look like an intentional
    /// test dummy, not a missing asset. Zero per-frame allocation: the MaterialPropertyBlock
    /// is reused and there are no closures.
    ///
    /// Offsets are along transform.forward, so point each capsule at its opponent in the
    /// scene: charge/finisher lean in, windup and recoil pull back.
    public sealed class PlaceholderFighterView : FighterViewBase
    {
        [SerializeField] private Renderer _renderer;
        [Header("Colours")]
        [SerializeField] private Color _idleColour   = Color.white;
        [SerializeField] private Color _chargeColour = new Color(0.35f, 0.90f, 0.35f);
        [SerializeField] private Color _windupColour = new Color(1.00f, 0.80f, 0.20f);
        [SerializeField] private Color _impactColour = new Color(1.00f, 0.20f, 0.20f);
        [Header("Motion (metres along forward)")]
        [SerializeField] private float _leanDistance   = 0.25f;
        [SerializeField] private float _recoilDistance = 0.60f;
        [SerializeField] private float _followSpeed    = 12f;
        [SerializeField] private float _colourSpeed    = 10f;
        [SerializeField] private float _impactFlashSeconds = 0.12f;
        [Tooltip("Head height above the root, for the HUD rings and the opponent's look target.")]
        [SerializeField] private float _headHeight = 0.6f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP Lit
        private static readonly int ColorId     = Shader.PropertyToID("_Color");     // built-in fallback
        private MaterialPropertyBlock _mpb;
        private Vector3 _homeWorldPos;
        private Vector3 _targetOffset;
        private Color _targetColour;
        private Color _currentColour;
        private float _flashUntil;
        private Transform _head;

        public override Transform HeadAnchor => _head != null ? _head : transform;

        private void Awake()
        {
            if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
            _mpb = new MaterialPropertyBlock();
            _homeWorldPos = transform.position;
            _currentColour = _targetColour = _idleColour;
            ApplyColour(_currentColour);
            _head = new GameObject("HeadAnchor").transform;
            _head.SetParent(transform, false);
            _head.localPosition = new Vector3(0f, _headHeight, 0f);
        }

        public override void PlayIdle()        { _targetOffset = Vector3.zero;                                  _targetColour = _idleColour; }
        public override void PlayChargePose()  { _targetOffset = transform.forward * _leanDistance;             _targetColour = _chargeColour; }
        public override void PlayWindup(float secondsToContact) { _targetOffset = -transform.forward * (_leanDistance * 0.5f); _targetColour = _windupColour; }
        public override void PlayFinisher()    { _targetOffset = transform.forward * (_leanDistance * 1.5f);    _targetColour = _windupColour; }
        public override void PlayVictoryPose() { _targetOffset = Vector3.zero;                                  _targetColour = _chargeColour; }
        public override void PlayDefeatPose()  { _targetOffset = -transform.forward * _recoilDistance;          _targetColour = _impactColour; }

        public override void PlayImpactRecoil(float intensity, Vector3 hitDirection)
        {
            intensity = Mathf.Clamp01(intensity);
            _targetOffset = -transform.forward * (_recoilDistance * Mathf.Lerp(0.4f, 1f, intensity));
            _flashUntil = Time.unscaledTime + _impactFlashSeconds;
        }

        public override void PlayKnockout(float intensity, Vector3 hitDirection)
        {
            PlayImpactRecoil(1f, hitDirection);
            _targetColour = _impactColour;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            Color desired = Time.unscaledTime < _flashUntil ? _impactColour : _targetColour;
            _currentColour = Color.Lerp(_currentColour, desired, 1f - Mathf.Exp(-_colourSpeed * dt));
            ApplyColour(_currentColour);

            Vector3 target = _homeWorldPos + _targetOffset;
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-_followSpeed * dt));
        }

        private void ApplyColour(Color c)
        {
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            _mpb.SetColor(ColorId, c);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
