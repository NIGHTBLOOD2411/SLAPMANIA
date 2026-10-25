using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SlapMania.Core;
using SlapMania.Core.Motion;
using SlapMania.Gameplay;

namespace SlapMania.UI
{
    /// V2 E2 Victory and E3 Defeat, reporting the bout's real SlapStats. The reference's
    /// economy pieces (division points, coins, crates, level XP) are later systems; their
    /// slots show the stats that exist today, in the same tiles and bars. The defeat coaching
    /// tip is computed from the player's early/late tapping (SlapStats.Tendency).
    ///
    /// Both screens open on the loser's slap gallery (M09): three framed photos of the loser taking
    /// the finisher and their hardest hits, from HudContext.Gallery. Frames with no photo hide.
    public sealed class HudResult : HudView, ITweenSink
    {
        public event Action RematchRequested;
        /// "Main menu": leave the bout for the front end (M10).
        public event Action MenuRequested;

        private const int ChVAccuracy = 0, ChVPower = 1, ChDAccuracy = 2, ChDSpeed = 3, ChDPower = 4;

        [Header("Victory (E2)")]
        [SerializeField] private GameObject _victoryRoot;
        [SerializeField] private RectTransform _vRays;
        [SerializeField] private RectTransform[] _vConfetti = new RectTransform[0];
        [SerializeField] private RectTransform _vTitle;
        [SerializeField] private TMP_Text _vScore;
        [SerializeField] private TMP_Text _vVs;
        [SerializeField] private TMP_Text _vAccuracy;
        [SerializeField] private RectTransform _vAccuracyFill;
        [SerializeField] private TMP_Text _vFooterLeft;
        [SerializeField] private TMP_Text _vFooterRight;
        [SerializeField] private TMP_Text _vPower;
        [SerializeField] private RectTransform _vPowerFill;
        [SerializeField] private TMP_Text _vTileSlaps;
        [SerializeField] private TMP_Text _vTileDamage;
        [SerializeField] private TMP_Text _vTileSpeed;
        [SerializeField] private TMP_Text _chipPerfect;
        [SerializeField] private TMP_Text _chipFinish;
        [SerializeField] private TMP_Text _chipBest;
        [SerializeField] private GameObject _chipFlawless;
        [SerializeField] private CanvasGroup[] _vRows = new CanvasGroup[0];
        [SerializeField] private Button _vButton;
        [SerializeField] private Button _vMenu;

        [Header("Defeat (E3)")]
        [SerializeField] private GameObject _defeatRoot;
        [SerializeField] private RectTransform _dTitle;
        [SerializeField] private TMP_Text _dVs;
        [SerializeField] private TMP_Text _dDelta;
        [SerializeField] private TMP_Text _dAccuracy;
        [SerializeField] private RectTransform _dAccuracyFill;
        [SerializeField] private TMP_Text _dSpeed;
        [SerializeField] private RectTransform _dSpeedFill;
        [SerializeField] private TMP_Text _dPower;
        [SerializeField] private RectTransform _dPowerFill;
        [SerializeField] private TMP_Text _dTip;
        [SerializeField] private CanvasGroup[] _dRows = new CanvasGroup[0];
        [SerializeField] private Button _dButton;
        [SerializeField] private Button _dMenu;

        [Header("Slap gallery (M09): one set per screen, three frames each")]
        [SerializeField] private RectTransform[] _vGalleryFrames = new RectTransform[0];
        [SerializeField] private RawImage[] _vGalleryPhotos = new RawImage[0];
        [SerializeField] private TMP_Text[] _vGalleryDamage = new TMP_Text[0];
        [SerializeField] private RectTransform[] _dGalleryFrames = new RectTransform[0];
        [SerializeField] private RawImage[] _dGalleryPhotos = new RawImage[0];
        [SerializeField] private TMP_Text[] _dGalleryDamage = new TMP_Text[0];

        [Tooltip("Taps per second that fills the CHARGE SPEED bar.")]
        [SerializeField, Min(1f)] private float _fullSpeedTps = 14f;

        private readonly float[] _fillTargets = new float[5];

