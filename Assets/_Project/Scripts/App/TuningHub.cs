using UnityEngine;
using SlapMania.Data;

namespace SlapMania.App
{
    /// One-stop tuning surface for the bout (M13 editor tooling). Select this object and its
    /// custom Inspector (SlapMania.Editor.TuningHubEditor) shows every config asset inline,
    /// then the scene's feel components, then a live readout of the FSM during Play Mode.
    ///
    /// Asset edits persist after Play Mode ends - that is the point: tune while playing, keep
    /// the result. Component values on the scene objects listed here revert like any scene
    /// edit; the Inspector says so above that section.
    ///
    /// Holds references only and never drives gameplay. The builder tags its GameObject
    /// "EditorOnly", so Unity strips it from player builds (R4 - stripped, not disabled). It
    /// lives in App rather than Diagnostics because it references BoutController, and nothing
    /// but Editor may depend on App.
    public sealed class TuningHub : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;
        [SerializeField] private BoutController _bout;
        [Tooltip("Assets outside GameConfig worth tuning here, e.g. each fighter's rig profile.")]
        [SerializeField] private ScriptableObject[] _extraAssets;
        [SerializeField] private Component[] _sceneComponents;

        public GameConfig Config => _config;
        public BoutController Bout => _bout;
        public ScriptableObject[] ExtraAssets => _extraAssets;
        public Component[] SceneComponents => _sceneComponents;
    }
}
