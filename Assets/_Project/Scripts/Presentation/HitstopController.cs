using UnityEngine;

namespace SlapMania.Presentation
{
    /// The freeze-frame on impact (M08) — "the most important 200 ms in the game". Drops
    /// Time.timeScale for a wall-clock duration, then restores it. A watchdog caps the freeze
    /// so time can never stick, and a freeze never sets timeScale to exactly 0 (that stalls
    /// AudioSettings.dspTime on some platforms; the pause below wants exactly that).
    ///
    /// fixedDeltaTime is scaled with timeScale (DEBUG S-042): the knock-out ragdoll launches
    /// inside the freeze, and without this physics would step in coarse jumps and explode when
    /// time resumes. Both are restored together.
    ///
    /// The simulation clock (UnityClock) reads realtime and is intentionally immune to this,
    /// so the FSM's own hitstop timer stays honest while the Animator and physics freeze.
    ///
    /// It is also the bout's only writer of Time.timeScale, so the pause (M01) goes through
    /// here too: SetPaused(true) stops scaled time outright (Animator, physics, SlapIK's
    /// contact timer), a freeze in progress keeps its remaining wall-clock time, and
    /// SetPaused(false) puts back exactly what was running. Two writers would fight: the
    /// hitstop watchdog would restore time in the middle of a pause.
    public sealed class HitstopController : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.05f)] private float _frozenTimeScale = 0.02f;
        [SerializeField, Min(0.05f)] private float _watchdogSeconds = 2f;

        private bool _active;
        private float _restoreAt;
        private float _prevTimeScale = 1f;
        private float _prevFixedDelta = 0.02f;

        private bool _paused;
        private float _pausedAt;
        private float _scaleBeforePause = 1f;

        public bool IsActive => _active;
        public bool IsPaused => _paused;

        public void Apply(float seconds)
        {
            // Nothing lands while paused (the FSM is frozen); never let a freeze un-pause time.
            if (seconds <= 0f || _paused) return;
            if (!_active)
            {
                _prevTimeScale = Time.timeScale;
                _prevFixedDelta = Time.fixedDeltaTime;
            }
            float scale = Mathf.Max(0.001f, _frozenTimeScale);
            Time.timeScale = scale;
            Time.fixedDeltaTime = _prevFixedDelta * scale / Mathf.Max(0.001f, _prevTimeScale);
            _restoreAt = Time.realtimeSinceStartup + Mathf.Min(seconds, _watchdogSeconds);
            _active = true;
        }

        /// Stop (true) or restart (false) scaled time for the pause menu. Idempotent.
        public void SetPaused(bool paused)
        {
            if (paused == _paused) return;
            if (paused)
            {
                _scaleBeforePause = Time.timeScale;
                _pausedAt = Time.realtimeSinceStartup;
                Time.timeScale = 0f;
                _paused = true;
                return;
            }
            _paused = false;
            if (_active) _restoreAt += Time.realtimeSinceStartup - _pausedAt; // the freeze keeps its remaining time
            Time.timeScale = _scaleBeforePause > 0f ? _scaleBeforePause : 1f;
        }

        private void Update()
        {
            if (!_active || _paused) return;
            if (Time.realtimeSinceStartup >= _restoreAt) Restore();
        }

        // Leaving the bout while paused or frozen (forfeit -> menu, scene unload) must never
        // carry a stopped clock into the next scene.
        private void OnDisable()
        {
            if (_paused)
            {
                _paused = false;
                Time.timeScale = _scaleBeforePause > 0f ? _scaleBeforePause : 1f;
            }
            if (_active) Restore();
        }

        private void Restore()
        {
            Time.timeScale = _prevTimeScale <= 0f ? 1f : _prevTimeScale;
            Time.fixedDeltaTime = _prevFixedDelta > 0f ? _prevFixedDelta : 0.02f;
            _active = false;
        }
    }
}
