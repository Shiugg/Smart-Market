using System;
using HarmonyLib;
using MelonLoader;
using MelonLogger = SmartMarket.SmartMarketLogger;
using UnityEngine;
using SmartMarket.Customers;
using SmartMarket.Core;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.ItemFramework;
using System.IO;

namespace SmartMarket.Patches
{
    // Patches to synchronize vanilla customer stats with SmartMarket profiles and
    // to allow profiles to influence customer valuation/acceptance.
    // Purpose: keep AddictionLevel in sync, adjust Satisfaction based on enjoyment
    // and short-circuit counteroffers when profile strongly dislikes a product.
    // Risks: Reflection fallbacks used when properties/methods differ between builds.
    [HarmonyPatch]
    public static class CustomerBehaviorPatches
    {
        // ChangeAddiction Postfix: sync profile.AddictionLevel with vanilla value.
        [HarmonyPatch(typeof(Customer), nameof(Customer.ChangeAddiction))]
        public static class ChangeAddiction_Postfix
        {
            public static void Postfix(Customer __instance)
            {
                try
                {
                    if (__instance == null) return;
                    var profile = CustomerSatisfactionProfile.GetOrCreate(GetCustomerId(__instance));

                    float currentAddiction = 0f;
                    try
                    {
                        // Prefer property if available
                        currentAddiction = __instance.CurrentAddiction;
                    }
                    catch
                    {
                        // Fallback: try reflection for getter method
                        try
                        {
                            var m = __instance.GetType().GetMethod("get_CurrentAddiction");
                            if (m != null) currentAddiction = (float)m.Invoke(__instance, null);
                        }
                        catch { }
                    }

                    // Normalize if needed: assume vanilla addiction is 0-10, map to 0-1
                    if (currentAddiction > 1.5f)
                        profile.AddictionLevel = Mathf.Clamp01(currentAddiction / 10f);
                    else
                        profile.AddictionLevel = Mathf.Clamp01(currentAddiction);

                    profile.Save();

                    SmartMarketConfig.LogDebug($"[ChangeAddiction] Synced addiction for {profile.CustomerID}: {profile.AddictionLevel}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SmartMarket] ChangeAddiction postfix error: {ex.Message}");
                }
            }
        }

        // GetProductEnjoyment Postfix: small immediate adjustment to satisfaction when enjoyment is evaluated.
        // Patch BOTH overloads explicitly to avoid ambiguous match error
        [HarmonyPatch(typeof(Customer), "GetProductEnjoyment", new[] { typeof(ProductDefinition), typeof(EQuality) })]
        public static class GetProductEnjoyment_WithQuality_Postfix
        {
            public static void Postfix(Customer __instance, ProductDefinition product, EQuality quality, ref float __result)
            {
                try
                {
                    if (__instance == null || product == null) return;
                    var profile = CustomerSatisfactionProfile.GetOrCreate(GetCustomerId(__instance));

                    // __result is expected 0..1 or similar; adjust satisfaction slightly
                    if (__result >= 0.7f)
                    {
                        profile.Satisfaction = Mathf.Clamp01(profile.Satisfaction + 0.03f);
                        profile.ConsecutiveGoodDeals = 0;
                    }
                    else if (__result <= 0.35f)
                    {
                        profile.Satisfaction = Mathf.Clamp01(profile.Satisfaction - 0.05f);
                        profile.ConsecutiveBadDeals++;
                    }

                    profile.Save();
                    SmartMarketConfig.LogDebug($"[GetProductEnjoyment] {profile.CustomerID} enjoyment={__result:0.00} -> satisfaction={profile.Satisfaction:0.00}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SmartMarket] GetProductEnjoyment postfix error: {ex.Message}");
                }
            }
        }

