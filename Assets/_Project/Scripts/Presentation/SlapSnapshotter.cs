using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using SlapMania.Core;
using SlapMania.Gameplay;
using SlapMania.Logging;
using Object = UnityEngine.Object;

namespace SlapMania.Presentation
{
    /// One photo per landed slap (M09, ADR-0016): the defender's face from the attacker's side,
    /// rendered once into a small sRGB RenderTexture (S-032) by a photo camera that stays
    /// disabled between shots. Up to Capacity photos are kept for the bout; past that the
    /// weakest non-lethal one is overwritten (S-037), so the gallery's candidates survive.
    /// The latest slap is also rendered at hero size (640 x 800) for D4 Instant Replay, which
    /// shows it across the full screen width.
    ///
    /// The pre-atlas path: one Camera.Render per slap, like FighterPortrait, which already
    /// renders correctly here. The 4x4 atlas + SubmitRenderRequest of M09 §3 replaces it once
    /// the full gallery and the GLES3 smoke test exist. Not on the Charge/Timing path (R5): it
    /// runs in the Snapshot state, and each RenderTexture is allocated once and reused.
    public sealed class SlapSnapshotter : IDisposable
    {
        public const int Capacity = 16;
        public const int Width = 256;   // 4:5 portrait, M09 §4 Low tier
        public const int Height = 320;
        public const int HeroWidth = 640;    // D4's 430 x 520 photo panel at ~1.5x
        public const int HeroHeight = 800;

        private readonly Camera _scene;
        private readonly SlapShot[] _shots = new SlapShot[Capacity];
        private readonly RenderTexture[] _photos = new RenderTexture[Capacity];
        private RenderTexture _hero;
        private SlapShot _latest;
        private GameObject _rig;
        private Camera _cam;
        private int _count;

        // Framing: the face from the attacker's side of the line, turned off-axis so the cheek
        // and the incoming hand read. Metres and degrees.
        private const float Distance = 1.05f;
        private const float SideDegrees = 35f;
        private const float FieldOfView = 36f;
        private const float Lift = 0.05f;

        /// `scene` is the gameplay camera: the photo copies its clear and culling settings.
        public SlapSnapshotter(Camera scene) { _scene = scene; }

        public int Count => _count;
        public SlapShot ShotAt(int i) => _shots[i];
        public Texture PhotoAt(int i) => _photos[i];
        /// The latest slap at hero size (null before the first), and what it showed.
        public Texture LatestHero => _hero;
        public SlapShot LatestShot => _latest;

        /// Photograph `defender` as it takes `shot`. Returns false (and logs) when there is
        /// nothing to point at.
        public bool Capture(Transform defenderHead, Vector3 attackerPosition, in SlapShot shot)
        {
            if (defenderHead == null) { SlapLog.Warn(LogChannel.Snapshot, "No defender head: snapshot skipped."); return false; }
            int slot = _count < Capacity ? _count++ : SnapshotPicker.SlotToEvict(_shots, _count);
            if (_photos[slot] == null)
            {
                _photos[slot] = new RenderTexture(Width, Height, 24, GraphicsFormat.R8G8B8A8_SRGB)
                                { name = "SlapSnapshot" + slot, antiAliasing = 1 }; // S-035: no MSAA
                _photos[slot].Create();
            }
            _shots[slot] = shot;
            Render(defenderHead, attackerPosition, _photos[slot]);
            if (_hero == null)
            {
                _hero = new RenderTexture(HeroWidth, HeroHeight, 24, GraphicsFormat.R8G8B8A8_SRGB)
                        { name = "SlapSnapshotHero", antiAliasing = 1 };
                _hero.Create();
            }
            Render(defenderHead, attackerPosition, _hero);
            _latest = shot;
            return true;
        }

        /// The loser's gallery (finisher + hardest hits, chronological) into `textures`; returns
        /// how many were written. `damage` (optional, same length) gets each photo's damage.
        public int FillGallery(FighterSlot loser, Texture[] textures, int[] damage)
        {
            if (textures == null) return 0;
            var picked = new int[textures.Length];
            int n = SnapshotPicker.PickLoserGallery(_shots, _count, loser, picked);
            for (int i = 0; i < textures.Length; i++)
            {
                textures[i] = i < n ? _photos[picked[i]] : null;
                if (damage != null && i < damage.Length) damage[i] = i < n ? _shots[picked[i]].Damage : 0;
            }
            return n;
        }

        private void Render(Transform head, Vector3 attackerPosition, RenderTexture target)
        {
            EnsureCamera();
            Vector3 focus = head.position + Vector3.down * 0.04f;
            Vector3 toAttacker = attackerPosition - head.position;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < 1e-6f) toAttacker = Vector3.forward;
            Vector3 dir = Quaternion.AngleAxis(SideDegrees, Vector3.up) * toAttacker.normalized;
            _cam.transform.position = focus + dir * Distance + Vector3.up * Lift;
            _cam.transform.LookAt(focus);
            _cam.targetTexture = target;
            _cam.ResetAspect();                 // follow this target (gallery 4:5, hero 4:5), not the copied camera
            _cam.Render();
            _cam.targetTexture = null;
        }

        private void EnsureCamera()
        {
            if (_cam != null) return;
            _rig = new GameObject("SnapshotCamera");
            _cam = _rig.AddComponent<Camera>();
            if (_scene != null) _cam.CopyFrom(_scene);
            _cam.enabled = false;               // renders only on Capture; the GameObject stays active
            _cam.fieldOfView = FieldOfView;
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 40f;
            _cam.allowMSAA = false;             // S-035
            int mask = _scene != null ? _scene.cullingMask : ~0;
            int only = LayerMask.NameToLayer("SnapshotOnly");
            int hide = LayerMask.NameToLayer("HideInSnapshot");
            int ui = LayerMask.NameToLayer("UI");
            if (only >= 0) mask |= 1 << only;
            if (hide >= 0) mask &= ~(1 << hide);
            if (ui >= 0) mask &= ~(1 << ui);    // S-036: never the HUD
            _cam.cullingMask = mask;
        }

        /// Frees every photo and the camera (S-033). The gallery must not be shown afterwards.
        public void Dispose()
        {
            for (int i = 0; i < _photos.Length; i++)
            {
                if (_photos[i] == null) continue;
                _photos[i].Release();
                Object.Destroy(_photos[i]);
                _photos[i] = null;
            }
            _count = 0;
            if (_hero != null) { _hero.Release(); Object.Destroy(_hero); _hero = null; }
            if (_rig != null) Object.Destroy(_rig);
            _rig = null;
            _cam = null;
        }
    }
}
