using UnityEngine;
using SlapMania.Core;

namespace SlapMania.Data
{
    [CreateAssetMenu(fileName = "BoutConfig", menuName = "SlapMania/Balance/Bout Config")]
    public sealed class BoutConfig : ScriptableObject
    {
        [Tooltip("How each slap is played (ADR-0014). TapMarker: mash to charge, tap the ping-pong marker (M03/M04). " +
                 "HoldRing: hold to wind up, release when the closing ring hits gold (V2, HoldRingConfig). " +
                 "Read at every turn start, so switching in Play Mode applies on the next turn. " +
                 "Default TapMarker since 2026-10-23: a tap in HoldRing is an instant release (DEBUG S-090).")]
        public AttackMode attackMode = AttackMode.TapMarker;
        [Range(20, 300)] public int startingHp = 100;
        public bool playerGoesFirst = true;
        [Range(5, 60)] public int maxRounds = 20;
        [Range(20, 100)] public int maxTurns = 40;
        public bool finisherEnabled = true;
        [Tooltip("0 = a fresh seed every bout (logged on the Match channel for replays). Any other value pins every " +
                 "bout to the same AI rolls and marker starts - for reproducing a bug (DEBUG S-094).")]
        public int matchSeed = 0;

        // Durations of the FSM's non-interactive states (M01). Authored in ms (R8) and read by
        // MatchDirector every tick, so edits made in Play Mode apply on the next state entry.
        [Header("Pacing (milliseconds)")]
        [Tooltip("BoutIntro: from StartBout to the first turn.")]
        [Min(0)] public int introMs = 500;
        [Tooltip("Windup: DamageResolved to the impact frame. The most coupled number in the game - see M01 §10.")]
        [Min(0)] public int windupMs = 500;
        [Tooltip("Hitstop state length in the FSM. The visual freeze is FeelConfig.hitstopMs.")]
        [Min(0)] public int hitstopMs = 90;
        [Tooltip("Snapshot: capture window after a non-lethal hit.")]
        [Min(0)] public int snapshotMs = 50;
        [Tooltip("Finisher: how long the lethal slap's finisher plays before BoutEnd.")]
        [Min(0)] public int finisherMs = 2500;
        [Tooltip("FinisherSnapshot: capture window added to the finisher.")]
        [Min(0)] public int finisherSnapshotMs = 100;
        [Tooltip("TurnEnd: breathing room before the other fighter's turn.")]
        [Min(0)] public int turnTransitionMs = 300;
    }
}