        // Patch second overload without EQuality parameter
        [HarmonyPatch(typeof(Customer), "GetProductEnjoyment", new[] { typeof(ProductDefinition) })]
        public static class GetProductEnjoyment_NoQuality_Postfix
        {
            public static void Postfix(Customer __instance, ProductDefinition product, ref float __result)
            {
                try
                {
                    if (__instance == null || product == null) return;
                    var profile = CustomerSatisfactionProfile.GetOrCreate(GetCustomerId(__instance));

                    if (__result >= 0.7f)
                    {
                        profile.Satisfaction = Mathf.Clamp01(profile.Satisfaction + 0.03f);
                        profile.ConsecutiveGoodDeals = 0;
                    }
                    else if (__result <= 0.35f)
                    {
                        profile.Satisfaction = Mathf.Clamp01(profile.Satisfaction - 0.05f);
                        profile.ConsecutiveBadDeals++;
                    }

                    profile.Save();
                    SmartMarketConfig.LogDebug($"[GetProductEnjoyment] {profile.CustomerID} enjoyment={__result:0.00} -> satisfaction={profile.Satisfaction:0.00}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SmartMarket] GetProductEnjoyment postfix error: {ex.Message}");
                }
            }
        }

        // EvaluateCounteroffer Prefix: use profile to potentially reject counteroffers early
        [HarmonyPatch(typeof(Customer), "EvaluateCounteroffer")]
        public static class EvaluateCounteroffer_Prefix
        {
            // Signature: protected virtual bool EvaluateCounteroffer(ProductDefinition product, int quantity, float price)
            public static bool Prefix(Customer __instance, ProductDefinition product, int quantity, float price, ref bool __result)
            {
                try
                {
                    if (__instance == null || product == null) return true; // continue to original
                    var profile = CustomerSatisfactionProfile.GetOrCreate(GetCustomerId(__instance));

                    // Build a simple score using SmartMarketConfig weights
                    float score = 0f;

                    // Effects: check product effects (best-effort)
                    var effects = CustomerSatisfactionProfile.ExtractProductEffects(product);
                    foreach (var eff in effects)
                    {
                        if (profile.PreferredEffects.Contains(eff)) score += SmartMarketConfig.EffectMatchWeight * profile.Satisfaction;
                        if (profile.DislikedEffects.Contains(eff)) score -= SmartMarketConfig.EffectMismatchWeight;
                    }

                    // Quality: check product quality vs min accepted (best-effort: quality passed via product or use default)
                    EQuality prodQuality = EQuality.Standard;
                    try
                    {
                        var ptype = product.GetType();
                        var qprop = ptype.GetProperty("Quality");
                        if (qprop != null)
                        {
                            var qv = qprop.GetValue(product);
                            if (qv is EQuality) prodQuality = (EQuality)qv;
                            else if (qv is int) prodQuality = (EQuality)((int)qv);
                        }
                        else
                        {
                            var m = ptype.GetMethod("get_Quality");
                            if (m != null)
                            {
                                var qv = m.Invoke(product, null);
                                if (qv is EQuality) prodQuality = (EQuality)qv;
                                else if (qv is int) prodQuality = (EQuality)((int)qv);
                            }
                        }
                    }
                    catch { }

                    if (prodQuality >= profile.MinAcceptedQuality)
                        score += SmartMarketConfig.QualityMatchWeight;
                    else
                        score -= SmartMarketConfig.QualityMismatchWeight * (1f - profile.Trust);

                    // Addiction/trust/satisfaction factors
                    score += profile.AddictionLevel * SmartMarketConfig.AddictionWeight;
                    score += profile.Trust * SmartMarketConfig.TrustWeight;
                    score += profile.Satisfaction * SmartMarketConfig.SatisfactionWeight;

                    // Check pending requested info from MemorySystem: penalize if wrong product or under-supplied
                    try
                    {
                        var mem = SmartMarket.Core.MemorySystem.GetMemory(profile.CustomerID);
                        if (mem != null)
                        {
                            // Wrong product penalty (if memory has a requested product id and it doesn't match offered product)
                            if (!string.IsNullOrEmpty(mem.PendingRequestedProductId))
                            {
                                string offeredId = product.SaveFileName ?? product.name;
                                if (!string.Equals(offeredId, mem.PendingRequestedProductId, StringComparison.OrdinalIgnoreCase) &&
                                    !string.Equals(product.name, mem.PendingRequestedProductId, StringComparison.OrdinalIgnoreCase))
                                {
                                    score -= SmartMarketConfig.WrongProductWeight;
                                    SmartMarketConfig.LogDebug($"[EvaluateCounteroffer] Applied WrongProductWeight penalty ({SmartMarketConfig.WrongProductWeight}) for {profile.CustomerID} (offered:{offeredId} expected:{mem.PendingRequestedProductId})");
 
                                    try
                                    {
                                        var msg = $"{profile.CustomerID}: Me vendiste {product?.name} en vez de lo que pedí. No me falles.";
                                        SmartMarket.Core.PhoneMessenger.SendMessageFromCustomer(profile.CustomerID, msg);
                                    }
                                    catch { }
                                }
                            }
 
                            if (!string.IsNullOrEmpty(mem.PendingRequestedEffectId) || !string.IsNullOrEmpty(mem.PendingRequestedEffectName))
                            {
                                var productEffects = MessageContextBuilder.ExtractProductEffects(product);
                                bool requestedEffectMatched = false;
                                bool matchedById = false;
                                 
                                if (!string.IsNullOrEmpty(mem.PendingRequestedEffectId))
                                {
                                    foreach (var eff in productEffects)
                                    {
                                        if (!string.IsNullOrEmpty(eff.EffectId) && string.Equals(eff.EffectId, mem.PendingRequestedEffectId, StringComparison.OrdinalIgnoreCase))
                                        {
                                            requestedEffectMatched = true;
                                            matchedById = true;
                                            break;
                                        }
                                    }
                                }
 
                                if (!requestedEffectMatched && !string.IsNullOrEmpty(mem.PendingRequestedEffectName))
                                {
                                    foreach (var eff in productEffects)
                                    {
                                        if (!string.IsNullOrEmpty(eff.EffectName) && string.Equals(eff.EffectName, mem.PendingRequestedEffectName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            requestedEffectMatched = true;
                                            break;
                                        }
                                    }
                                }
 
                                if (!requestedEffectMatched)
                                {
                                    score -= SmartMarketConfig.EffectMismatchWeight;
                                    SmartMarketConfig.LogDebug($"[EvaluateCounteroffer] Requested effect mismatch for {profile.CustomerID}: expectedId='{mem.PendingRequestedEffectId}' expectedName='{mem.PendingRequestedEffectName}' offeredEffects='[{string.Join(",", productEffects.ConvertAll(e => e.DisplayName))}]'");
                                }
                                else
                                {
                                    score += SmartMarketConfig.EffectMatchWeight * profile.Satisfaction;
                                    SmartMarketConfig.LogDebug($"[EvaluateCounteroffer] Requested effect matched for {profile.CustomerID}: expectedId='{mem.PendingRequestedEffectId}' expectedName='{mem.PendingRequestedEffectName}' matchedById={matchedById}");
                                }
                            }
 
                            if (!string.IsNullOrEmpty(mem.PendingRequestedQuality))
                            {
                                string offeredQuality = GetProductQualityName(product);
                                if (!string.IsNullOrEmpty(offeredQuality))
                                {
                                    if (!string.Equals(mem.PendingRequestedQuality, offeredQuality, StringComparison.OrdinalIgnoreCase))
                                    {
                                        float pen = SmartMarketConfig.QualityMismatchWeight * (1f - profile.Trust);
                                        score -= pen;
                                        SmartMarketConfig.LogDebug($"[EvaluateCounteroffer] Requested quality mismatch for {profile.CustomerID}: requested='{mem.PendingRequestedQuality}' offered='{offeredQuality}' penalty={pen:0.00}");
                                    }
                                    else
                                    {
                                        score += SmartMarketConfig.QualityMatchWeight;
                                        SmartMarketConfig.LogDebug($"[EvaluateCounteroffer] Requested quality matched for {profile.CustomerID}: '{offeredQuality}'");
                                    }
                                }
                            }
 
                            // Under-supply penalty (if memory recorded requested quantity and offered quantity is lower)
                            if (mem.PendingRequestedQuantity > 0 && quantity < mem.PendingRequestedQuantity)
                            {
                                    // proportional penalty based on fraction missing
                                    float fracMissing = (mem.PendingRequestedQuantity - quantity) / (float)mem.PendingRequestedQuantity;
                                    float pen = SmartMarketConfig.UnderSupplyWeight * Mathf.Clamp01(fracMissing);
                                    score -= pen;
                                    SmartMarketConfig.LogDebug($"[EvaluateCounteroffer] Applied UnderSupplyWeight penalty ({pen:0.00}) for {profile.CustomerID} (offeredQty:{quantity} requested:{mem.PendingRequestedQuantity})");
 
                                    try
                                    {
                                        var msg = $"{profile.CustomerID}: Me ofreciste {quantity} en vez de {mem.PendingRequestedQuantity}. Esto me desagrada.";
                                        SmartMarket.Core.PhoneMessenger.SendMessageFromCustomer(profile.CustomerID, msg);
                                    }
                                    catch { }
                            }
                        }
                    }
                    catch { }

                    SmartMarketConfig.LogDebug($"[EvaluateCounteroffer] {profile.CustomerID} score={score:0.00} for product '{product?.name}' price={price}");

                    // Additionally write a compact scores log for easy extraction (only when debug enabled)
                    try
                    {
                        if (SmartMarket.Core.SmartMarketConfig.DebugEnabled)
                        {
                            var logPath = Path.Combine(Application.persistentDataPath, "SmartMarket_scores.log");
                            var line = $"{DateTime.Now:O}\t{profile.CustomerID}\t{product?.name}\t{price:0.00}\t{score:0.00}\n";
                            File.AppendAllText(logPath, line);
                        }
                    }
                    catch { }

                    // Threshold heuristic: if score very low, reject immediately; if very high, accept immediately
                    if (score < 0.3f)
                    {
                                            // Apply small conservative penalty so player can notice the change in UI
                                            try
                                            {
                                                profile.Satisfaction = Mathf.Clamp01(profile.Satisfaction - 0.05f);
                                                profile.Trust = Mathf.Clamp01(profile.Trust - 0.03f);
                                                profile.Save();

                                                // Write an event entry
                                                try
                                                {
                                                    var eventsPath = Path.Combine(Application.persistentDataPath, "SmartMarket_events.log");
                                                    var line = $"{DateTime.Now:O}\tREJECT_LOW_SCORE\t{profile.CustomerID}\tproduct={product?.name}\tprice={price:0.00}\tscore={score:0.00}\tSatisfaction={profile.Satisfaction:0.00}\tTrust={profile.Trust:0.00}\n";
                                                    File.AppendAllText(eventsPath, line);
                                                }
                                                catch { }

                                                SmartMarketConfig.LogDebug($"[EvaluateCounteroffer] Applied penalty to {profile.CustomerID}: SAT-0.05 TRUST-0.03");

                                                // Try updating vanilla customer object so UI bars reflect change
                                                try
                                                {
                                                    TryUpdateVanillaRelationship(__instance, profile);
                                                }
                                                catch { }
                                            }
                                            catch (Exception ex)
                                            {
                                                MelonLogger.Warning($"[SmartMarket] Error applying reject deltas: {ex.Message}");
                                            }

                                            __result = false;
                                            return false; // skip original
                                        }

                                        if (score > 2.5f)
                                        {
                                            __result = true;
                                            return false; // skip original and accept
                                        }

                                        // Otherwise, let original method run
                                        return true;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SmartMarket] EvaluateCounteroffer prefix error: {ex.Message}");
                    return true;
                }
            }
        }

