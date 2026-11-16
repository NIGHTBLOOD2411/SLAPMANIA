using UnityEngine;
using SlapMania.Core;
using SlapMania.Data;
using SlapMania.Logging;
using SlapMania.Presentation;

namespace SlapMania.App
{
    /// Puts the chosen champions in the ring (M08, ADR-0022). The bout scene keeps its two
    /// built fighters as marks: each champion's prefab is spawned at its mark's pose, layer and
    /// parent, takes over the mark's contact shadow, and the mark is switched off. Runs once at
    /// bout start, before anything binds to the views, so it is off the per-frame paths (R5).
    internal static class ChampionSpawner
    {
        // The crimson shift the prototype builder gives the rival (PrototypeSceneBuilder), so a
        // mirror match still reads as two fighters.
        private static readonly Color RivalTint = new Color(1f, 0.6f, 0.58f, 1f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private const string ContactShadow = "Contact Shadow";

        /// Returns the spawned view, or `mark` unchanged when the champion cannot be spawned.
        /// `targetHumanScale` > 0 scales the champion so every body fits the table the same way
        /// (FighterFit, ADR-0023); 0 keeps its modelled size.
        public static FighterViewBase Spawn(FighterViewBase mark, ChampionDefinition champion, bool tintAsRival,
                                            float targetHumanScale)
        {
            if (mark == null || champion == null || champion.fighterPrefab == null) return mark;

            var t = mark.transform;
            var go = Object.Instantiate(champion.fighterPrefab, t.position, t.rotation, t.parent);
            go.name = mark.gameObject.name + " [" + champion.id + "]";
            var view = go.GetComponent<FighterViewBase>();
            if (view == null)
            {
                SlapLog.Error(LogChannel.Boot, "Champion '" + champion.id + "': prefab has no FighterViewBase on its root; keeping the scene fighter.");
                Object.Destroy(go);
                return mark;
            }

            var anim = go.GetComponentInChildren<Animator>();
            if (targetHumanScale > 0f && anim != null && anim.isHuman)
            {
                float s = FighterFit.UniformScale(anim.humanScale, targetHumanScale);
                go.transform.localScale *= s;
                SlapLog.Info(LogChannel.Boot, "Champion '" + champion.id + "' scaled x" + s.ToString("0.###") +
                             " (humanScale " + anim.humanScale.ToString("0.###") + " -> " + targetHumanScale.ToString("0.###") + ")");
            }
            SetLayerRecursive(go.transform, mark.gameObject.layer);
            var shadow = t.Find(ContactShadow);
            if (shadow != null) shadow.SetParent(go.transform, false);
            if (tintAsRival) Tint(go, RivalTint);

            mark.gameObject.SetActive(false);
            return view;
        }

        private static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayerRecursive(t.GetChild(i), layer);
        }

        // Instanced materials: the champion's shared materials are never touched.
        private static void Tint(GameObject go, Color tint)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r.gameObject.name == ContactShadow) continue;
                foreach (var m in r.materials)
                {
                    if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, m.GetColor(BaseColorId) * tint);
                    else if (m.HasProperty(ColorId)) m.SetColor(ColorId, m.GetColor(ColorId) * tint);
                }
            }
        }
    }
}
