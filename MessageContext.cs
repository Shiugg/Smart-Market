using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Product;

namespace SmartMarket.Core
{
    public enum NeighborhoodStandard
    {
        Marginal,
        Normal,
        High
    }

    public class MessageContext
    {
        public class PreferenceProfile
        {
            public float PreferenceNovelty = 0.5f; // 0-1, tendency to seek new products
            public float SubstitutionTolerance = 0.5f; // 0-1, willingness to accept substitutes
            public float Urgency = 0.5f; // 0-1, how urgent the request is
            public float QualityBias = 0.5f; // 0-1, preference for high quality
        }

        public string CustomerName;
        public ConsumerProfile Profile;
        public Neighborhood Neighborhood;
        public NeighborhoodStandard NeighborhoodStandard;
        public string RequestedProductId;
        public string RequestedProductName;
        public string RequestedEffect;
        public string RequestedEffectId;
        public string RequestedQuality;
        public ProductDefinition RequestedProductDefinition;
        public int RequestedQuantity;
        public bool IsWordOfMouth;
        public string WordOfMouthSource; // nombre del NPC que recomendó
        public bool IsRepeatRequest;
        public bool IsEffectDriven;
        public bool IsUrgent;
        public bool HasGoodQualityFocus;
        public bool RejectsCounterOffers;
        public string MotivationReason;

        public PreferenceProfile Preferences = new PreferenceProfile();
    }
}
