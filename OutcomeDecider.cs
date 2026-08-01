using System;
using SmartMarket.Scoring;

namespace SmartMarket.Core
{
    public enum OutcomeLevel
    {
        Excellent,
        Accepted,
        AcceptedWithReservations,
        Rejected
    }

    public static class OutcomeDecider
    {
        // Default thresholds (can be moved to SmartMarketConfig later)
        public static float ExcellentThreshold = 3.0f;
        public static float AcceptedThreshold = 1.0f;
        public static float ReservedThreshold = 0.0f; // >=0 accepted with reservations

        public static OutcomeLevel Decide(ScoreResult result)
        {
            if (result == null) return OutcomeLevel.Rejected;
            float score = result.Total;
            if (score >= ExcellentThreshold) return OutcomeLevel.Excellent;
            if (score >= AcceptedThreshold) return OutcomeLevel.Accepted;
            if (score >= ReservedThreshold) return OutcomeLevel.AcceptedWithReservations;
            return OutcomeLevel.Rejected;
        }
    }
}
