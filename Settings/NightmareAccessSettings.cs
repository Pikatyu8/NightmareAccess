namespace NightmareAccess.Settings
{
    public enum RequirementScope
    {
        PerLocation,
        PerObjectGroup,
        PerObject
    }

    public class NightmareAccessSettings
    {
        public bool Enabled { get; set; } = false;
        public RequirementScope Scope { get; set; } = RequirementScope.PerLocation;
        public int RequirementChance { get; set; } = 100;

        /// <summary>Copies every public setting into this instance so object identity (menu closures) stays valid.</summary>
        public void CopyFrom(NightmareAccessSettings other)
        {
            if (other == null || ReferenceEquals(other, this)) return;

            foreach (var p in typeof(NightmareAccessSettings).GetProperties(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
                {
                    p.SetValue(this, p.GetValue(other, null), null);
                }
            }
        }

        /// <summary>Fixes values that would make System.Random throw (inverted Min/Max, chance out of range).</summary>
        public void Normalize()
        {
            RequirementChance = System.Math.Max(0, System.Math.Min(100, RequirementChance));

            var type = typeof(NightmareAccessSettings);
            foreach (var minProp in type.GetProperties(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (minProp.PropertyType != typeof(int)
                    || !minProp.Name.StartsWith("Min", System.StringComparison.Ordinal)) continue;

                var maxProp = type.GetProperty("Max" + minProp.Name.Substring(3));
                if (maxProp == null || maxProp.PropertyType != typeof(int)) continue;

                int min = (int)minProp.GetValue(this, null);
                int max = (int)maxProp.GetValue(this, null);
                if (min > max)
                {
                    minProp.SetValue(this, max, null);
                    maxProp.SetValue(this, min, null);
                }
            }
        }

        // Currencies & Basic Resources
        public bool AllowGeo { get; set; } = true;
        public int MinGeo { get; set; } = 50;
        public int MaxGeo { get; set; } = 1200;

        public bool AllowEggs { get; set; } = true;
        public int MinEggs { get; set; } = 1;
        public int MaxEggs { get; set; } = 10;

        public bool AllowEssence { get; set; } = true;
        public int MinEssence { get; set; } = 100;
        public int MaxEssence { get; set; } = 1200;

        public bool AllowGrubs { get; set; } = true;
        public int MinGrubs { get; set; } = 1;
        public int MaxGrubs { get; set; } = 25;

        public bool AllowRoots { get; set; } = true;
        public int MinRoots { get; set; } = 1;
        public int MaxRoots { get; set; } = 10;

        public bool AllowSimpleKeys { get; set; } = true;
        public int MinSimpleKeys { get; set; } = 1;
        public int MaxSimpleKeys { get; set; } = 3;

        public bool AllowMaskShards { get; set; } = true;
        public int MinMaskShards { get; set; } = 1;
        public int MaxMaskShards { get; set; } = 8;

        public bool AllowVesselFragments { get; set; } = true;
        public int MinVesselFragments { get; set; } = 1;
        public int MaxVesselFragments { get; set; } = 5;

        public bool AllowPaleOre { get; set; } = true;
        public int MinPaleOre { get; set; } = 1;
        public int MaxPaleOre { get; set; } = 4;

        public bool AllowNotches { get; set; } = true;
        public int MinNotches { get; set; } = 1;
        public int MaxNotches { get; set; } = 4;

        public bool AllowRelics { get; set; } = true;
        public int MinRelics { get; set; } = 1;
        public int MaxRelics { get; set; } = 6;

        // Movement Abilities (Individually separated)
        public bool AllowDash { get; set; } = true;
        public bool AllowClaw { get; set; } = true;
        public bool AllowSuperdash { get; set; } = true;
        public bool AllowDoubleJump { get; set; } = true;
        public bool AllowAcidArmour { get; set; } = true;

        // Nail & Arts
        public bool AllowDreamNail { get; set; } = true;
        public bool AllowNailArts { get; set; } = true;

        // Spells (Individually separated)
        public bool AllowFireball { get; set; } = true;
        public bool AllowQuake { get; set; } = true;
        public bool AllowScream { get; set; } = true;

        // Key Items
        public bool AllowCityCrest { get; set; } = true;
        public bool AllowLantern { get; set; } = true;

        // Special Chains & Dreamers
        public bool AllowDreamers { get; set; } = true;
        public bool AllowKingsoul { get; set; } = true;
        public bool AllowGrimmchild { get; set; } = true;

        // Collectibles with Master & Sub toggles
        public bool AllowCharms { get; set; } = true;
        public bool AllowSpecificCharms { get; set; } = true;
        public bool AllowCharmsCount { get; set; } = true;

        public bool AllowMaps { get; set; } = true;
        public bool AllowSpecificMaps { get; set; } = true;
        public bool AllowMapsCount { get; set; } = true;

        public bool AllowJournal { get; set; } = true;
        public bool AllowSpecificJournal { get; set; } = true;
        public bool AllowJournalCount { get; set; } = true;

        public bool AllowStags { get; set; } = true;
        public bool AllowSpecificStags { get; set; } = true;
        public bool AllowStagsCount { get; set; } = true;
    }
}