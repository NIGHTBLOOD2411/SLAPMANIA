using System.IO;
using UnityEditor;
using UnityEngine;
using static SlapMania.Editor.SceneBuildUtil;

namespace SlapMania.Editor.Characters
{
    /// Renders a roster-card portrait (head and shoulders, transparent background) of a built
    /// fighter prefab into a PNG (M08). Uses PreviewRenderUtility, so it works in URP without
    /// touching the open scene. The fighter is posed on the first frame of its Idle clip.
    public static class PortraitRenderer
    {
        public const int Size = 256;

        /// Returns the imported texture at `pngPath`, or null when the prefab has no humanoid head.
        public static Texture2D Render(GameObject fighterPrefab, string pngPath)
        {
            var pru = new PreviewRenderUtility();
            try
            {
                var go = pru.InstantiatePrefabInScene(fighterPrefab);
                var anim = go.GetComponentInChildren<Animator>();
                if (anim == null || !anim.isHuman) return null;
                BeginIdlePose(go, anim); // stays sampled until StopAnimationMode, after the render
                var head = anim.GetBoneTransform(HumanBodyBones.Head);
                if (head == null) return null;

                // The Head bone sits at the base of the skull, and hair, horns or a hood can rise
                // well above it, so frame the span from below the neck to the model's top.
                var neck = anim.GetBoneTransform(HumanBodyBones.Neck);
                float neckY = neck != null ? neck.position.y : head.position.y - 0.1f;
                float topY = head.position.y + 0.12f;
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    if (r.enabled && r.bounds.max.y > topY) topY = r.bounds.max.y;
                topY = Mathf.Min(topY, head.position.y + (head.position.y - neckY) * 6f); // a raised prop must not shrink the face
                float bottomY = neckY - (topY - neckY) * 0.55f;
                float span = topY - bottomY;
                Vector3 look = new Vector3(head.position.x, (topY + bottomY) * 0.5f, head.position.z);
                Vector3 fwd = go.transform.forward;
                Vector3 side = go.transform.right;
                float distance = span * 0.5f / Mathf.Tan(12f * Mathf.Deg2Rad) * 1.08f; // fov 24, a little margin

                var cam = pru.camera;
                cam.fieldOfView = 24f;
                cam.nearClipPlane = 0.02f;
                cam.farClipPlane = 20f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                // backgroundColor is set per shot (black, then white).
                cam.transform.position = look + (fwd * 0.97f + side * 0.25f).normalized * distance;
                cam.farClipPlane = distance + 10f;
                cam.transform.LookAt(look);

                pru.lights[0].intensity = 1.25f;
                pru.lights[0].transform.rotation = Quaternion.LookRotation(-fwd + Vector3.down * 0.5f + side * 0.4f);
                pru.lights[1].intensity = 0.55f;
                pru.ambientColor = new Color(0.25f, 0.25f, 0.28f, 1f);

                // EndStaticPreview is the only read-back that works under URP, and it is RGB24
                // (reading cam.targetTexture directly came back empty). Recover alpha by
                // difference matting: render on black and on white; where the model covers a
                // pixel both agree, where it does not they differ by the full background step.
                var onBlack = Shot(pru, Color.black);
                var onWhite = Shot(pru, Color.white);
                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                var outPx = new Color32[onBlack.Length];
                for (int i = 0; i < outPx.Length; i++)
                {
                    Color32 b = onBlack[i], w = onWhite[i];
                    int spread = ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3;
                    byte a = (byte)Mathf.Clamp(255 - spread, 0, 255);
                    // Un-premultiply: the black shot is colour x alpha.
                    outPx[i] = a == 0 ? new Color32(0, 0, 0, 0)
                        : new Color32((byte)Mathf.Min(255, b.r * 255 / a), (byte)Mathf.Min(255, b.g * 255 / a),
                                      (byte)Mathf.Min(255, b.b * 255 / a), a);
                }
                tex.SetPixels32(outPx);
                tex.Apply();

                EnsureFolder(Path.GetDirectoryName(pngPath).Replace('\\', '/'));
                File.WriteAllBytes(pngPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
                if (AssetImporter.GetAtPath(pngPath) is TextureImporter ti)
                {
                    ti.textureType = TextureImporterType.Default;
                    ti.alphaIsTransparency = true;
                    ti.mipmapEnabled = false;
                    ti.maxTextureSize = Size;
                    ti.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            }
            finally
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                pru.Cleanup();
            }
        }

        private static Color32[] Shot(PreviewRenderUtility pru, Color background)
        {
            pru.camera.backgroundColor = background;
            pru.BeginStaticPreview(new Rect(0, 0, Size, Size));
            pru.Render(true);
            var shot = pru.EndStaticPreview();
            try { return shot.GetPixels32(); }
            finally { Object.DestroyImmediate(shot); }
        }

        // The controller's default state is Idle (FighterBuilder); stopping AnimationMode
        // restores the bind pose, so the caller stops it only once the frame is rendered.
        private static void BeginIdlePose(GameObject go, Animator anim)
        {
            var ctrl = anim.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            var idle = ctrl != null && ctrl.layers.Length > 0 && ctrl.layers[0].stateMachine.defaultState != null
                ? ctrl.layers[0].stateMachine.defaultState.motion as AnimationClip
                : null;
            if (idle == null) return;
            AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(go, idle, 0f);
            AnimationMode.EndSampling();
        }
    }
}
