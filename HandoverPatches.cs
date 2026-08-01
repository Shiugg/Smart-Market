using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using MelonLogger = SmartMarket.SmartMarketLogger;
using SmartMarket.Core;
using SmartMarket.Customers;
using UnityEngine;
using SmartMarket.Scoring;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;

namespace SmartMarket.Patches
{
    // Dynamic patch: locate the game's ProcessHandoverServerSide method at runtime and attach a Postfix
    [HarmonyPatch]
    public static class ProcessHandoverServerSide_Patch
    {
        // Find the target method by name and parameter count to be resilient across game versions
        public static MethodBase TargetMethod()
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types = null;
                    try { types = asm.GetTypes(); } catch { continue; }
                    foreach (var t in types)
                    {
                        if (t == null) continue;
                        var methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                        foreach (var m in methods)
                        {
                            if (m == null) continue;
                            if (m.Name != "ProcessHandoverServerSide") continue;
                            // prefer the overload that matches our expected parameter count
                            var ps = m.GetParameters();
                            if (ps.Length >= 6)
                            {
                                MelonLogger.Msg($"[SmartMarket] Patching method: {t.FullName}.{m.Name}");
                                return m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] Error finding ProcessHandoverServerSide target: {ex.Message}");
            }
            MelonLogger.Warning("[SmartMarket] ProcessHandoverServerSide method not found for patching.");
            return null;
        }

