using System.Collections.Generic;
using UnityEngine;
using SlapMania.Presentation;
using static SlapMania.Editor.SceneBuildUtil;

namespace SlapMania.Editor.Characters
{
    /// Builds the knock-out ragdoll on a humanoid, in the spirit of Unity's own Ragdoll Wizard:
    /// 11 bodies (hips, chest, head, upper/lower arms, upper/lower legs), colliders sized from
    /// the real bone lengths, CharacterJoints with the wizard's angular limits, 60 kg total.
    /// Everything starts kinematic with colliders off; RagdollController switches it on at
    /// the KO (M08, ADR-0011).
    internal static class RagdollBuilder
    {
        private struct Part
        {
            public HumanBodyBones Bone;
            public HumanBodyBones Parent;   // joint anchor body; Hips for the root
            public HumanBodyBones Child;    // the bone whose position ends this segment
            public float Mass;
            public float RadiusScale;       // collider radius as a fraction of the segment length
            public float LowTwist, HighTwist, Swing;
        }

        private static readonly Part[] Parts =
        {
            new Part { Bone = HumanBodyBones.Hips,          Parent = HumanBodyBones.Hips,          Child = HumanBodyBones.Spine,         Mass = 12f },
            new Part { Bone = HumanBodyBones.Chest,         Parent = HumanBodyBones.Hips,          Child = HumanBodyBones.Neck,          Mass = 12f, RadiusScale = 0.45f, LowTwist = -20f, HighTwist = 20f, Swing = 10f },
            new Part { Bone = HumanBodyBones.Head,          Parent = HumanBodyBones.Chest,         Child = HumanBodyBones.Head,          Mass = 5f,  LowTwist = -40f, HighTwist = 25f, Swing = 25f },
            new Part { Bone = HumanBodyBones.LeftUpperArm,  Parent = HumanBodyBones.Chest,         Child = HumanBodyBones.LeftLowerArm,  Mass = 2.5f, RadiusScale = 0.2f,  LowTwist = -70f, HighTwist = 10f, Swing = 50f },
            new Part { Bone = HumanBodyBones.LeftLowerArm,  Parent = HumanBodyBones.LeftUpperArm,  Child = HumanBodyBones.LeftHand,      Mass = 2f,   RadiusScale = 0.18f, LowTwist = -90f, HighTwist = 0f,  Swing = 0f },
            new Part { Bone = HumanBodyBones.RightUpperArm, Parent = HumanBodyBones.Chest,         Child = HumanBodyBones.RightLowerArm, Mass = 2.5f, RadiusScale = 0.2f,  LowTwist = -70f, HighTwist = 10f, Swing = 50f },
            new Part { Bone = HumanBodyBones.RightLowerArm, Parent = HumanBodyBones.RightUpperArm, Child = HumanBodyBones.RightHand,     Mass = 2f,   RadiusScale = 0.18f, LowTwist = -90f, HighTwist = 0f,  Swing = 0f },
            new Part { Bone = HumanBodyBones.LeftUpperLeg,  Parent = HumanBodyBones.Hips,          Child = HumanBodyBones.LeftLowerLeg,  Mass = 7f,   RadiusScale = 0.2f,  LowTwist = -20f, HighTwist = 70f, Swing = 30f },
            new Part { Bone = HumanBodyBones.LeftLowerLeg,  Parent = HumanBodyBones.LeftUpperLeg,  Child = HumanBodyBones.LeftFoot,      Mass = 4f,   RadiusScale = 0.16f, LowTwist = -80f, HighTwist = 0f,  Swing = 0f },
            new Part { Bone = HumanBodyBones.RightUpperLeg, Parent = HumanBodyBones.Hips,          Child = HumanBodyBones.RightLowerLeg, Mass = 7f,   RadiusScale = 0.2f,  LowTwist = -20f, HighTwist = 70f, Swing = 30f },
            new Part { Bone = HumanBodyBones.RightLowerLeg, Parent = HumanBodyBones.RightUpperLeg, Child = HumanBodyBones.RightFoot,     Mass = 4f,   RadiusScale = 0.16f, LowTwist = -80f, HighTwist = 0f,  Swing = 0f },
        };

