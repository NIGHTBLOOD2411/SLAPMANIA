using UnityEngine;
using SlapMania.Core;

namespace SlapMania.Data
{
    /// One selectable champion (GLOSSARY: the narrative framing of a fighter): what the roster
    /// card shows and what the bout spawns. Written by the Champion Creator
    /// (Tools > Slap Mania > Characters > Champion Creator) from a rigged FBX; safe to edit by
    /// hand afterwards - the creator loads these values back when you pick the same FBX again.
    [CreateAssetMenu(fileName = "Champion", menuName = "SlapMania/Fighters/Champion Definition")]
    public sealed class ChampionDefinition : ScriptableObject
    {
        [Tooltip("Stable key: saved as the player's pick and used in generated asset names. Never rename a shipped one.")]
        public string id = "";
        public string displayName = "";
        [Tooltip("The champion's universe (GLOSSARY: Universe / Plane), shown on the card.")]
        public string originId = "";
        [Tooltip("Card tag under the overall, e.g. HVY, LGT, TEC.")]
        public string classTag = "";

        [Header("Look")]
        [Tooltip("The roster card portrait. The creator renders one from the model when none is given.")]
        public Texture2D portrait;
        [Tooltip("Card colour: gold (default), red or black - the V2 roster's three card finishes.")]
        public ChampionCardFinish cardFinish = ChampionCardFinish.Gold;

        [Header("Fighter")]
        [Tooltip("The built fighter prefab (root carries a FighterViewBase). Spawned at the bout's fighter marks.")]
        public GameObject fighterPrefab;
        [Tooltip("Animation, IK, hit-spring and knock-out tuning for this rig.")]
        public FighterRigProfile rigProfile;
        [Tooltip("Name prefixes of model props the creator switches off (e.g. sword). Kept so a rebuild hides the same ones.")]
        public string[] hiddenProps = new string[0];

        [Header("Stats (0..99; PWR, CHN and STA change the bout - ADR-0022)")]
        public ChampionStats stats = ChampionStats.Neutral;

        public string ShownName => string.IsNullOrEmpty(displayName) ? name : displayName;
    }

    public enum ChampionCardFinish { Gold = 0, Red = 1, Black = 2 }
}
