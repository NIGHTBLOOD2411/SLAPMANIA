using UnityEngine;
using SlapMania.Core.Motion;
using SlapMania.Data;

namespace SlapMania.UI
{
    /// Base for every V2 HUD screen: a CanvasGroup fade in/out and a private Tweener ticked on
    /// unscaled time, so animation keeps playing through hitstop (CONVENTIONS §Time). Screens
    /// sequence themselves with Tw.Delay(..., this, id) and handle the id in OnTween — no
    /// coroutines, no closures (R5).
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class HudView : MonoBehaviour, ITweenCallback
    {
        private const int HideDoneId = -1;

        [SerializeField] private CanvasGroup _group;
        [SerializeField, Min(0f)] private float _fadeIn = 0.14f;
        [SerializeField, Min(0f)] private float _fadeOut = 0.18f;

        protected readonly Tweener Tw = new Tweener(32);
        protected HudContext Ctx;

        public bool IsShown { get; private set; }

        protected CanvasGroup Group
        {
            get
            {
                if (_group == null) _group = GetComponent<CanvasGroup>();
                return _group;
            }
        }

        public virtual void Bind(HudContext ctx)
        {
            Ctx = ctx;
            HideImmediate();
        }

        public void Show()
        {
            if (IsShown) return;
            IsShown = true;
            gameObject.SetActive(true);
            Group.blocksRaycasts = true;
            Group.interactable = true;
            Tw.Fade(Group, 0f, ShownAlpha, _fadeIn, Ease.OutQuad);
            OnShow();
        }

        public void Hide()
        {
            if (!IsShown) return;
            IsShown = false;
            Group.blocksRaycasts = false;
            Group.interactable = false;
            OnHide();
            Tw.Fade(Group, Group.alpha, 0f, _fadeOut, Ease.OutQuad).OnComplete(this, HideDoneId);
        }

        public void HideImmediate()
        {
            IsShown = false;
            Tw.KillAll();
            Group.alpha = 0f;
            Group.blocksRaycasts = false;
            Group.interactable = false;
            gameObject.SetActive(false);
        }

        void ITweenCallback.OnTweenComplete(int id)
        {
            if (id == HideDoneId) { if (!IsShown) gameObject.SetActive(false); return; }
            OnTween(id);
        }

        /// The whole screen's alpha once faded in. 1 for most; the scorebug rests lower (POLISH §9).
        protected virtual float ShownAlpha => 1f;

        /// The attention budget (never null once bound).
        protected HudAttentionConfig Budget => Ctx.Attention;

        protected static float Sec(int ms) => HudAttentionConfig.Seconds(ms);

        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
        protected virtual void OnTick(float dt) { }
        protected virtual void OnTween(int id) { }

        // Paused (M01): the whole HUD holds its frame under the pause menu and carries on from
        // it on resume; unscaled time would otherwise play every sequence out behind the menu.
        private void Update()
        {
            if (Ctx != null && Ctx.IsPaused) return;
            float dt = Time.unscaledDeltaTime;
            Tw.Tick(dt);
            if (IsShown && Ctx != null) OnTick(dt);
        }
    }
}
