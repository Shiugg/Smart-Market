using System;
using System.Collections.Generic;
using System.Linq;
using SmartMarket.Customers;

namespace SmartMarket.Scoring
{
    public class EffectRef
    {
        public string EffectId;
        public string EffectName;
        public EffectRef() { }
        public EffectRef(string id, string name) { EffectId = id; EffectName = name; }
    }

    public class DeliveryContext
    {
        public string CustomerId;
        public string PendingProductId;
        public List<EffectRef> RequestedEffects = new List<EffectRef>();
        public string PendingQuality;
        public int PendingQuantity;

        public string ResolvedProductId;
        public string ResolvedProductName;

        public List<EffectRef> DeliveredEffects = new List<EffectRef>();
        public int DeliveredQuantity = 0;
        public string DeliveredQuality = string.Empty;

        public float Price = 0f;
    }

    public class BreakdownEntry
    {
        public string Key;
        public float Value;
        public string Note;
        public BreakdownEntry(string key, float value, string note = null) { Key = key; Value = value; Note = note; }
        public override string ToString() => string.IsNullOrEmpty(Note) ? $"{Key} {Value:0.00}" : $"{Key} {Value:0.00} ({Note})";
    }

    public class ScoreResult
    {
        public float Total = 0f;
        public List<BreakdownEntry> Breakdown = new List<BreakdownEntry>();
        public void Add(string key, float value, string note = null)
        {
            Breakdown.Add(new BreakdownEntry(key, value, note));
            Total += value;
        }
    }

    public static class ScoreEngine
    {
        public static ScoreResult Evaluate(DeliveryContext ctx)
        {
            var result = new ScoreResult();
            try
            {
                // Wrong product
                if (!string.IsNullOrEmpty(ctx.PendingProductId) && !string.IsNullOrEmpty(ctx.ResolvedProductId) && !string.Equals(ctx.PendingProductId, ctx.ResolvedProductId, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add("WrongProduct", -SmartMarket.Core.SmartMarketConfig.WrongProductWeight, null);
                }

                // Under-supply
                if (ctx.PendingQuantity > 0 && ctx.DeliveredQuantity > 0 && ctx.DeliveredQuantity < ctx.PendingQuantity)
                {
                    float fracMissing = (float)(ctx.PendingQuantity - ctx.DeliveredQuantity) / (float)ctx.PendingQuantity;
                    float penal = SmartMarket.Core.SmartMarketConfig.UnderSupplyWeight * fracMissing;
                    result.Add("UnderSupply", -penal, $"{ctx.DeliveredQuantity}/{ctx.PendingQuantity}");
                }

                // Requested effects presence (support multiple)
                foreach (var req in ctx.RequestedEffects)
                {
                    bool matched = false;
                    if (!string.IsNullOrEmpty(req.EffectId))
                    {
                        matched = ctx.DeliveredEffects.Any(d => !string.IsNullOrEmpty(d.EffectId) && string.Equals(d.EffectId, req.EffectId, StringComparison.OrdinalIgnoreCase));
                    }
                    if (!matched && !string.IsNullOrEmpty(req.EffectName))
                    {
                        matched = ctx.DeliveredEffects.Any(d => !string.IsNullOrEmpty(d.EffectName) && string.Equals(d.EffectName, req.EffectName, StringComparison.OrdinalIgnoreCase));
                    }
                    if (matched)
                    {
                        result.Add("RequestedEffect", SmartMarket.Core.SmartMarketConfig.EffectMatchWeight, req.EffectName ?? req.EffectId);
                    }
                }

                // Additional effects: consult customer profile
                var csProfile = CustomerSatisfactionProfile.GetOrCreate(ctx.CustomerId);
                foreach (var ef in ctx.DeliveredEffects)
                {
                    bool isRequested = ctx.RequestedEffects.Any(r => (!string.IsNullOrEmpty(r.EffectId) && !string.IsNullOrEmpty(ef.EffectId) && string.Equals(r.EffectId, ef.EffectId, StringComparison.OrdinalIgnoreCase)) || (!string.IsNullOrEmpty(r.EffectName) && !string.IsNullOrEmpty(ef.EffectName) && string.Equals(r.EffectName, ef.EffectName, StringComparison.OrdinalIgnoreCase)));
                    if (isRequested) continue;

                    if (csProfile != null && csProfile.PreferredEffects != null && csProfile.PreferredEffects.Contains(ef.EffectName))
                    {
                        float add = SmartMarket.Core.SmartMarketConfig.EffectMatchWeight * 0.5f * csProfile.Satisfaction;
                        result.Add($"LikedEffect:{ef.EffectName}", add, null);
                    }
                    else if (csProfile != null && csProfile.DislikedEffects != null && csProfile.DislikedEffects.Contains(ef.EffectName))
                    {
                        float sub = -SmartMarket.Core.SmartMarketConfig.EffectMismatchWeight;
                        result.Add($"DislikedEffect:{ef.EffectName}", sub, null);
                    }
                }

                // Quality
                if (!string.IsNullOrEmpty(ctx.PendingQuality) && !string.IsNullOrEmpty(ctx.DeliveredQuality))
                {
                    if (string.Equals(ctx.PendingQuality, ctx.DeliveredQuality, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add("QualityMatch", SmartMarket.Core.SmartMarketConfig.QualityMatchWeight, null);
                    }
                    else
                    {
                        result.Add("QualityMismatch", -SmartMarket.Core.SmartMarketConfig.QualityMismatchWeight, $"requested:{ctx.PendingQuality} delivered:{ctx.DeliveredQuality}");
                    }
                }

                if (Math.Abs(result.Total) < 0.0001f) result.Total = 0f;
            }
            catch (Exception ex)
            {
                SmartMarket.Core.SmartMarketConfig.LogDebug($"[SmartMarket][SCORE] Evaluation failed: {ex.Message}");
            }

            return result;
        }
    }
}
