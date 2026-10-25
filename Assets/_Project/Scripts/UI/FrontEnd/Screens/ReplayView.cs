using UnityEngine;
using UnityEngine.UIElements;
using SlapMania.Logging;

namespace SlapMania.UI.FrontEnd.Screens
{
    /// D4 Instant Replay · Damage Cam, made live (M09/M10, ADR-0016): the photo panel shows the
    /// latest landed slap's snapshot (ScreenManager.Replay, filled by the bout) instead of the
    /// mockup still, and the header, the three stat tiles and the timeline label carry the
    /// slap's real round, damage, power, zone and impact time.
    ///
    /// The UXML is generated (Tools/mockup2uxml) and has no names inside the photo panel, so the
    /// hooks are its stable shapes: the `fe-img` element, the three `k-tl` tiles (caption label,
    /// value label), and the two captions whose mockup copy says FINAL BLOW and IMPACT. A
    /// re-converted mockup that loses one is logged once at bind, and that part stays as drawn.
    ///
    /// Between the K.O. and the results (ReplayShot.EndOfBout), Close and Next both end the
    /// replay (ScreenManager.ReplayFinished). From the pause menu they navigate as drawn.
    /// The camera chips, speed chips, Save and Share stay placeholders: a snapshot is one frame.
    internal sealed class ReplayView : ScreenView
    {
        private VisualElement _photo;
        private Label _caption;
        private Label _impact;
        private readonly Label[] _tileCaption = new Label[3];
        private readonly Label[] _tileValue = new Label[3];

        protected override void OnBind()
        {
            _photo = Root.Q(className: "fe-img");
            if (_photo == null) SlapLog.Warn(LogChannel.UI, "Replay: photo panel (fe-img) missing from the UXML");

            var tiles = Root.Query<VisualElement>(className: "k-tl").ToList();
            for (int i = 0; i < 3 && i < tiles.Count; i++)
            {
                var labels = tiles[i].Query<Label>().ToList();
                if (labels.Count < 2) continue;
                _tileCaption[i] = labels[0];
                _tileValue[i] = labels[1];
            }
            if (tiles.Count < 3) SlapLog.Warn(LogChannel.UI, "Replay: expected 3 stat tiles (k-tl), found " + tiles.Count);

            foreach (var label in Root.Query<Label>().ToList())
            {
                string t = label.text;
                if (t == null) continue;
                if (_caption == null && t.Contains("FINAL BLOW")) _caption = label;
                else if (_impact == null && t.Contains("IMPACT ·")) _impact = label;
            }
            if (_caption == null) SlapLog.Warn(LogChannel.UI, "Replay: header caption (FINAL BLOW) missing from the UXML");
            if (_impact == null) SlapLog.Warn(LogChannel.UI, "Replay: timeline label (IMPACT ·) missing from the UXML");
        }

        protected override void OnShow()
        {
            var shot = Manager.Replay;
            if (shot == null || shot.Photo == null) return; // the front end's D4: the mockup as drawn

            if (_photo != null)
            {
                _photo.style.backgroundImage = shot.Photo is RenderTexture rt
                    ? new StyleBackground(Background.FromRenderTexture(rt))
                    : new StyleBackground(Background.FromTexture2D(shot.Photo as Texture2D));
                _photo.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            }
            if (_caption != null)
                _caption.text = "<cspace=1.4px>ROUND " + shot.Round + (shot.Finisher ? " · FINAL BLOW" : " · LAST SLAP") + "</cspace>";

            SetTile(0, "DAMAGE", shot.Damage + " <cspace=1.26px><size=9>HP</size></cspace>");
            SetTile(1, "POWER", shot.Power + " <cspace=1.26px><size=9>/ 100</size></cspace>");
            SetTile(2, "TIMING", "×" + shot.Multiplier.ToString("0.0") + " <cspace=1.26px><size=9>" + shot.Zone + "</size></cspace>");

            if (_impact != null)
            {
                double t = shot.ImpactAt < 0 ? 0 : shot.ImpactAt;
                int minutes = (int)(t / 60.0);
                double seconds = t - minutes * 60.0;
                _impact.text = "<cspace=1.26px>IMPACT · " + minutes.ToString("00") + ":" + seconds.ToString("00.0") + "</cspace>";
            }
        }

        private void SetTile(int i, string caption, string value)
        {
            if (_tileCaption[i] != null) _tileCaption[i].text = "<cspace=1.12px>" + caption + "</cspace>";
            if (_tileValue[i] != null) _tileValue[i].text = value;
        }

        protected override void OnLink(ScreenId target)
        {
            // Close (-> Knockout) and Next (-> RoundEnd) after the K.O.: on to the result card.
            if (Manager.Replay.EndOfBout && (target == ScreenId.Knockout || target == ScreenId.RoundEnd))
            {
                Manager.FinishReplay();
                return;
            }
            base.OnLink(target);
        }
    }
}
