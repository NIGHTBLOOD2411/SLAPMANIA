using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using SlapMania.Data;

namespace SlapMania.Presentation
{
    /// Runs the generated arena at runtime (M14/M15/M17, ADR-0012): applies the ArenaLook
    /// asset to the rig's lights and the scene's ambient/fog, device tiering, the slow sweep of
    /// the truss beams, and the impact flash — key light, beams and lamp flares surge on the
    /// impact frame and decay on unscaled time, so the flash still reads during hitstop.
    /// Everything expensive is a fake (emissive, additive cones, impostors); Low tier simply
    /// switches the high-only objects off.
    public sealed class ArenaDirector : MonoBehaviour
    {
        public enum Tier : byte { High, Low }

        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        [SerializeField] private ArenaLookConfig _look;
        [SerializeField] private Tier _tier = Tier.High;
        [Tooltip("Beams, haze sheets, the damage-cam camera — off on Low.")]
        [SerializeField] private GameObject[] _highOnly = new GameObject[0];

        [Header("Rig lights (driven by the look asset)")]
        [SerializeField] private Light _key;
        [SerializeField] private Light _rimLeft;
        [SerializeField] private Light _rimRight;
        [SerializeField] private Light _fill;
        [SerializeField] private Light _moon;

        [Header("Beams")]
        [SerializeField] private Transform[] _sweepBeams = new Transform[0];
        [Tooltip("Every additive renderer that surges on impact: beam cones and lamp flares.")]
        [SerializeField] private Renderer[] _beamRenderers = new Renderer[0];

        [Header("Jumbotron")]
        [SerializeField] private DamageCam _damageCam;

        private Quaternion[] _baseRotations = new Quaternion[0];
        private float[] _beamBase = new float[0];
        private float _flash;
        private bool _flashing;
        private int _appliedRevision = int.MinValue;
        private bool _sceneSettingsApplied;
        private MaterialPropertyBlock _mpb;

        public Tier CurrentTier
        {
            get => _tier;
            set { _tier = value; ApplyTier(); }
        }

        public ArenaLookConfig Look => _look;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _baseRotations = new Quaternion[_sweepBeams.Length];
            for (int i = 0; i < _sweepBeams.Length; i++)
                if (_sweepBeams[i] != null) _baseRotations[i] = _sweepBeams[i].localRotation;
            _beamBase = new float[_beamRenderers.Length];
            for (int i = 0; i < _beamRenderers.Length; i++)
            {
                var r = _beamRenderers[i];
                _beamBase[i] = r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty(IntensityId)
                    ? r.sharedMaterial.GetFloat(IntensityId) : 1f;
            }
            ApplyTier();
            ApplyLights();
        }

        private void ApplyTier()
        {
            bool high = _tier == Tier.High;
            for (int i = 0; i < _highOnly.Length; i++)
                if (_highOnly[i] != null) _highOnly[i].SetActive(high);
        }

        /// Lights are ours; ambient and fog belong to the active scene, so those wait until the
        /// bout scene is the active one (GameRoot activates it after the additive load).
        private void ApplyLights()
        {
            if (_look == null) return;
            _appliedRevision = _look.Revision;
            SetLight(_key, _look.keyColour, _look.keyIntensity);
            if (_key != null) _key.spotAngle = _look.keySpotAngle;
            SetLight(_rimLeft, _look.rimLeftColour, _look.rimLeftIntensity);
            SetLight(_rimRight, _look.rimRightColour, _look.rimRightIntensity);
            SetLight(_fill, _look.fillColour, _look.fillIntensity);
            SetLight(_moon, _look.moonColour, _look.moonIntensity);
            _sceneSettingsApplied = false;
        }

        private static void SetLight(Light l, Color c, float intensity)
        {
            if (l == null) return;
            l.color = c;
            l.intensity = intensity;
        }

        /// Writes the look's ambient, fog and reflection intensity into the active scene.
        /// The builder calls this in the Editor so the saved scene matches Play.
        public static void ApplySceneSettings(ArenaLookConfig look)
        {
            if (look == null) return;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = look.ambientSky;
            RenderSettings.ambientEquatorColor = look.ambientEquator;
            RenderSettings.ambientGroundColor = look.ambientGround;
            RenderSettings.reflectionIntensity = look.reflectionIntensity;
            RenderSettings.fog = look.fogDensity > 0f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = look.fogColour;
            RenderSettings.fogDensity = look.fogDensity;
        }

        /// A slap landed: `strength` 0..1 (damage-scaled).
        public void ImpactFlash(float strength)
        {
            _flash = Mathf.Max(_flash, Mathf.Clamp01(strength));
            _flashing = true;
        }

        /// Point the jumbotron feed at the fighter about to be hit.
        public void FocusDamageCam(Transform defenderHead, Vector3 attackerPosition)
        {
            if (_damageCam != null) _damageCam.SetTarget(defenderHead, attackerPosition);
        }

        private void Update()
        {
            if (_look == null) return;
            if (_look.Revision != _appliedRevision) ApplyLights();
            if (!_sceneSettingsApplied && SceneManager.GetActiveScene() == gameObject.scene)
            {
                ApplySceneSettings(_look);
                _sceneSettingsApplied = true;
            }

            float t = Time.time;
            float w = 2f * Mathf.PI / Mathf.Max(0.5f, _look.sweepPeriodMs * 0.001f);
            float deg = _look.sweepDegrees;
            for (int i = 0; i < _sweepBeams.Length; i++)
            {
                var b = _sweepBeams[i];
                if (b == null) continue;
                float phase = i * 1.7f;
                b.localRotation = _baseRotations[i] * Quaternion.Euler(
                    Mathf.Sin(t * w + phase) * deg, Mathf.Cos(t * w * 0.7f + phase) * deg, 0f);
            }

            if (!_flashing) return;
            _flash = Mathf.Max(0f, _flash - _look.flashDecayPerSecond * Time.unscaledDeltaTime);
            float k = _flash * _flash;
            if (_key != null) _key.intensity = _look.keyIntensity * (1f + _look.keyFlashBoost * k);
            for (int i = 0; i < _beamRenderers.Length; i++)
            {
                var r = _beamRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(IntensityId, _beamBase[i] * (1f + _look.beamFlashBoost * k));
                r.SetPropertyBlock(_mpb);
            }
            // One last write at zero restores the base values, then stop touching the renderers.
            if (_flash <= 0f) _flashing = false;
        }
    }
}
