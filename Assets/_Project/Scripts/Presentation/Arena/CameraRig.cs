using UnityEngine;
using SlapMania.Core.Motion;

namespace SlapMania.Presentation
{
    /// The bout's gameplay framing: an establishing fly-in from a wide arena shot (the
    /// reference clip's framing) down to the gameplay pose during the intro countdown, and a
    /// camera kick on impact. The rig moves; the camera is its child. When the camera carries
    /// a `CameraDirector` (M16, ADR-0020) the director owns the child's final pose: recorded
    /// sequences and trauma shake layer on top of this rig, and `Kick` becomes trauma.
    /// Unscaled time throughout.
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] private Transform _camera;

        [Header("Poses (world)")]
        [SerializeField] private Vector3 _introPosition = new Vector3(-3.4f, 4.6f, -9.5f);
        [SerializeField] private Vector3 _introEuler = new Vector3(16f, 18f, 0f);
        [SerializeField] private Vector3 _gameplayPosition = new Vector3(0f, 1.75f, -3.6f);
        [SerializeField] private Vector3 _gameplayEuler = new Vector3(5f, 0f, 0f);
        [SerializeField] private Ease _ease = Ease.InOutCubic;

        [Header("Impact kick")]
        [SerializeField, Range(0f, 0.3f)] private float _kickMetres = 0.06f;
        [SerializeField, Range(0f, 10f)] private float _kickDegrees = 1.4f;
        [SerializeField, Range(0.05f, 1f)] private float _kickSeconds = 0.28f;

        private readonly Tweener _tw = new Tweener(4);
        private float _t0;
        private float _duration;
        private bool _flying;
        private CameraDirector _director;

        /// The director on the child camera, or null on a rig without one.
        public CameraDirector Director => _director;
        public Camera Camera => _camera != null ? _camera.GetComponent<Camera>() : null;

        // Setters for the camera rig tools (recorder Rig tab, Editor apply). Changing a pose
        // does not move the rig; call SnapToGameplay / PlayIntro to see it.
        public Vector3 GameplayPosition { get => _gameplayPosition; set => _gameplayPosition = value; }
        public Vector3 GameplayEuler { get => _gameplayEuler; set => _gameplayEuler = value; }
        public Vector3 IntroPosition { get => _introPosition; set => _introPosition = value; }
        public Vector3 IntroEuler { get => _introEuler; set => _introEuler = value; }
        public Ease IntroEase { get => _ease; set => _ease = value; }
        public bool IsFlyingIntro => _flying;

        private void Awake()
        {
            if (_camera != null) _director = _camera.GetComponent<CameraDirector>();
            SnapToGameplay();
        }

        public void SnapToGameplay()
        {
            _flying = false;
            transform.SetPositionAndRotation(_gameplayPosition, Quaternion.Euler(_gameplayEuler));
        }

        public void PlayIntro(float seconds)
        {
            if (seconds <= 0.05f) { SnapToGameplay(); return; }
            _t0 = Time.unscaledTime;
            _duration = seconds;
            _flying = true;
            transform.SetPositionAndRotation(_introPosition, Quaternion.Euler(_introEuler));
        }

        /// `strength` 0..1, damage-scaled; a lethal slap gets the director's lethal shake.
        public void Kick(float strength, bool lethal)
        {
            if (_director != null) { _director.Impact(strength, lethal); return; }
            Kick(strength);
        }

        /// `strength` 0..1, damage-scaled.
        public void Kick(float strength)
        {
            if (_camera == null) return;
            strength = Mathf.Clamp01(strength);
            // The director rewrites the camera's pose every LateUpdate, so a tween here would
            // be overwritten: hand the hit over as trauma instead (lethal hits get the
            // Finisher preset).
            if (_director != null) { _director.Impact(strength, strength >= 1f); return; }
            _tw.Kill(_camera);
            _camera.localPosition = Vector3.zero;
            _camera.localRotation = Quaternion.identity;
            _tw.Shake(_camera, new Vector3(_kickMetres, _kickMetres * 0.6f, 0f) * strength, _kickSeconds, 14);
            _tw.ShakeRotation(_camera, _kickDegrees * strength, _kickSeconds, 12);
        }

        private void LateUpdate()
        {
            _tw.Tick(Time.unscaledDeltaTime);
            if (!_flying) return;
            float k = Easing.Evaluate(_ease, (Time.unscaledTime - _t0) / _duration);
            transform.SetPositionAndRotation(
                Vector3.LerpUnclamped(_introPosition, _gameplayPosition, k),
                Quaternion.SlerpUnclamped(Quaternion.Euler(_introEuler), Quaternion.Euler(_gameplayEuler), k));
            if (k >= 1f) _flying = false;
        }
    }
}
