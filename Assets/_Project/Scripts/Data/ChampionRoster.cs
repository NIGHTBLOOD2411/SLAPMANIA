using UnityEngine;

namespace SlapMania.Data
{
    /// The champions the player can pick (M08, M10 Roster tab), in card order. GameConfig.roster
    /// points at it; the Champion Creator appends to it. A null or empty roster leaves the bout
    /// scene's own fighters in place (the pre-roster prototype).
    [CreateAssetMenu(fileName = "ChampionRoster", menuName = "SlapMania/Fighters/Champion Roster")]
    public sealed class ChampionRoster : ScriptableObject
    {
        public ChampionDefinition[] champions = new ChampionDefinition[0];

        [Tooltip("Every spawned champion is scaled uniformly to this Avatar humanScale (ADR-0023), so all of them fit " +
                 "the slap table the same way. 1.05 is Alucard as modelled: a 1.75 m fighter, head at ~1.64 m. 0 = keep native sizes.")]
        [Min(0f)] public float fighterHumanScale = 1.05f;

        public int Count => champions != null ? champions.Length : 0;

        public ChampionDefinition Get(int index) =>
            champions != null && index >= 0 && index < champions.Length ? champions[index] : null;

        /// Index of the champion with `id`, or -1. Null entries are skipped.
        public int IndexOf(string id)
        {
            if (champions == null || string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < champions.Length; i++)
                if (champions[i] != null && champions[i].id == id) return i;
            return -1;
        }

        /// Every entry is set, has an id and a fighter prefab, and no id repeats.
        public bool Validate(out string error)
        {
            for (int i = 0; i < Count; i++)
            {
                var c = champions[i];
                if (c == null)                         { error = "champions[" + i + "] is null"; return false; }
                if (string.IsNullOrEmpty(c.id))        { error = c.name + " has no id"; return false; }
                if (c.fighterPrefab == null)           { error = c.id + " has no fighterPrefab"; return false; }
                if (IndexOf(c.id) != i)                { error = "id '" + c.id + "' is used twice"; return false; }
            }
            error = null;
            return true;
        }
    }
}
