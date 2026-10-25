using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SlapMania.Editor
{
    /// Small helpers shared by the scene builders. Serialized-field writes go through
    /// SerializedObject so private [SerializeField]s can be wired without making them public
    /// (CONVENTIONS: no public fields on MonoBehaviours), and a wrong field name throws instead
    /// of silently leaving a reference unwired - the R11 failure this tool exists to prevent.
    internal static class SceneBuildUtil
    {
        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// Loads the asset at `path`, or creates it with the type's field defaults. Never
        /// overwrites an existing asset: re-running a builder must not stomp tuned values.
        public static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        /// A project layer by name (BUILD-AND-RUN §Tags and layers). Throws if it is missing:
        /// layers live in ProjectSettings, so a builder must never invent them (R9).
        public static int Layer(string name)
        {
            int l = LayerMask.NameToLayer(name);
            if (l < 0) throw new System.InvalidOperationException("Layer '" + name + "' is missing; see BUILD-AND-RUN §Tags and layers.");
            return l;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }

        public static Color Hex(string hex)
        {
            if (hex[0] != '#') hex = "#" + hex;
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }

        public static Color Hex(string hex, float alpha)
        {
            var c = Hex(hex);
            c.a = alpha;
            return c;
        }

        public static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            Find(so, target, field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Set(Object target, string field, Color value)
        {
            var so = new SerializedObject(target);
            Find(so, target, field).colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Set(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            Find(so, target, field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Set(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            Find(so, target, field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Set(Object target, string field, Vector3 value)
        {
            var so = new SerializedObject(target);
            Find(so, target, field).vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Set(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            Find(so, target, field).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = Find(so, target, field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// Throws if any listed object-reference field is null after wiring — the check that
        /// would otherwise only surface as "Disabling: no GameConfig" at Play time (R11).
        public static void AssertWired(Object target, params string[] fields)
        {
            var so = new SerializedObject(target);
            for (int i = 0; i < fields.Length; i++)
                if (Find(so, target, fields[i]).objectReferenceValue == null)
                    throw new System.InvalidOperationException(target.GetType().Name + "." + fields[i] + " is null after wiring.");
        }

        private static SerializedProperty Find(SerializedObject so, Object target, string field)
        {
            var p = so.FindProperty(field);
            if (p == null)
                throw new System.ArgumentException(target.GetType().Name + " has no serialized field '" + field +
                                                   "'. The builder is out of date with the component.");
            return p;
        }
    }
}
