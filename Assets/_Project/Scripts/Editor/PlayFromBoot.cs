using UnityEditor;
using UnityEditor.SceneManagement;

namespace SlapMania.Editor
{
    /// Makes Play always start at Boot (M00), whatever scene is open, so the editor runs the
    /// same Boot -> additive bout path a device build does. Toggle with
    /// Tools > Slap Mania > Prototype > Play From Boot (on by default; stored per machine).
    /// With it off, pressing Play in Proto_Bout still works: BoutController falls back to its
    /// serialized GameConfig when there is no GameRoot.
    [InitializeOnLoad]
    public static class PlayFromBoot
    {
        private const string MenuPath = "Tools/Slap Mania/Prototype/Play From Boot";
        private const string PrefKey = "SlapMania.PlayFromBoot";

        static PlayFromBoot() => EditorApplication.delayCall += Apply;

        private static bool Enabled => EditorPrefs.GetBool(PrefKey, true);

        [MenuItem(MenuPath, priority = 20)]
        private static void Toggle()
        {
            EditorPrefs.SetBool(PrefKey, !Enabled);
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        public static void Apply()
        {
            EditorSceneManager.playModeStartScene = Enabled
                ? AssetDatabase.LoadAssetAtPath<SceneAsset>(PrototypeSceneBuilder.BootScenePath)
                : null;
        }
    }
}