        // Helper to get stable customer id (prefer GameObject name)
        private static string GetCustomerId(Customer customer)
        {
            try
            {
                if (customer.gameObject != null && !string.IsNullOrEmpty(customer.gameObject.name))
                    return customer.gameObject.name;
            }
            catch { }
            try
            {
                // fallback: try an 'ID' property
                var prop = customer.GetType().GetProperty("ID");
                if (prop != null) return prop.GetValue(customer)?.ToString() ?? "unknown";
            }
            catch { }
            return "unknown_customer";
        }
 
        private static string GetProductQualityName(ProductDefinition product)
        {
            if (product == null)
                return string.Empty;

            try
            {
                var type = product.GetType();
                var prop = type.GetProperty("Quality");
                if (prop != null)
                {
                    var value = prop.GetValue(product);
                    if (value != null)
                    {
                        var qualityName = value.ToString();
                        if (string.Equals(qualityName, "Standard", StringComparison.OrdinalIgnoreCase))
                            return string.Empty;
                        return char.ToUpperInvariant(qualityName[0]) + qualityName.Substring(1);
                    }
                }
            }
            catch { }

            return string.Empty;
        }
 
        // Try to update vanilla customer relationship/trust fields so the UI bars reflect profile changes.
        // This is best-effort: it tries several common property/method names via reflection and logs failures.
        public static void TryUpdateVanillaRelationship(Customer customer, Customers.CustomerSatisfactionProfile profile)
        {
            if (customer == null || profile == null) return;
            try
            {
                // Map profile.Trust (0..1) to likely vanilla scales. Try float setter first.
                float normalized = Mathf.Clamp01(profile.Trust);

                var type = customer.GetType();
                bool updated = false;

                // Try common property names
                string[] floatProps = new[] { "CurrentRelationship", "Relationship", "Trust", "Loyalty", "RelationshipNormalized" };
                foreach (var pn in floatProps)
                {
                    try
                    {
                        var prop = type.GetProperty(pn);
                        if (prop != null && prop.CanWrite)
                        {
                            var ptype = prop.PropertyType;
                            if (ptype == typeof(float)) { prop.SetValue(customer, normalized); updated = true; break; }
                            if (ptype == typeof(double)) { prop.SetValue(customer, (double)normalized); updated = true; break; }
                            if (ptype == typeof(int)) { prop.SetValue(customer, (int)Mathf.Round(normalized * 100f)); updated = true; break; }
                        }
                    }
                    catch { }
                }

                if (!updated)
                {
                    // Try setter method patterns: SetRelationship(float) or SetTrust(float)
                    string[] methodNames = new[] { "SetRelationship", "SetTrust", "UpdateRelationship", "SetLoyalty" };
                    foreach (var mn in methodNames)
                    {
                        try
                        {
                            var m = type.GetMethod(mn);
                            if (m != null)
                            {
                                var pars = m.GetParameters();
                                if (pars.Length == 1 && pars[0].ParameterType == typeof(float)) { m.Invoke(customer, new object[] { normalized }); updated = true; break; }
                                if (pars.Length == 1 && pars[0].ParameterType == typeof(double)) { m.Invoke(customer, new object[] { (double)normalized }); updated = true; break; }
                                if (pars.Length == 1 && pars[0].ParameterType == typeof(int)) { m.Invoke(customer, new object[] { (int)Mathf.Round(normalized * 100f) }); updated = true; break; }
                            }
                        }
                        catch { }
                    }
                }

                if (!updated)
                {
                    // Last resort: try to find any numeric field and set it heuristically
                    foreach (var field in type.GetFields())
                    {
                        try
                        {
                            if (field.FieldType == typeof(float)) { field.SetValue(customer, normalized); updated = true; break; }
                            if (field.FieldType == typeof(double)) { field.SetValue(customer, (double)normalized); updated = true; break; }
                            if (field.FieldType == typeof(int)) { field.SetValue(customer, (int)Mathf.Round(normalized * 100f)); updated = true; break; }
                        }
                        catch { }
                    }
                }

                if (updated)
                    SmartMarketConfig.LogDebug($"[VanillaInterop] Updated vanilla relationship for {GetCustomerId(customer)} -> {normalized:0.00}");
                else
                    SmartMarketConfig.LogDebug($"[VanillaInterop] Could not find a writable relationship-like member on Customer {GetCustomerId(customer)}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] TryUpdateVanillaRelationship failed: {ex.Message}");
            }
        }
    }
}
