using UnityEngine;
using TMPro;

namespace SlapMania.UI
{
    /// The V2 `.chroma` headline ("SMASH!", countdown digits, "TAP FAST!"): the white text with
    /// a cyan copy offset left and a red copy offset right behind it. The copies are siblings
    /// placed before the main text so they draw underneath. Every setter writes all three, so
    /// the layers never disagree; SetText with a number does not allocate.
    public sealed class ChromaText : MonoBehaviour
    {
        [SerializeField] private TMP_Text _main;
        [SerializeField] private TMP_Text _cyan;
        [SerializeField] private TMP_Text _red;

        public TMP_Text Main => _main;

        public void SetText(string text)
        {
            if (_main != null) _main.text = text;
            if (_cyan != null) _cyan.text = text;
            if (_red != null) _red.text = text;
        }

        public void SetText(string format, float value)
        {
            if (_main != null) _main.SetText(format, value);
            if (_cyan != null) _cyan.SetText(format, value);
            if (_red != null) _red.SetText(format, value);
        }

        public void SetMainColor(Color c)
        {
            if (_main != null) _main.color = c;
        }
    }
}
