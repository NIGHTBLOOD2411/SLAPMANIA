using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SlapMania.Editor.Characters
{
    /// Authors the fighters' slap animation set from scratch (M08, ADR-0023): humanoid
    /// muscle-curve clips written as .anim assets, so they retarget through the Humanoid Avatar
    /// onto every champion, carry no third-party licence, and never move the root.
    ///
    /// Why authored and not the Mixamo boxing set: those clips step and lunge (the old Charge
    /// clip put a knee 0.63-0.85 m forward, straight through the slap table), make fists, and
    /// throw punches. A slap is thrown from a square stance with the feet planted: the chest
    /// twists, the open hand swings across at shoulder height, and SlapIK pins the palm to the
    /// cheek on the FSM's impact frame. Nothing below the hips moves, so a body that starts
    /// behind the table edge stays there.
    ///
    /// The key poses were solved on the Alucard rig (muscle values that put the elbow and hand
    /// at target offsets from the shoulder), then hand-tuned. Muscles are normalised, so the
    /// same values hold on any humanoid.
    ///
    ///   Slap_Idle      2.4 s loop - square stance, breathing
    ///   Slap_Charge    0.8 s loop - right hand cocked back by the ear, trembling with power
    ///   Slap_Forehand  1.2 s      - wind back, swing across, contact at ContactSeconds, recover
    ///                               (the left-hand slap is the same clip on a mirrored state)
    ///   Slap_Hit       0.9 s      - head snaps away, torso rocks back, arms flare
    ///   Slap_Block     0.8 s      - forearms up in front of the face, held
    ///   Slap_Victory   2.0 s loop - both arms up
    public static class SlapClipAuthor
    {
        public const string Dir = "Assets/_Project/Animation/Slap";
        public const string Idle = "Slap_Idle";
        public const string Charge = "Slap_Charge";
        public const string Forehand = "Slap_Forehand";
        public const string Hit = "Slap_Hit";
        public const string Block = "Slap_Block";
        public const string Victory = "Slap_Victory";

        /// When the palm meets the cheek in Slap_Forehand. Equal to BoutConfig.windupMs (500) so
        /// the clip plays at speed 1 instead of being slowed to fit the windup.
        public const float ContactSeconds = 0.5f;

        // Normalised body height of the stance (RootT.y): x humanScale it is the body's height in
        // metres. 1.0 puts the ankles ~0.15 m up, soles on the floor, on the template rig.
        private const float RootHeight = 1.0f;

        /// Not a muscle: degrees the whole body pitches forward from the hips. The thighs are
        /// re-straightened and the body shifted so the feet stay planted (PlantFeet), so a lean
        /// carries the head and shoulders over the table while nothing below it moves.
        private const string Lean = "Lean";

        // Degrees of thigh correction per degree of lean, and the yaw search for planting.
        private const float MaxYawDegrees = 40f;

        private sealed class Pose : Dictionary<string, float>
        {
            public Pose() { }
            public Pose(Pose from) : base(from) { }
            public Pose With(string muscle, float v) { var p = new Pose(this); p[muscle] = v; return p; }
            public Pose With(Pose over) { var p = new Pose(this); foreach (var kv in over) p[kv.Key] = kv.Value; return p; }
        }

        private struct Key { public float T; public Pose Pose; public Key(float t, Pose p) { T = t; Pose = p; } }

        // ── Poses ────────────────────────────────────────────────────
        // Sign conventions (measured, character space): Front-Back -1 bends forward, Twist +1
        // turns the chest to the right (right shoulder back), Arm Front-Back -1 swings the arm
        // forward across the body, Arm Down-Up +1 raises it, Forearm Stretch +1 straightens it.

        private static Pose Stance()
        {
            var p = new Pose();
            foreach (var side in new[] { "Left", "Right" })
            {
                p[side + " Upper Leg Front-Back"] = 0.5f;   // thigh vertical
                p[side + " Upper Leg In-Out"] = 0.08f;      // feet a little apart
                p[side + " Lower Leg Stretch"] = 0.92f;     // soft knee
                p[side + " Foot Up-Down"] = 0f;
                p[side + " Arm Down-Up"] = -0.62f;          // hanging, slightly away from the hips
                p[side + " Arm Front-Back"] = 0.22f;        // at the side, so long arms don't reach the table
                p[side + " Arm Twist In-Out"] = 0f;
                p[side + " Forearm Stretch"] = 0.85f;
                p[side + " Forearm Twist In-Out"] = 0f;
                p[side + " Shoulder Down-Up"] = 0f;
                OpenHand(p, side, 0.2f);
            }
            p["Spine Front-Back"] = 0f;
            p["Chest Front-Back"] = 0f;
            p["Head Nod Down-Up"] = 0f;
            return p;
        }

        private static void OpenHand(Pose p, string side, float stretch)
        {
            foreach (var f in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
            {
                for (int j = 1; j <= 3; j++) p[side + " " + f + " " + j + " Stretched"] = stretch;
                p[side + " " + f + " Spread"] = f == "Thumb" ? 0.2f : 0f;
            }
        }

        private static Pose Breath(Pose s) => s.With(new Pose
        {
            ["Chest Front-Back"] = 0.04f,
            ["Left Shoulder Down-Up"] = 0.06f, ["Right Shoulder Down-Up"] = 0.06f,
            ["Left Arm Down-Up"] = -0.58f, ["Right Arm Down-Up"] = -0.58f,
        });

        /// The right hand cocked back by the ear, chest turned away: held through Charge.
        private static Pose Cocked(Pose s)
        {
            var p = s.With(new Pose
            {
                ["Chest Twist Left-Right"] = 0.45f, ["UpperChest Twist Left-Right"] = 0.27f,
                ["Spine Front-Back"] = 0.05f,
                ["Right Arm Down-Up"] = 0.36f, ["Right Arm Front-Back"] = 0.55f,
                ["Right Arm Twist In-Out"] = 1.0f, ["Right Forearm Stretch"] = 0.2f,
                ["Right Hand Down-Up"] = 0.3f,
                // The free hand comes up in front of the belly for balance.
                ["Left Arm Down-Up"] = -0.45f, ["Left Arm Front-Back"] = -0.25f, ["Left Forearm Stretch"] = 0.1f,
                ["Head Turn Left-Right"] = -0.25f, // eyes stay on the opponent while the chest turns
            });
            OpenHand(p, "Right", 0.85f);
            return p;
        }

        private static Pose Tremble(Pose cocked, float k) => cocked.With(new Pose
        {
            ["Chest Twist Left-Right"] = 0.45f + 0.05f * k,
            ["Right Arm Down-Up"] = 0.36f + 0.04f * k,
            ["Right Shoulder Down-Up"] = 0.08f * k,
            ["Spine Front-Back"] = 0.05f - 0.03f * k,
        });

        /// Further back than cocked: the last load before the swing.
        private static Pose Wind(Pose s)
        {
            var p = Cocked(s).With(new Pose
            {
                ["Chest Twist Left-Right"] = 0.65f, ["UpperChest Twist Left-Right"] = 0.4f,
                ["Spine Front-Back"] = 0.08f,
                ["Right Arm Down-Up"] = 0.53f, ["Right Arm Front-Back"] = 0.76f,
                ["Right Forearm Stretch"] = 0.53f,
                ["Head Turn Left-Right"] = -0.35f,
            });
            return p;
        }

        /// Halfway: the arm out to the side at shoulder height, chest squaring up.
        private static Pose Swing(Pose s)
        {
            var p = s.With(new Pose
            {
                ["Chest Twist Left-Right"] = 0.1f, ["UpperChest Twist Left-Right"] = 0.06f,
                [Lean] = 12f,
                ["Right Arm Down-Up"] = 0.42f, ["Right Arm Front-Back"] = 0.05f,
                ["Right Arm Twist In-Out"] = 0.3f, ["Right Forearm Stretch"] = 0.7f,
                ["Left Arm Down-Up"] = -0.5f, ["Left Arm Front-Back"] = 0.25f,
            });
            OpenHand(p, "Right", 1f);
            return p;
        }

        /// The palm at the cheek: arm long at shoulder height, chest turned into it, leaning in
        /// over the table from the hips (the reach an upright slapper lacks).
        private static Pose Contact(Pose s)
        {
            var p = s.With(new Pose
            {
                ["Chest Twist Left-Right"] = -0.35f, ["UpperChest Twist Left-Right"] = -0.21f,
                [Lean] = 25f,
                ["Right Arm Down-Up"] = 0.25f, ["Right Arm Front-Back"] = -0.51f,
                ["Right Arm Twist In-Out"] = -0.29f, ["Right Forearm Stretch"] = 0.79f,
                ["Right Hand In-Out"] = 0.2f,
                ["Left Arm Down-Up"] = -0.55f, ["Left Arm Front-Back"] = 0.4f,
                ["Head Turn Left-Right"] = 0.15f,
            });
            OpenHand(p, "Right", 1f);
            return p;
        }

        private static Pose Follow(Pose s)
        {
            var p = s.With(new Pose
            {
                ["Chest Twist Left-Right"] = -0.7f, ["UpperChest Twist Left-Right"] = -0.42f,
                [Lean] = 27f,
                // The hand finishes across the chest, not down by the hip: a long-armed champion's
                // hand would otherwise sweep through the tabletop.
                ["Right Arm Down-Up"] = 0.1f, ["Right Arm Front-Back"] = -0.73f,
                ["Right Arm Twist In-Out"] = -0.5f, ["Right Forearm Stretch"] = 0.35f,
                ["Left Arm Down-Up"] = -0.55f, ["Left Arm Front-Back"] = 0.45f,
                ["Head Turn Left-Right"] = 0.3f,
            });
            OpenHand(p, "Right", 0.8f);
            return p;
        }

        private static Pose Slapped(Pose s, float k) => s.With(new Pose
        {
            ["Head Turn Left-Right"] = 0.7f * k, ["Neck Turn Left-Right"] = 0.4f * k,
            ["Head Tilt Left-Right"] = 0.3f * k, ["Head Nod Down-Up"] = 0.15f * k,
            ["Chest Twist Left-Right"] = 0.25f * k, ["UpperChest Twist Left-Right"] = 0.15f * k,
            ["Chest Front-Back"] = 0.12f * k, [Lean] = -8f * k, // rocks back, away from the table
            ["Spine Left-Right"] = 0.12f * k,
            // Arms flare out and back, away from the table, rather than bending forward.
            ["Left Arm Down-Up"] = -0.62f + 0.3f * k, ["Right Arm Down-Up"] = -0.62f + 0.3f * k,
            ["Left Arm Front-Back"] = 0.15f + 0.2f * k, ["Right Arm Front-Back"] = 0.15f + 0.2f * k,
        });

        private static Pose Guard(Pose s)
        {
            var p = s;
            foreach (var side in new[] { "Left", "Right" })
                p = p.With(new Pose
                {
                    [side + " Arm Down-Up"] = 0.05f, [side + " Arm Front-Back"] = -0.55f,
                    [side + " Arm Twist In-Out"] = 0.2f, [side + " Forearm Stretch"] = -0.6f,
                });
            return p.With(new Pose { ["Spine Front-Back"] = 0.08f, ["Head Nod Down-Up"] = -0.2f });
        }

        /// On the way to the guard: forearms fold first, elbows by the ribs, so the hands rise
        /// close to the body instead of swinging out over the table.
        private static Pose Tuck(Pose s)
        {
            var p = s;
            foreach (var side in new[] { "Left", "Right" })
                p = p.With(new Pose
                {
                    // Twisted so the forearms fold upward, not forward over the table.
                    [side + " Arm Down-Up"] = -0.3f, [side + " Arm Front-Back"] = 0.4f,
                    [side + " Arm Twist In-Out"] = 0.9f, [side + " Forearm Stretch"] = -0.2f,
                });
            return p;
        }

        private static Pose Cheer(Pose s, float k)
        {
            var p = s;
            foreach (var side in new[] { "Left", "Right" })
                p = p.With(new Pose
                {
                    [side + " Arm Down-Up"] = 0.85f + 0.1f * k, [side + " Arm Front-Back"] = -0.1f,
                    [side + " Forearm Stretch"] = 0.75f, [side + " Shoulder Down-Up"] = 0.3f * k,
                });
            return p.With(new Pose { ["Chest Front-Back"] = 0.15f + 0.05f * k, ["Head Nod Down-Up"] = 0.35f });
        }

        // ── Build ────────────────────────────────────────────────────

        [MenuItem("Tools/Slap Mania/Characters/Author Slap Clips", priority = 40)]
        public static void BuildMenu()
        {
            BuildAll();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Slap Mania", "Slap clips written to " + Dir +
                ".\nRebuild fighters (Champion Creator or Build Playable Prototype) to use them.", "OK");
        }

        /// Writes (or overwrites in place, GUIDs kept) every clip. Returns state role -> clip.
        public static Dictionary<string, AnimationClip> BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets/_Project/Animation", "Slap");

            var s = Stance();
            var c = Cocked(s);
            Dictionary<string, AnimationClip> clips;
            s_planter = FootPlanter.ForTemplateRig(s);
            try
            {
                clips = new Dictionary<string, AnimationClip>
                {
                [Idle] = Write(Idle, true, new Key(0f, s), new Key(1.2f, Breath(s)), new Key(2.4f, s)),
                [Charge] = Write(Charge, true,
                    new Key(0f, c), new Key(0.2f, Tremble(c, 1f)), new Key(0.4f, Tremble(c, -0.5f)),
                    new Key(0.6f, Tremble(c, 0.8f)), new Key(0.8f, c)),
                [Forehand] = Write(Forehand, false,
                    new Key(0f, c), new Key(0.3f, Wind(s)),
                    // The arm turns over with the forearm still folded, so it cannot whip low.
                    new Key(0.36f, Swing(s).With(new Pose { ["Right Arm Down-Up"] = 0.5f, ["Right Arm Twist In-Out"] = 0.7f, ["Right Forearm Stretch"] = 0.3f })),
                    new Key(0.42f, Swing(s)),
                    new Key(ContactSeconds, Contact(s)), new Key(0.66f, Follow(s)),
                    // Recover with the forearm folded and the arm drawn back to the body before it
                    // drops, so a long arm never sweeps down through the tabletop.
                    new Key(0.9f, Breath(s).With(new Pose
                    {
                        ["Chest Twist Left-Right"] = -0.2f, [Lean] = 4f,
                        // Twisted so the folded forearm points up, not forward over the table.
                        ["Right Arm Down-Up"] = 0.1f, ["Right Arm Front-Back"] = 0.25f,
                        ["Right Arm Twist In-Out"] = 0.85f, ["Right Forearm Stretch"] = 0.2f,
                    })),
                    // Straighten while still twisted (the hand rises out to the side), then lower the
                    // straight arm sideways: untwisting a straight arm does not move the hand.
                    new Key(1.0f, Breath(s).With(new Pose
                    {
                        ["Right Arm Down-Up"] = 0.05f, ["Right Arm Front-Back"] = 0.25f,
                        ["Right Arm Twist In-Out"] = 0.85f, ["Right Forearm Stretch"] = 0.85f,
                    })),
                    new Key(1.2f, s)),
                [Hit] = Write(Hit, false,
                    new Key(0f, s), new Key(0.07f, Slapped(s, 1f)), new Key(0.3f, Slapped(s, 0.7f)),
                    new Key(0.55f, Slapped(s, -0.15f)), new Key(0.9f, s)),
                [Block] = Write(Block, false, new Key(0f, s),
                    new Key(0.04f, s.With(new Pose { ["Left Arm Twist In-Out"] = 0.9f, ["Right Arm Twist In-Out"] = 0.9f })),
                    new Key(0.09f, Tuck(s)), new Key(0.16f, Guard(s)), new Key(0.8f, Guard(s))),
                [Victory] = Write(Victory, true, new Key(0f, Cheer(s, 0f)), new Key(1f, Cheer(s, 1f)), new Key(2f, Cheer(s, 0f))),
                };
            }
            finally
            {
                s_planter?.Dispose();
                s_planter = null;
            }
            return clips;
        }

        private static FootPlanter s_planter;

        private static AnimationClip Write(string name, bool loop, params Key[] keys)
        {
            string path = Dir + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool created = clip == null;
            if (created) clip = new AnimationClip { name = name };
            clip.ClearCurves();
            clip.frameRate = 30f;

            // Plant the feet first: a lean rewrites the thigh muscles, so it must precede the curves.
            var bodyT = new Vector3[keys.Length];
            var bodyQ = new Quaternion[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                if (s_planter != null) s_planter.Plant(keys[i].Pose, out bodyT[i], out bodyQ[i]);
                else { bodyT[i] = new Vector3(0f, RootHeight, 0f); bodyQ[i] = Quaternion.identity; }
            }

            foreach (var muscle in HumanTrait.MuscleName)
            {
                var curve = new AnimationCurve();
                for (int i = 0; i < keys.Length; i++)
                    curve.AddKey(keys[i].T, keys[i].Pose.TryGetValue(muscle, out float v) ? v : 0f);
                Smooth(curve);
                AnimationUtility.SetEditorCurve(clip, Binding(ClipPropertyName(muscle)), curve);
            }
            for (int axis = 0; axis < 7; axis++)
            {
                var curve = new AnimationCurve();
                for (int i = 0; i < keys.Length; i++)
                    curve.AddKey(keys[i].T, axis < 3 ? bodyT[i][axis] : bodyQ[i][axis - 3]);
                Smooth(curve);
                AnimationUtility.SetEditorCurve(clip, Binding(RootCurves[axis]), curve);
            }

            // Root baked into the pose, based upon Original: the fighter stays on its mark at the
            // height the planter authored. Not "Feet": that reads the LeftFootT/RightFootT goal
            // curves, which authored clips do not have, and grounds the body at y = 0 (S-105).
            var st = AnimationUtility.GetAnimationClipSettings(clip);
            st.loopTime = loop;
            st.loopBlend = false;
            st.loopBlendOrientation = true;
            st.loopBlendPositionY = true;
            st.loopBlendPositionXZ = true;
            st.keepOriginalOrientation = true;
            st.keepOriginalPositionY = true;
            st.keepOriginalPositionXZ = true;
            st.heightFromFeet = false;
            st.mirror = false;
            AnimationUtility.SetAnimationClipSettings(clip, st);

            if (created) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
            return clip;
        }

        private static EditorCurveBinding Binding(string property) =>
            EditorCurveBinding.FloatCurve("", typeof(Animator), property);

        private static readonly string[] RootCurves = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };

        /// Keeps the feet where the stance put them (ADR-0023). Upper-body muscles move the legs
        /// relative to Mecanim's body frame (a chest twist turns the feet, a lean swings them
        /// back), so for every key pose this finds the body rotation and position that put both
        /// ankles back on their stance marks, and re-straightens the thighs under a lean. Solved
        /// on the Alucard rig; the result is normalised, so it holds for any humanoid.
        private sealed class FootPlanter : System.IDisposable
        {
            private readonly GameObject _go;
            private readonly Animator _anim;
            private readonly HumanPoseHandler _handler;
            private readonly Transform _lFoot, _rFoot, _lHip, _lKnee;
            private readonly float _humanScale;
            private HumanPose _pose;
            private Vector3 _refL, _refR;

            private FootPlanter(GameObject model, Avatar avatar, Pose stance)
            {
                _go = Object.Instantiate(model);
                _go.hideFlags = HideFlags.HideAndDontSave;
                _go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                _go.transform.localScale = Vector3.one;
                _anim = _go.GetComponent<Animator>();
                if (_anim == null) _anim = _go.AddComponent<Animator>();
                _anim.avatar = avatar;
                _handler = new HumanPoseHandler(avatar, _go.transform);
                _handler.GetHumanPose(ref _pose);
                _humanScale = Mathf.Max(0.01f, _anim.humanScale);
                _lFoot = _anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                _rFoot = _anim.GetBoneTransform(HumanBodyBones.RightFoot);
                _lHip = _anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                _lKnee = _anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                Apply(stance, new Vector3(0f, RootHeight, 0f), Quaternion.identity);
                _refL = _lFoot.position;
                _refR = _rFoot.position;
            }

            /// Null (and the clips keep a fixed root) when the Alucard template is not in the project.
            public static FootPlanter ForTemplateRig(Pose stance)
            {
                try
                {
                    string path = FighterImportSetup.FindBaseModel(FighterImportSetup.AlucardStem);
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var avatar = FighterImportSetup.LoadAvatar(path);
                    if (model != null && avatar != null && avatar.isHuman) return new FootPlanter(model, avatar, stance);
                }
                catch (System.Exception e) { Debug.LogWarning("SlapClipAuthor: no template rig to plant feet on (" + e.Message + ")."); }
                Debug.LogWarning("SlapClipAuthor: Alucard template rig missing; clips keep a fixed root and feet may slide.");
                return null;
            }

            /// Solves `pose` in place (thighs under a lean) and returns the clip's RootT / RootQ.
            public void Plant(Pose pose, out Vector3 bodyT, out Quaternion bodyQ)
            {
                float lean = pose.TryGetValue(Lean, out float l) ? l : 0f;
                if (Mathf.Abs(lean) > 0.01f) StraightenThighs(pose, lean);

                float bestYaw = 0f, bestErr = float.MaxValue;
                Vector3 bestBody = new Vector3(0f, RootHeight, 0f);
                for (float step = 2f, lo = -MaxYawDegrees, hi = MaxYawDegrees; step >= 0.124f; step *= 0.25f)
                {
                    for (float yaw = lo; yaw <= hi + 1e-4f; yaw += step)
                    {
                        float err = Fit(pose, lean, yaw, out Vector3 body);
                        if (err < bestErr) { bestErr = err; bestYaw = yaw; bestBody = body; }
                    }
                    lo = bestYaw - step; hi = bestYaw + step;
                }
                bodyT = bestBody;
                bodyQ = Quaternion.Euler(lean, bestYaw, 0f);
            }

            // Body rotation fixed at (lean, yaw): the position that best returns both ankles to
            // their marks is the mean offset. Returns the squared error left over.
            private float Fit(Pose pose, float lean, float yaw, out Vector3 body)
            {
                var q = Quaternion.Euler(lean, yaw, 0f);
                var start = new Vector3(0f, RootHeight, 0f);
                Apply(pose, start, q);
                Vector3 shift = ((_refL - _lFoot.position) + (_refR - _rFoot.position)) * 0.5f;
                body = start + shift / _humanScale;
                Apply(pose, body, q);
                return (_refL - _lFoot.position).sqrMagnitude + (_refR - _rFoot.position).sqrMagnitude;
            }

            // A lean tips the pelvis forward; bring the thighs back to vertical (knee a hair forward).
            private void StraightenThighs(Pose pose, float lean)
            {
                float best = pose.TryGetValue("Left Upper Leg Front-Back", out float u) ? u : 0f, bestErr = float.MaxValue;
                for (float v = -1f; v <= 1f; v += 0.01f)
                {
                    pose["Left Upper Leg Front-Back"] = v;
                    Apply(pose, new Vector3(0f, RootHeight, 0f), Quaternion.Euler(lean, 0f, 0f));
                    float z = (_lKnee.position - _lHip.position).normalized.z - 0.04f;
                    if (z * z < bestErr) { bestErr = z * z; best = v; }
                }
                pose["Left Upper Leg Front-Back"] = best;
                pose["Right Upper Leg Front-Back"] = best;
            }

            private void Apply(Pose pose, Vector3 body, Quaternion rot)
            {
                var names = HumanTrait.MuscleName;
                for (int i = 0; i < _pose.muscles.Length; i++)
                    _pose.muscles[i] = pose.TryGetValue(names[i], out float v) ? v : 0f;
                _pose.bodyPosition = body;
                _pose.bodyRotation = rot;
                _handler.SetHumanPose(ref _pose);
            }

            public void Dispose()
            {
                _handler?.Dispose();
                if (_go != null) Object.DestroyImmediate(_go);
            }
        }

        private static void Smooth(AnimationCurve c)
        {
            for (int i = 0; i < c.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
            }
        }

        /// HumanTrait names fingers "Right Index 1 Stretched"; clip curves call the same muscle
        /// "RightHand.Index.1 Stretched". Every other muscle uses its HumanTrait name.
        public static string ClipPropertyName(string muscle)
        {
            foreach (var side in new[] { "Left", "Right" })
                foreach (var f in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                {
                    string prefix = side + " " + f + " ";
                    if (muscle.StartsWith(prefix)) return side + "Hand." + f + "." + muscle.Substring(prefix.Length);
                }
            return muscle;
        }
    }
}
