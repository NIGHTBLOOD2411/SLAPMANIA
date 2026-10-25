using UnityEngine;
using UnityEngine.UIElements;

namespace SlapMania.UI.FrontEnd.Screens
{
    /// B6 Settings: every switch and slider remembers its value between sessions. They are
    /// stored under "fe.<element name>" until M12 (save) and M11 (audio) own these settings
    /// and read them; nothing consumes them yet.
    internal sealed class SettingsView : ScreenView
    {
        private const string Prefix = "fe.";

        protected override void OnBind()
        {
            Root.Query<KitSwitch>().ForEach(BindSwitch);
            Root.Query<KitSlider>().ForEach(BindSlider);
        }

        private void BindSwitch(KitSwitch s)
        {
            if (string.IsNullOrEmpty(s.name)) return;
            string key = Prefix + s.name;
            if (PlayerPrefs.HasKey(key)) s.SetValueWithoutNotify(PlayerPrefs.GetInt(key) != 0);
            s.Changed += OnSwitch;
        }

        private void BindSlider(KitSlider s)
        {
            if (string.IsNullOrEmpty(s.name)) return;
            string key = Prefix + s.name;
            if (PlayerPrefs.HasKey(key)) s.SetValueWithoutNotify(PlayerPrefs.GetFloat(key));
            s.Changed += OnSlider;
        }

        private static void OnSwitch(KitSwitch s, bool on) => PlayerPrefs.SetInt(Prefix + s.name, on ? 1 : 0);
        private static void OnSlider(KitSlider s, float v) => PlayerPrefs.SetFloat(Prefix + s.name, v);

        protected override void OnHide() => PlayerPrefs.Save();
    }
}
