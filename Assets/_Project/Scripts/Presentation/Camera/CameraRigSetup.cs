using System;
using SlapMania.Core.Cinematics;
using SlapMania.Core.Motion;
using UnityEngine;

namespace SlapMania.Presentation
{
    /// Everything the camera rig tools save and undo as one unit (M16): the rig's gameplay
    /// and intro angles, the resting FOV, the shake presets and the per-state transitions.
    /// JSON via JsonUtility. Capture/ApplyTo are null-safe and work in Play or Edit mode, so
    /// the runtime Rig tab and the Editor's apply-on-exit share one code path.
    [Serializable]
    public sealed class CameraRigSetup
    {
        /// Saves live in `<project>/Library/<FolderName>` in the Editor (local, never committed)
        /// and in `persistentDataPath/<FolderName>` on a device.
        public const string FolderName = "CameraRigSaves";

        public string name = "rig";
        public string savedAt = "";

        public Vector3 gameplayPosition;
        public Vector3 gameplayEuler;
        public Vector3 introPosition;
        public Vector3 introEuler;
        public Ease introEase = Ease.InOutCubic;
        public float fov = 50f;
        public ShakeProfile[] shakePresets = Array.Empty<ShakeProfile>();
        public StateShot[] states = Array.Empty<StateShot>();
        public float impactMinTrauma = 0.35f;
        public float impactScale = 1f;
        public int impactPreset = 1;
        public int lethalPreset = 2;

        public static CameraRigSetup Capture(CameraRig rig, Camera cam, CameraDirector director)
        {
            var s = new CameraRigSetup();
            if (rig != null)
            {
                s.gameplayPosition = rig.GameplayPosition;
                s.gameplayEuler = rig.GameplayEuler;
                s.introPosition = rig.IntroPosition;
                s.introEuler = rig.IntroEuler;
                s.introEase = rig.IntroEase;
            }
            if (director != null && Application.isPlaying) s.fov = director.RestFov;
            else if (cam != null) s.fov = cam.fieldOfView;
            if (director != null)
            {
                s.shakePresets = director.CopyShakePresets();
                s.states = director.CopyStateShots();
                s.impactMinTrauma = director.ImpactMinTrauma;
                s.impactScale = director.ImpactScale;
                s.impactPreset = director.ImpactPreset;
                s.lethalPreset = director.LethalPreset;
            }
            return s;
        }

        public void ApplyTo(CameraRig rig, Camera cam, CameraDirector director)
        {
            if (rig != null)
            {
                rig.GameplayPosition = gameplayPosition;
                rig.GameplayEuler = gameplayEuler;
                rig.IntroPosition = introPosition;
                rig.IntroEuler = introEuler;
                rig.IntroEase = introEase;
            }
            if (cam != null) cam.fieldOfView = fov;
            if (director != null)
            {
                director.RestFov = fov;
                if (shakePresets != null && shakePresets.Length > 0) director.SetShakePresets(shakePresets);
                if (states != null) director.SetStateShots(states);
                director.ImpactMinTrauma = impactMinTrauma;
                director.ImpactScale = impactScale;
                director.ImpactPreset = impactPreset;
                director.LethalPreset = lethalPreset;
            }
        }

        public CameraRigSetup Clone()
        {
            var c = (CameraRigSetup)MemberwiseClone();
            c.shakePresets = shakePresets != null ? (ShakeProfile[])shakePresets.Clone() : Array.Empty<ShakeProfile>();
            c.states = new StateShot[states != null ? states.Length : 0];
            for (int i = 0; i < c.states.Length; i++) c.states[i] = states[i]?.Clone();
            return c;
        }
    }
}