        private void Awake()
        {
            if (_vButton != null) _vButton.onClick.AddListener(OnRematch);
            if (_dButton != null) _dButton.onClick.AddListener(OnRematch);
            if (_vMenu != null) _vMenu.onClick.AddListener(OnMenu);
            if (_dMenu != null) _dMenu.onClick.AddListener(OnMenu);
        }

        private void OnDestroy()
        {
            if (_vButton != null) _vButton.onClick.RemoveListener(OnRematch);
            if (_dButton != null) _dButton.onClick.RemoveListener(OnRematch);
            if (_vMenu != null) _vMenu.onClick.RemoveListener(OnMenu);
            if (_dMenu != null) _dMenu.onClick.RemoveListener(OnMenu);
        }

        private void OnMenu() => MenuRequested?.Invoke();

        public void Play(in MatchResult r)
        {
            bool won = r.Winner == FighterSlot.Player;
            if (_victoryRoot != null) _victoryRoot.SetActive(won);
            if (_defeatRoot != null) _defeatRoot.SetActive(!won);
            Show();

            var me = Ctx.Stats != null ? Ctx.Stats[FighterSlot.Player] : default;
            int accuracy = Mathf.RoundToInt(me.TimingAccuracy01 * 100f);
            int avgPower = Mathf.RoundToInt(me.AveragePower * 100f);
            if (won) PlayVictory(r, me, accuracy, avgPower);
            else PlayDefeat(me, accuracy, avgPower);
            if (won) PlayGallery(_vGalleryFrames, _vGalleryPhotos, _vGalleryDamage, 0.15f);
            else PlayGallery(_dGalleryFrames, _dGalleryPhotos, _dGalleryDamage, 0.45f);
        }

        private void PlayGallery(RectTransform[] frames, RawImage[] photos, TMP_Text[] damage, float start)
        {
            for (int i = 0; i < frames.Length; i++)
            {
                var frame = frames[i];
                if (frame == null) continue;
                bool has = i < Ctx.GalleryCount && i < Ctx.Gallery.Length && Ctx.Gallery[i] != null;
                frame.gameObject.SetActive(has);
                if (!has) continue;
                if (i < photos.Length && photos[i] != null) photos[i].texture = Ctx.Gallery[i];
                if (i < damage.Length && damage[i] != null) damage[i].SetText("-{0}", Ctx.GalleryDamage[i]);
                frame.localScale = Vector3.zero;   // a delayed tween does not apply its start value
                Tw.Scale(frame, 0f, 1f, 0.35f, Ease.OutBack, start + i * 0.12f);
            }
        }

        private void PlayVictory(in MatchResult r, in SlapStats.Fighter me, int accuracy, int avgPower)
        {
            if (_vRays != null) Tw.RotateZ(_vRays, 0f, 360f, 30f, Ease.Linear).SetLoops(-1);
            for (int i = 0; i < _vConfetti.Length; i++)
            {
                var c = _vConfetti[i];
                if (c == null) continue;
                Tw.RotateZ(c, i * 41f, i * 41f + 360f, 1.6f + i * 0.2f, Ease.Linear).SetLoops(-1);
                Tw.Punch(c, 0.3f, 0.6f, 2, i * 0.07f);
            }
            if (_vTitle != null) Tw.Scale(_vTitle, 2.2f, 1f, 0.45f, Ease.OutBack);

            if (_vScore != null) _vScore.SetText("{0} HP LEFT", Ctx.Director.PlayerHealth.CurrentHp);
            if (_vVs != null) _vVs.text = "VS " + Ctx.OpponentName;
            if (_vAccuracy != null) _vAccuracy.SetText("{0}%", accuracy);
            if (_vFooterLeft != null) _vFooterLeft.SetText("{0} PERFECT HITS", me.Perfects);
            if (_vFooterRight != null) _vFooterRight.SetText("BEST SLAP {0}", me.BestSlap);
            if (_vPower != null) _vPower.SetText("{0}", avgPower);
            if (_vTileSlaps != null) _vTileSlaps.SetText("{0}", me.Slaps);
            if (_vTileDamage != null) _vTileDamage.SetText("{0}", me.TotalDamage);
            if (_vTileSpeed != null) _vTileSpeed.SetText("{0:1}", me.PeakTps);
            if (_chipPerfect != null) _chipPerfect.SetText("{0} PERFECT", me.Perfects);
            if (_chipFinish != null) _chipFinish.text = r.ReachedMaxTurns ? "DECISION" : "K.O.";
            if (_chipBest != null) _chipBest.SetText("BEST {0}", me.BestSlap);
            if (_chipFlawless != null) _chipFlawless.SetActive(me.Flawless);

            Fill(ChVAccuracy, _vAccuracyFill, accuracy / 100f, 0.35f);
            Fill(ChVPower, _vPowerFill, avgPower / 100f, 0.5f);
            Stagger(_vRows, 0.25f);
        }

