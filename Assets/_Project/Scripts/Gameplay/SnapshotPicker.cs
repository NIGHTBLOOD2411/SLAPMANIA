using SlapMania.Core;

namespace SlapMania.Gameplay
{
    /// What one snapshot shows (M09): the defender of a landed slap, and how hard it landed.
    public readonly struct SlapShot
    {
        public readonly int SlapIndex;
        public readonly FighterSlot Defender;
        public readonly int Damage;
        public readonly bool IsLethal;

        public SlapShot(int slapIndex, FighterSlot defender, int damage, bool isLethal)
        {
            SlapIndex = slapIndex;
            Defender = defender;
            Damage = damage;
            IsLethal = isLethal;
        }
    }

    /// The pure rules behind the slap gallery (M09, ADR-0016). Indices refer to a caller-owned
    /// shot array, so the presentation side keeps its photos parallel to it. No allocation.
    public static class SnapshotPicker
    {
        /// The loser's gallery: the lethal slap if there was one, then the loser's hardest other
        /// hits, up to `into.Length`, written in the order they happened. Ties on damage go to the
        /// later slap. Returns how many were written (fewer when the loser took fewer hits).
        public static int PickLoserGallery(SlapShot[] shots, int count, FighterSlot loser, int[] into)
        {
            if (shots == null || into == null || into.Length == 0) return 0;
            if (count > shots.Length) count = shots.Length;
            int n = 0;

            for (int i = 0; i < count; i++)
            {
                if (shots[i].Defender != loser || !shots[i].IsLethal) continue;
                into[n++] = i;
                break;
            }

            while (n < into.Length)
            {
                int best = -1;
                for (int i = 0; i < count; i++)
                {
                    if (shots[i].Defender != loser || Contains(into, n, i)) continue;
                    if (best < 0 || shots[i].Damage > shots[best].Damage
                        || (shots[i].Damage == shots[best].Damage && shots[i].SlapIndex > shots[best].SlapIndex))
                        best = i;
                }
                if (best < 0) break;
                into[n++] = best;
            }

            // Chronological, by slap index (insertion sort: n is tiny).
            for (int i = 1; i < n; i++)
            {
                int v = into[i];
                int j = i - 1;
                while (j >= 0 && shots[into[j]].SlapIndex > shots[v].SlapIndex) { into[j + 1] = into[j]; j--; }
                into[j + 1] = v;
            }
            return n;
        }

        /// The slot to overwrite when the store is full (M09 S-037): the weakest non-lethal shot,
        /// the earlier one on a tie, so the gallery's candidates survive. 0 if all are lethal.
        public static int SlotToEvict(SlapShot[] shots, int count)
        {
            if (shots == null) return 0;
            if (count > shots.Length) count = shots.Length;
            int worst = -1;
            for (int i = 0; i < count; i++)
            {
                if (shots[i].IsLethal) continue;
                if (worst < 0 || shots[i].Damage < shots[worst].Damage
                    || (shots[i].Damage == shots[worst].Damage && shots[i].SlapIndex < shots[worst].SlapIndex))
                    worst = i;
            }
            return worst < 0 ? 0 : worst;
        }

        private static bool Contains(int[] a, int n, int v)
        {
            for (int i = 0; i < n; i++) if (a[i] == v) return true;
            return false;
        }
    }
}
