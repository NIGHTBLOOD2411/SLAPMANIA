using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SlapMania.Core.Cinematics;

namespace SlapMania.Presentation
{
    /// The lens half of a `CameraPose`: depth of field through a private global Volume with a
    /// runtime Bokeh `DepthOfField` override (ADR-0020 §5). The arena's own profile is never
    /// touched, and the override is inactive unless the pose asks for DoF, because Bokeh is
    /// the most expensive post effect on a mobile GPU. FOV is set by `CameraDirector`.
    [RequireComponent(typeof(Camera))]
    public sealed class CameraLens : MonoBehaviour
    {
        [Tooltip("Above the arena's post volume, so this DoF wins.")]
        [SerializeField] private float _priority = 50f;
        [SerializeField, Range(1f, 300f)] private float _focalLengthMm = 50f;
        [SerializeField, Range(3, 9)] private int _bladeCount = 6;

        private GameObject _volumeGo;
        private VolumeProfile _profile;
        private DepthOfField _dof;
        private bool _dofOn;

        public bool DofActive => _dofOn;

        private void Awake()
        {
            _volumeGo = new GameObject("CameraLens DoF Volume") { hideFlags = HideFlags.DontSave };
            _volumeGo.transform.SetParent(transform, false);
            // The cameras' volume mask looks at Default (ArenaBuilder).
            _volumeGo.layer = 0;
            var volume = _volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = _priority;
            volume.weight = 1f;

            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.hideFlags = HideFlags.DontSave;
            _dof = _profile.Add<DepthOfField>(true);
            _dof.mode.Override(DepthOfFieldMode.Bokeh);
            _dof.focalLength.Override(_focalLengthMm);
            _dof.bladeCount.Override(_bladeCount);
            _dof.active = false;
            volume.sharedProfile = _profile;
        }

        private void OnDestroy()
        {
            if (_profile != null) Destroy(_profile);
        }

        public void Apply(in CameraPose pose)
        {
            if (_dof == null) return;
            if (pose.dof != _dofOn)
            {
                _dofOn = pose.dof;
                _dof.active = _dofOn;
            }
            if (!_dofOn) return;
            _dof.focusDistance.Override(pose.focusDistance);
            _dof.aperture.Override(pose.aperture);
        }
    }
}
