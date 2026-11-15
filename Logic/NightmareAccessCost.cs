using ItemChanger;
using Newtonsoft.Json;

namespace NightmareAccess.Logic
{
    public record NightmareAccessCost : Cost
    {
        [JsonProperty]
        public AccessCondition Condition { get; init; }

        public NightmareAccessCost() { }

        public NightmareAccessCost(AccessCondition condition)
        {
            Condition = condition;
        }

        public override bool CanPay()
        {
            return Condition != null && Condition.IsSatisfied(out _);
        }

        public override void OnPay()
        {
        }

        public override bool HasPayEffects() => false;

        public override bool Includes(Cost other) => other is NightmareAccessCost;

        public static NightmareAccessCost Find(Cost cost)
        {
            if (cost is NightmareAccessCost nac) return nac;
            if (cost is MultiCost mc)
            {
                foreach (Cost sub in mc)
                {
                    NightmareAccessCost found = Find(sub);
                    if (found != null) return found;
                }
            }
            return null;
        }

        public override string GetCostText()
        {
            if (Condition == null) return "Nightmare Seal";

            bool satisfied = Condition.IsSatisfied(out int current);
            string req = Condition.GetRequirementText();

            if (!satisfied)
            {
                if (Condition.IsProgressive())
                {
                    return $"{req} (have: Lv{current})";
                }
                if (Condition.IsNumeric())
                {
                    return $"{req} (have: {current})";
                }
                return req;
            }

            return req;
        }
    }
}