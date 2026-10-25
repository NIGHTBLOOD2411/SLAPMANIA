using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace SlapMania.Presentation
{
    /// One-shot head-and-shoulders portrait of a fighter for the HUD scorebug (V2 C4/C5 tiles)
    /// and the K.O. winner line. Renders once into a small sRGB RenderTexture (S-032: a linear
    /// RT would show washed out in a RawImage) with a temporary camera, then throws the camera
    /// away. Call it after the Animator's first update so the pose is not the bind T-pose.
    public static class FighterPortrait
    {
        public static RenderTexture Capture(Transform head, Vector3 facing, int size = 192, float distance = 0.8f, float fov = 32f)
        {
            if (head == null) return null;
            var rt = new RenderTexture(size, size, 16, GraphicsFormat.R8G8B8A8_SRGB) { name = "FighterPortrait" };
            rt.Create();

            var go = new GameObject("PortraitCamera");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.082f, 0.094f, 0.118f, 1f); // V2 tile #15181E
                cam.fieldOfView = fov;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 6f;
                facing.y = 0f;
                if (facing.sqrMagnitude < 1e-6f) facing = Vector3.forward;
                Vector3 focus = head.position + Vector3.down * 0.06f;
                go.transform.position = focus + facing.normalized * distance + Vector3.up * 0.04f;
                go.transform.LookAt(focus);
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
            }
            finally
            {
                Object.Destroy(go);
            }
            return rt;
        }
    }
}