        // Postfix receives the original instance and argument array so we can be flexible with signatures
        public static void Postfix(object __instance, MethodBase __originalMethod, object[] __args)
        {
            try
            {
                SmartMarketConfig.LogDebug("[SmartMarket] ProcessHandoverServerSide_Patch.Postfix triggered");

                // Attempt to extract arguments by convention: (EHandoverOutcome outcome, List items, bool handoverByPlayer, float totalPayment, ProductList productList, float satisfaction, NetworkObject dealerObject)
                object outcome = null;
                object items = null;
                object productList = null;
                object dealerObject = null;

                if (__args != null && __args.Length > 0)
                {
                    if (__args.Length >= 1) outcome = __args[0];
                    if (__args.Length >= 2) items = __args[1];
                    if (__args.Length >= 5) productList = __args[4];
                    if (__args.Length >= 6) { try { dealerObject = __args[6]; } catch { dealerObject = null; } }
                }

                string customerName = null;

                // Try to resolve customer name from __instance if it's a Customer
                try
                {
                    if (__instance != null)
                    {
                        var itype = __instance.GetType();
                        // Common case: method is an instance method on Customer
                        if (itype.Name.IndexOf("Customer", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            try
                            {
                                var goProp = itype.GetProperty("gameObject");
                                if (goProp != null)
                                {
                                    var go = goProp.GetValue(__instance);
                                    if (go != null)
                                    {
                                        var nameProp = go.GetType().GetProperty("name");
                                        if (nameProp != null)
                                        {
                                            customerName = nameProp.GetValue(go) as string;
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                // Fallback: try to read a "npcToRecommend" or similar from productList or outcome
                if (string.IsNullOrEmpty(customerName))
                {
                    try
                    {
                        if (productList != null)
                        {
                            var t = productList.GetType();
                            var npcProp = t.GetProperty("npcToRecommend") ?? t.GetProperty("CustomerID") ?? t.GetProperty("CustomerName") ?? t.GetProperty("NpcName");
                            if (npcProp != null)
                            {
                                var val = npcProp.GetValue(productList);
                                if (val != null) customerName = val.ToString();
                            }
                        }
                    }
                    catch { }
                }

                // Another fallback: if dealerObject is present and contains a Dealer with AssignedCustomer, try to find
                if (string.IsNullOrEmpty(customerName) && dealerObject != null)
                {
                    try
                    {
                        var dType = dealerObject.GetType();
                        var goProp = dType.GetProperty("gameObject");
                        if (goProp != null)
                        {
                            var go = goProp.GetValue(dealerObject);
                            if (go != null)
                            {
                                var nameProp = go.GetType().GetProperty("name");
                                if (nameProp != null)
                                {
                                    // Dealer gameObject name might be the dealer name, not the customer
                                    // leave it as a fallback marker
                                    customerName = nameProp.GetValue(go) as string;
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (string.IsNullOrEmpty(customerName))
                {
                    MelonLogger.Msg("HANDOVER", "Could not resolve customer name for handover. Skipping effect-based validation.");
                    return;
                }

                SmartMarketConfig.LogDebug($"[SmartMarket] Handover for customer: {customerName}");

                // Retrieve pending request info for this customer
                var mem = MemorySystem.GetMemory(customerName);
                if (mem == null)
                {
                    MelonLogger.Msg($"[SmartMarket][SM-DIAG] No memory entry for customer {customerName}. Skipping.");
                    return;
                }

                string pendingEffectId = mem.PendingRequestedEffectId ?? string.Empty;
                string pendingEffectName = mem.PendingRequestedEffectName ?? string.Empty;
                string pendingProductId = mem.PendingRequestedProductId ?? string.Empty;
                string pendingQuality = mem.PendingRequestedQuality ?? string.Empty;
                int pendingQuantity = mem.PendingRequestedQuantity;

                // Build diagnostic header
                MelonLogger.Msg($"[SM-DIAG] Handover detected for NPC: {customerName}");
                MelonLogger.Msg($"[SM-DIAG] Pending requested product: {pendingProductId} qty:{pendingQuantity} quality:{pendingQuality}");
                MelonLogger.Msg($"[SM-DIAG] Pending requested effect: {pendingEffectName} (ID={pendingEffectId})");

                // Extract delivered product(s) and their effects
                var deliveredEffects = new List<(string Id, string Name)>();
                string resolvedProductName = "<unknown>";
                string resolvedProductId = "";
                float price = 0f;
                int deliveredQuantity = 0;
                string deliveredQuality = string.Empty;

                try
                {
                    // If productList is provided (likely better representation), inspect its entries
                    if (productList != null)
                    {
                        try
                        {
                            var t = productList.GetType();
                            FieldInfo entriesFieldInfo = t.GetField("entries");
                            PropertyInfo entriesPropInfo = t.GetProperty("entries");
                            object entries = null;

                            if (entriesFieldInfo != null)
                            {
                                try { entries = entriesFieldInfo.GetValue(productList); } catch { entries = null; }
                            }
                            else if (entriesPropInfo != null)
                            {
                                try { entries = entriesPropInfo.GetValue(productList); } catch { entries = null; }
                            }

                            if (entries != null)
                            {
                                // entries is likely an Il2Cpp List of ProductList.Entry
                                var listType = entries.GetType();
                                var countProp = listType.GetProperty("Count");
                                var itemProp = listType.GetProperty("Item");
                                if (countProp != null && itemProp != null)
                                {
                                    int count = 0; try { count = (int)countProp.GetValue(entries); } catch { }
                                    if (count > 0)
                                    {
                                        var first = itemProp.GetValue(entries, new object[] { 0 });
                                        if (first != null)
                                        {
                                            try
                                            {
                                                var prodIdProp = first.GetType().GetProperty("ProductID") ?? first.GetType().GetProperty("productId");
                                                if (prodIdProp != null) resolvedProductId = prodIdProp.GetValue(first)?.ToString() ?? "";
                                                var qtyProp = first.GetType().GetProperty("Quantity");
                                                if (qtyProp != null)
                                                {
                                                    try { deliveredQuantity = Convert.ToInt32(qtyProp.GetValue(first)); } catch { }
                                                }
                                                var qProp = first.GetType().GetProperty("Quality");
                                                if (qProp != null)
                                                {
                                                    try { deliveredQuality = qProp.GetValue(first)?.ToString() ?? deliveredQuality; } catch { }
                                                }
                                                // Try resolve product definition
                                                if (!string.IsNullOrEmpty(resolvedProductId))
                                                {
                                                    var pd = SmartMarket.Patches.Customer_TryGenerateContract_Patch.FindMatchingProduct(resolvedProductId, ProductManager.Instance?.AllProducts);
                                                    if (pd != null) resolvedProductName = pd.name;
                                                    // extract effects from product definition
                                                    var infos = SmartMarket.Core.MessageContextBuilder.ExtractProductEffects(pd);
                                                    foreach (var pi in infos)
                                                    {
                                                        if (!string.IsNullOrEmpty(pi.EffectId) || !string.IsNullOrEmpty(pi.EffectName))
                                                            deliveredEffects.Add((pi.EffectId ?? "", pi.EffectName ?? ""));
                                                    }
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception exEntries) { SmartMarketConfig.LogDebug($"[SmartMarket] productList inspection failed: {exEntries.Message}"); }
                    }

                    // Fallback: inspect items list (actual ItemInstance objects delivered)
                    if (deliveredEffects.Count == 0 && items != null)
                    {
                        try
                        {
                            if (items is IEnumerable ien)
                            {
                                foreach (var it in ien)
                                {
                                    if (it == null) continue;
                                    try
                                    {
                                        // Try to get ProductDefinition from ItemInstance
                                        var ip = it.GetType().GetProperty("ProductDefinition") ?? it.GetType().GetProperty("Product");
                                        object prodDef = null;
                                        if (ip != null) prodDef = ip.GetValue(it);
                                        if (prodDef == null)
                                        {
                                            // Try field access
                                            var f = it.GetType().GetField("product");
                                            if (f != null) prodDef = f.GetValue(it);
                                        }

                                        if (prodDef != null)
                                        {
                                            var pd = prodDef as Il2CppScheduleOne.Product.ProductDefinition;
                                            if (pd != null)
                                            {
                                                var infos = SmartMarket.Core.MessageContextBuilder.ExtractProductEffects(pd);
                                                foreach (var pi in infos)
                                                {
                                                    if (!string.IsNullOrEmpty(pi.EffectId) || !string.IsNullOrEmpty(pi.EffectName))
                                                        deliveredEffects.Add((pi.EffectId ?? "", pi.EffectName ?? ""));
                                                }
                                                if (string.IsNullOrEmpty(resolvedProductId))
                                                {
                                                    try { resolvedProductId = !string.IsNullOrEmpty(pd.SaveFileName) ? pd.SaveFileName : pd.name; resolvedProductName = pd.name; } catch { }
                                                }
                                            }
                                            else
                                            {
                                                // attempt generic extraction if not strongly typed
                                                var infos = SmartMarket.Core.MessageContextBuilder.ExtractProductEffects(prodDef);
                                                foreach (var pi in infos) deliveredEffects.Add((pi.EffectId ?? "", pi.EffectName ?? ""));
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                        catch (Exception exItems) { SmartMarketConfig.LogDebug($"[SmartMarket] items inspection failed: {exItems.Message}"); }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SmartMarket] Error extracting delivered effects: {ex.Message}");
                }

                // Build delivered effects string for logs
                string deliveredEffectsStr = deliveredEffects.Count == 0 ? "<none>" : string.Join(", ", deliveredEffects.Select(d => (string.IsNullOrEmpty(d.Id)?d.Name:d.Id + ":" + d.Name)));
                MelonLogger.Msg($"[SM-DIAG] Resolved product: {resolvedProductName} ({resolvedProductId})");
                MelonLogger.Msg($"[SM-DIAG] Delivered effects: {deliveredEffectsStr}");

                // Determine effect match (prefer ID)
                bool effectMatch = false;
                if (!string.IsNullOrEmpty(pendingEffectId))
                {
                    effectMatch = deliveredEffects.Any(d => !string.IsNullOrEmpty(d.Id) && string.Equals(d.Id, pendingEffectId, StringComparison.OrdinalIgnoreCase));
                }
                if (!effectMatch && !string.IsNullOrEmpty(pendingEffectName))
                {
                    effectMatch = deliveredEffects.Any(d => !string.IsNullOrEmpty(d.Name) && string.Equals(d.Name, pendingEffectName, StringComparison.OrdinalIgnoreCase));
                }

                MelonLogger.Msg($"[SM-DIAG] Effect match: {(effectMatch ? "TRUE" : "FALSE")}");

                // Record purchase with the actual delivered effect(s) joined
                string effectGained = deliveredEffects.Count == 0 ? "<none>" : string.Join("+", deliveredEffects.Select(d => string.IsNullOrEmpty(d.Id)?d.Name:d.Id));

                // Price/satisfaction may be present in args; try to extract payment (arg index 3)
                try { if (__args.Length >= 4) price = Convert.ToSingle(__args[3]); } catch { }

                // Attempt to find consumer profile for neighborhood info (best-effort)
                SmartMarket.Core.ConsumerProfile profile = null;
                try
                {
                    // Try to find Customer instance in scene by name
                    var all = UnityEngine.Object.FindObjectsOfType<Il2CppScheduleOne.Economy.Customer>();
                    foreach (var c in all)
                    {
                        try { if (c != null && c.gameObject != null && c.gameObject.name == customerName) { profile = SmartMarket.Core.ProfileManager.GetOrCreateProfile(c); break; } } catch { }
                    }
                }
                catch { }

                // Build delivery context and evaluate score using ScoreEngine
                try
                {
                    var ctx = new SmartMarket.Scoring.DeliveryContext();
                    ctx.CustomerId = customerName;
                    ctx.PendingProductId = pendingProductId;
                    ctx.PendingQuality = pendingQuality;
                    ctx.PendingQuantity = pendingQuantity;
                    if (!string.IsNullOrEmpty(pendingEffectId) || !string.IsNullOrEmpty(pendingEffectName))
                        ctx.RequestedEffects.Add(new SmartMarket.Scoring.EffectRef(pendingEffectId, pendingEffectName));

                    ctx.ResolvedProductId = resolvedProductId;
                    ctx.ResolvedProductName = resolvedProductName;
                    ctx.DeliveredQuantity = deliveredQuantity;
                    ctx.DeliveredQuality = deliveredQuality;
                    ctx.Price = price;
                    foreach (var d in deliveredEffects)
                    {
                        ctx.DeliveredEffects.Add(new SmartMarket.Scoring.EffectRef(d.Id, d.Name));
                    }

                    var scoreResult = SmartMarket.Scoring.ScoreEngine.Evaluate(ctx);
                    var breakdownStrings = scoreResult.Breakdown.Select(b => b.ToString()).ToArray();
                    SmartMarketConfig.LogDebug($"[SmartMarket][SCORE] Breakdown for {customerName}: {string.Join(" ; ", breakdownStrings)} => total={scoreResult.Total:0.00}");

                    try
                    {
                        MemorySystem.AddPurchaseRecord(customerName, resolvedProductId ?? resolvedProductName, resolvedProductName, effectGained, 50f, 0f, price, profile, scoreResult.Total);
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[SmartMarket] Failed adding purchase record for {customerName}: {ex.Message}");
                    }

                    try
                    {
                        var decidedOutcome = SmartMarket.Core.OutcomeDecider.Decide(scoreResult);
                                                SmartMarketConfig.LogDebug($"[SmartMarket][OUTCOME] For {customerName} outcome={decidedOutcome} score={scoreResult.Total:0.00}");
                    }
                    catch (Exception exOut)
                    {
                        SmartMarketConfig.LogDebug($"[SmartMarket] OutcomeDecider failed: {exOut.Message}");
                    }
                }
                catch (Exception ex)
                {
                    SmartMarketConfig.LogDebug($"[SmartMarket] Scoring/Eval failed: {ex.Message}");
                }

                // Keep pending used/clear pending requested info now that handover was processed
                try
                {
                    mem.PendingRequestedProductId = "";
                    mem.PendingRequestedEffectId = "";
                    mem.PendingRequestedEffectName = "";
                    mem.PendingRequestedQuality = "";
                    mem.PendingRequestedQuantity = 0;
                    MemorySystem.Save();
                }
                catch { }

                // Final diagnostic log with structured output
                MelonLogger.Msg($"[SM-DIAG] Generating EFFECT request log: NPC: {customerName} | RequestedEffect: {pendingEffectName} (ID={pendingEffectId}) | ResolvedProduct: {resolvedProductName} | DeliveredEffects: {deliveredEffectsStr} | EffectMatch: {effectMatch}");

            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] ProcessHandoverServerSide_Patch.Postfix failed: {ex.Message}");
            }
        }
    }
}