        public static void Build(Animator anim, RagdollController controller)
        {
            var bodies = new Dictionary<HumanBodyBones, Rigidbody>();
            var colliders = new List<Collider>();
            Transform root = anim.transform;

            foreach (var p in Parts)
            {
                var bone = Bone(anim, p.Bone);
                if (bone == null) continue;
                var rb = bone.gameObject.AddComponent<Rigidbody>();
                rb.mass = p.Mass;
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.5f;
                bodies[p.Bone] = rb;
                var col = AddCollider(anim, p, bone);
                if (col != null) { col.enabled = false; colliders.Add(col); }
            }

            foreach (var p in Parts)
            {
                if (p.Bone == HumanBodyBones.Hips) continue;
                if (!bodies.TryGetValue(p.Bone, out var rb) || !bodies.TryGetValue(p.Parent, out var parent)) continue;
                var j = rb.gameObject.AddComponent<CharacterJoint>();
                j.connectedBody = parent;
                // Twist about the segment direction; swing about the character's right axis.
                Vector3 seg = SegmentDirection(anim, p);
                j.axis = rb.transform.InverseTransformDirection(seg);
                j.swingAxis = rb.transform.InverseTransformDirection(root.right);
                j.lowTwistLimit = new SoftJointLimit { limit = p.LowTwist };
                j.highTwistLimit = new SoftJointLimit { limit = p.HighTwist };
                j.swing1Limit = new SoftJointLimit { limit = p.Swing };
                j.swing2Limit = new SoftJointLimit { limit = p.Swing };
                j.enableProjection = true;
                j.enablePreprocessing = false;
            }

            var ordered = new List<Object>();
            foreach (var p in Parts) if (bodies.TryGetValue(p.Bone, out var rb)) ordered.Add(rb);
            Set(controller, "_animator", anim);
            SetArray(controller, "_bodies", ordered.ToArray());
            SetArray(controller, "_colliders", colliders.ConvertAll(c => (Object)c).ToArray());
            if (bodies.TryGetValue(HumanBodyBones.Head, out var head)) Set(controller, "_head", head);
        }

        private static Transform Bone(Animator anim, HumanBodyBones b)
        {
            var t = anim.GetBoneTransform(b);
            if (t == null && b == HumanBodyBones.Chest) t = anim.GetBoneTransform(HumanBodyBones.Spine);
            return t;
        }

        private static Vector3 SegmentDirection(Animator anim, Part p)
        {
            var a = Bone(anim, p.Bone);
            var b = p.Child == p.Bone ? null : Bone(anim, p.Child);
            if (a == null || b == null) return anim.transform.up;
            Vector3 d = b.position - a.position;
            return d.sqrMagnitude > 1e-8f ? d.normalized : anim.transform.up;
        }

        private static Collider AddCollider(Animator anim, Part p, Transform bone)
        {
            float scale = Mathf.Max(1e-4f, bone.lossyScale.x);
            if (p.Bone == HumanBodyBones.Head)
            {
                var s = bone.gameObject.AddComponent<SphereCollider>();
                s.radius = 0.11f / scale;
                s.center = bone.InverseTransformPoint(bone.position + anim.transform.up * 0.08f);
                return s;
            }
            if (p.Bone == HumanBodyBones.Hips)
            {
                var s = bone.gameObject.AddComponent<SphereCollider>();
                s.radius = 0.14f / scale;
                return s;
            }

            var end = Bone(anim, p.Child);
            if (end == null) return null;
            Vector3 localEnd = bone.InverseTransformPoint(end.position);
            float len = localEnd.magnitude;
            if (len < 1e-4f) return null;
            var c = bone.gameObject.AddComponent<CapsuleCollider>();
            c.direction = LargestAxis(localEnd);
            c.center = localEnd * 0.5f;
            c.height = len;
            c.radius = len * p.RadiusScale;
            return c;
        }

        private static int LargestAxis(Vector3 v)
        {
            float x = Mathf.Abs(v.x), y = Mathf.Abs(v.y), z = Mathf.Abs(v.z);
            return x > y && x > z ? 0 : y > z ? 1 : 2;
        }
    }
}