        private void PlayDefeat(in SlapStats.Fighter me, int accuracy, int avgPower)
        {
            if (_dTitle != null) Tw.Scale(_dTitle, 1.8f, 1f, 0.4f, Ease.OutBack);
            if (_dVs != null) _dVs.text = "VS " + Ctx.OpponentName;
            if (_dDelta != null) _dDelta.SetText("{0} HP LEFT", Ctx.Director.OpponentHealth.CurrentHp);
            if (_dAccuracy != null) _dAccuracy.SetText("{0}%", accuracy);
            if (_dSpeed != null) _dSpeed.SetText("{0:1}/S", me.PeakTps);
            if (_dPower != null) _dPower.SetText("{0}", avgPower);
            if (_dTip != null) WriteTip(me);

            Fill(ChDAccuracy, _dAccuracyFill, accuracy / 100f, 0.35f);
            Fill(ChDSpeed, _dSpeedFill, me.PeakTps / _fullSpeedTps, 0.45f);
            Fill(ChDPower, _dPowerFill, avgPower / 100f, 0.55f);
            Stagger(_dRows, 0.2f);
        }

        private void WriteTip(in SlapStats.Fighter me)
        {
            switch (me.Tendency)
            {
                case TimingTendency.Early:
                    _dTip.SetText("You tapped <b><color=#FFFFFF>early on {0} of {1} slaps</color></b>. Wait for the white ring to touch the gold band.", me.Early, me.Commits);
                    break;
                case TimingTendency.Late:
                    _dTip.SetText("You tapped <b><color=#FFFFFF>late on {0} of {1} slaps</color></b>. Tap as the white ring meets the gold band, not after.", me.Late, me.Commits);
                    break;
                case TimingTendency.NoTap:
                    _dTip.SetText("The ring ran out on <b><color=#FFFFFF>{0} of {1} slaps</color></b>. Tap before the timer stops it for you.", me.TimedOut, me.Commits);
                    break;
                default:
                    _dTip.text = "Solid timing. <b><color=#FFFFFF>Mash faster</color></b> in the charge phase to hit harder.";
                    break;
            }
        }

        private void Fill(int channel, RectTransform bar, float target, float delay)
        {
            if (bar == null) return;
            _fillTargets[channel] = Mathf.Clamp01(target);
            SetFill(channel, 0f);
            Tw.Float(this, channel, 0f, _fillTargets[channel], 0.6f, Ease.OutCubic, delay);
        }

        private void Stagger(CanvasGroup[] rows, float start)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null) continue;
                Tw.Fade(rows[i], 0f, 1f, 0.25f, Ease.OutQuad, start + i * 0.08f);
                var rt = (RectTransform)rows[i].transform;
                Vector2 home = rt.anchoredPosition;
                Tw.AnchorMove(rt, home + new Vector2(0f, -14f), home, 0.3f, Ease.OutCubic, start + i * 0.08f);
            }
        }

        public void OnTweenValue(int channel, float value) => SetFill(channel, value);

        private void SetFill(int channel, float v)
        {
            RectTransform bar = channel switch
            {
                ChVAccuracy => _vAccuracyFill,
                ChVPower => _vPowerFill,
                ChDAccuracy => _dAccuracyFill,
                ChDSpeed => _dSpeedFill,
                _ => _dPowerFill
            };
            if (bar == null) return;
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = new Vector2(Mathf.Clamp01(v), 1f);
            bar.offsetMin = Vector2.zero;
            bar.offsetMax = Vector2.zero;
        }

        private void OnRematch() => RematchRequested?.Invoke();
    }
}
