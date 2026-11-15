using ItemChanger;
using Newtonsoft.Json;
using NightmareAccess.Logic;

namespace NightmareAccess.InGame
{
    public class NightmareAccessTag : Tag
    {
        [JsonProperty] public AccessCondition Condition { get; set; }

        public NightmareAccessTag() { }

        public NightmareAccessTag(AccessCondition condition)
        {
            Condition = condition;
        }
    }
}