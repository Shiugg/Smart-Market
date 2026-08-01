using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;
using MelonLoader;
using MelonLogger = SmartMarket.SmartMarketLogger;
using UnityEngine;

namespace SmartMarket.Core
{
    public static class MessageContextBuilder
    {
        public class ProductEffectInfo
        {
            public string EffectId;
            public string EffectName;

            public string DisplayName
            {
                get
                {
                    if (!string.IsNullOrEmpty(EffectName)) return EffectName;
                    return EffectId ?? string.Empty;
                }
            }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        public static MessageContext Build(Customer customer, ContractInfo contract)
        {
            var context = new MessageContext();
            try
            {
                context.CustomerName = customer?.gameObject?.name ?? "Desconocido";
                context.Profile = ProfileManager.GetOrCreateProfile(customer) ?? new ConsumerProfile(context.CustomerName, ConsumerType.Classic, Neighborhood.Westville);
                context.Neighborhood = context.Profile.HomeNeighborhood;
                context.NeighborhoodStandard = ProfileManager.GetNeighborhoodStandard(context.Neighborhood);

                context.RequestedProductId = ExtractProductIdFromContract(contract);
                context.RequestedProductName = ExtractProductNameFromContract(contract);
                context.RequestedQuantity = ExtractProductQuantityFromContract(contract);
                context.RequestedQuality = ExtractProductQualityFromContract(contract);
                context.RequestedProductDefinition = ResolveProductDefinition(context.RequestedProductId, context.RequestedProductName);

                // If no quantity found in the contract, try to use any pending requested quantity stored in memory
                var memory = MemorySystem.GetMemory(context.CustomerName);
                if ((context.RequestedQuantity == 0) && memory != null && memory.PendingRequestedQuantity > 0)
                {
                    context.RequestedQuantity = memory.PendingRequestedQuantity;
                    SmartMarketConfig.LogDebug($"ContextBuilder: Used memory PendingRequestedQuantity={context.RequestedQuantity} for {context.CustomerName}");
                }

                if (context.RequestedProductDefinition != null)
                {
                    context.RequestedProductName = !string.IsNullOrEmpty(context.RequestedProductDefinition.name)
                        ? context.RequestedProductDefinition.name
                        : context.RequestedProductName;

                    // CRÍTICO: Clasificar ANTES de extraer efectos
                    bool isBaseProduct = ProductClassifier.IsBaseProduct(context.RequestedProductDefinition);
                    
                    if (isBaseProduct)
                    {
                        // Solo extraer efectos para productos base
                        var effectInfo = ExtractProductEffectInfo(context.RequestedProductDefinition);
                        context.RequestedEffectId = effectInfo.EffectId ?? string.Empty;
                        context.RequestedEffect = !string.IsNullOrEmpty(effectInfo.EffectName)
                            ? effectInfo.EffectName
                            : effectInfo.EffectId ?? string.Empty;
                        
                        AuditPipeline.AuditProductClassification(context.CustomerName, context.RequestedProductDefinition, true, context.RequestedEffect, context.RequestedQuality);
                    }
                    else
                    {
                        // Para mezclas: NO extraer efectos
                        context.RequestedEffectId = string.Empty;
                        context.RequestedEffect = string.Empty;
                        SmartMarketConfig.LogDebug($"ContextBuilder: '{context.RequestedProductName}' es una mezcla (no se extraerán efectos)");
                        
                        AuditPipeline.AuditProductClassification(context.CustomerName, context.RequestedProductDefinition, false, "", "");
                    }
                }
                else
                {
                    // If no product definition resolved, log for diagnosis (why effects not chosen)
                    if (!string.IsNullOrEmpty(context.RequestedProductId) || !string.IsNullOrEmpty(context.RequestedProductName))
                        SmartMarketConfig.LogDebug($"ContextBuilder: Could not resolve product definition for RequestedProductId='{context.RequestedProductId}', RequestedProductName='{context.RequestedProductName}'");
                }

                context.IsWordOfMouth = !string.IsNullOrEmpty(memory?.PendingWordOfMouthProduct) && 
                                      string.Equals(memory.PendingWordOfMouthProduct, context.RequestedProductId, StringComparison.OrdinalIgnoreCase) &&
                                      // PROBLEM 4: Add probability check for Word of Mouth events
                                      UnityEngine.Random.Range(0f, 1f) < SmartMarketConfig.WordOfMouthChance;
                
                // LOG CRÍTICO: Rastrear exactamente CUÁNDO IsWordOfMouth pasa a true
                if (context.IsWordOfMouth)
                {
                    MelonLogger.Msg($"[TRACE-WOM-DECISION] IsWordOfMouth BECAME TRUE");
                    MelonLogger.Msg($"[TRACE-WOM-DECISION]   PendingWordOfMouthProduct: {memory?.PendingWordOfMouthProduct}");
                    MelonLogger.Msg($"[TRACE-WOM-DECISION]   RequestedProductId: {context.RequestedProductId}");
                    MelonLogger.Msg($"[TRACE-WOM-DECISION]   Stack: {new System.Diagnostics.StackTrace()}");
                }
                else
                {
                    MelonLogger.Msg($"[TRACE-WOM-DECISION] IsWordOfMouth REMAINS FALSE");
                    MelonLogger.Msg($"[TRACE-WOM-DECISION]   PendingWordOfMouthProduct: {(memory?.PendingWordOfMouthProduct ?? "NULL")}");
                    MelonLogger.Msg($"[TRACE-WOM-DECISION]   RequestedProductId: {context.RequestedProductId}");
                }
                
                AuditPipeline.AuditWordOfMouthDecision(context.CustomerName, memory?.PendingWordOfMouthProduct, context.RequestedProductId, context.IsWordOfMouth);

                context.IsRepeatRequest = IsRepeatPurchaseRequest(memory, context.RequestedProductId, context.RequestedProductName);
                context.IsEffectDriven = !string.IsNullOrEmpty(context.RequestedEffect);

                // Debug output for context construction
                SmartMarketConfig.LogDebug($"ContextBuilder: Built context for {context.CustomerName} - Profile:{context.Profile.Type} Neighborhood:{context.Profile.HomeNeighborhood} Requested:{context.RequestedProductName} (id:{context.RequestedProductId}) IsWOM:{context.IsWordOfMouth} IsRepeat:{context.IsRepeatRequest} IsEffect:{context.IsEffectDriven} Effect:{context.RequestedEffect}");

                // Personality-driven modifiers
                switch (context.Profile.Type)
                {
                    case ConsumerType.Classic:
                        context.Preferences.PreferenceNovelty = 0.2f;
                        context.Preferences.SubstitutionTolerance = 0.2f;
                        context.Preferences.Urgency = 0.3f;
                        context.Preferences.QualityBias = 0.3f;
                        break;
                    case ConsumerType.Experimenter:
                        context.Preferences.PreferenceNovelty = 0.8f;
                        context.Preferences.SubstitutionTolerance = 0.6f;
                        context.Preferences.Urgency = 0.4f;
                        context.Preferences.QualityBias = 0.4f;
                        break;
                    case ConsumerType.Addict:
                        context.Preferences.PreferenceNovelty = 0.1f;
                        context.Preferences.SubstitutionTolerance = 0.9f;
                        context.Preferences.Urgency = 0.95f;
                        context.Preferences.QualityBias = 0.2f;
                        break;
                    case ConsumerType.Gourmet:
                        context.Preferences.PreferenceNovelty = 0.3f;
                        context.Preferences.SubstitutionTolerance = 0.2f;
                        context.Preferences.Urgency = 0.4f;
                        context.Preferences.QualityBias = 0.95f;
                        break;
                    default:
                        context.Preferences.PreferenceNovelty = 0.5f;
                        context.Preferences.SubstitutionTolerance = 0.5f;
                        context.Preferences.Urgency = 0.5f;
                        context.Preferences.QualityBias = 0.5f;
                        break;
                }

                // Adjust by neighborhood standards
                var std = ProfileManager.GetNeighborhoodStandard(context.Profile.HomeNeighborhood);
                                if (std == NeighborhoodStandard.High)
                {
                    context.Preferences.QualityBias = Mathf.Clamp01(context.Preferences.QualityBias + 0.15f);
                    context.Preferences.SubstitutionTolerance = Mathf.Clamp01(context.Preferences.SubstitutionTolerance - 0.1f);
                }
                                else if (std == NeighborhoodStandard.Marginal)
                {
                    context.Preferences.SubstitutionTolerance = Mathf.Clamp01(context.Preferences.SubstitutionTolerance + 0.15f);
                    context.Preferences.PreferenceNovelty = Mathf.Clamp01(context.Preferences.PreferenceNovelty - 0.1f);
                }

                context.IsUrgent = context.Preferences.Urgency > 0.6f || context.IsWordOfMouth;
                context.HasGoodQualityFocus = context.Preferences.QualityBias > 0.6f;
                // Respect behavioral flag from profile (Addict still maps to high rejection by default)
                context.RejectsCounterOffers = context.Profile != null && context.Profile.RejectsCounterOffers;
                context.MotivationReason = DetermineMotivationReason(context);

                // AUDITORÍA FINAL: Loguear el contexto completamente construido
                AuditPipeline.AuditMessageContext(context);
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Warning($"[SmartMarket] Error construyendo contexto de mensaje: {ex.Message}");
            }

            return context;
        }

        private static string ExtractProductIdFromContract(ContractInfo contract)
        {
            try
            {
                if (contract?.Products?.entries != null && contract.Products.entries.Count > 0)
                {
                    return contract.Products.entries[0].ProductID;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static string ExtractProductNameFromContract(ContractInfo contract)
        {
            try
            {
                if (contract?.Products != null)
                {
                    string productsStr = contract.Products.GetCommaSeperatedString();
                    if (!string.IsNullOrEmpty(productsStr))
                    {
                        if (productsStr.Contains("x "))
                        {
                            return productsStr.Substring(productsStr.IndexOf("x ") + 2).Trim();
                        }
 
                        return productsStr.Trim();
                    }
                }
            }
            catch
            {
            }
 
            return string.Empty;
        }
 
        private static int ExtractProductQuantityFromContract(ContractInfo contract)
        {
            try
            {
                if (contract?.Products?.entries != null && contract.Products.entries.Count > 0)
                {
                    return contract.Products.entries[0].Quantity;
                }
            }
            catch
            {
            }
  
            return 0;
        }
  
        private static string ExtractProductQualityFromContract(ContractInfo contract)
        {
            try
            {
                if (contract?.Products?.entries != null && contract.Products.entries.Count > 0)
                {
                    var entry = contract.Products.entries[0];
                    var type = entry.GetType();
                    var prop = type.GetProperty("Quality");
                    if (prop != null)
                    {
                        var value = prop.GetValue(entry);
                        if (value != null)
                        {
                            var qualityName = FormatQualityName(value.ToString());
                            if (string.Equals(qualityName, "Standard", StringComparison.OrdinalIgnoreCase))
                                return string.Empty;
                            return qualityName;
                        }
                    }
                }
            }
            catch
            {
            }
  
            return string.Empty;
        }
  
        private static string FormatQualityName(string quality)
        {
            if (string.IsNullOrEmpty(quality)) return string.Empty;
            switch (quality.ToLowerInvariant())
            {
                case "premium": return "Premium";
                case "standard": return "Standard";
                case "trash": return "Trash";
                case "heavenly": return "Heavenly";
                case "elite": return "Elite";
                case "high": return "High";
                case "low": return "Low";
                default: return char.ToUpperInvariant(quality[0]) + quality.Substring(1);
            }
        }
  
        private static ProductDefinition ResolveProductDefinition(string productId, string productName)
        {
            try
            {
                if (!string.IsNullOrEmpty(productId))
                {
                    var product = FindProduct(productId);
                    if (product != null)
                        return product;
                }

                if (!string.IsNullOrEmpty(productName))
                {
                    var product = FindProduct(productName);
                    if (product != null)
                        return product;
                }
            }
            catch
            {
            }

            return null;
        }

        private static ProductDefinition FindProduct(string query)
        {
            if (string.IsNullOrEmpty(query) || ProductManager.Instance == null || ProductManager.Instance.AllProducts == null)
                return null;

            foreach (var product in ProductManager.Instance.AllProducts)
            {
                if (product == null)
                    continue;

                if (string.Equals(product.name, query, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(product.SaveFileName, query, StringComparison.OrdinalIgnoreCase))
                {
                    return product;
                }
            }

            return null;
        }

        public static List<ProductEffectInfo> ExtractProductEffects(ProductDefinition product)
        {
            var effects = new System.Collections.Generic.List<ProductEffectInfo>();
            if (product == null)
                return effects;

            try
            {
                var productType = product.GetType();
                var propsProperty = productType.GetProperty("Properties");
                if (propsProperty != null)
                {
                    var propertiesValue = propsProperty.GetValue(product);
                    if (propertiesValue != null)
                    {
                        SmartMarketConfig.LogDebug($"ContextBuilder: Product.Properties type={propertiesValue.GetType().FullName}");
                        var handled = false;

                        try
                        {
                            var propsType = propertiesValue.GetType();
                            if (propsType.FullName != null && propsType.FullName.Contains("Il2CppSystem.Collections.Generic.List`1"))
                            {
                                var itemsField = propsType.GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance);
                                if (itemsField != null)
                                {
                                    var itemsArr = itemsField.GetValue(propertiesValue);
                                    if (itemsArr != null)
                                    {
                                        var lenProp = itemsArr.GetType().GetProperty("Length") ?? itemsArr.GetType().GetProperty("Count");
                                        int len = -1;
                                        if (lenProp != null)
                                        {
                                            try { var lv = lenProp.GetValue(itemsArr); if (lv is int li) len = li; }
                                            catch { len = -1; }
                                        }

                                        MethodInfo getValueMethod = itemsArr.GetType().GetMethod("GetValue", new[] { typeof(int) });
                                        PropertyInfo itemProp = itemsArr.GetType().GetProperty("Item");
                                        MethodInfo getMethod = itemsArr.GetType().GetMethod("Get", new[] { typeof(int) });

                                        if (len >= 0)
                                        {
                                            for (int i = 0; i < len; i++)
                                            {
                                                object it = null;
                                                try
                                                {
                                                    if (getValueMethod != null) it = getValueMethod.Invoke(itemsArr, new object[] { i });
                                                    else if (itemProp != null) it = itemProp.GetValue(itemsArr, new object[] { i });
                                                    else if (getMethod != null) it = getMethod.Invoke(itemsArr, new object[] { i });
                                                }
                                                catch { it = null; }

                                                var effectInfo = ExtractProductEffectInfoFromObject(it);
                                                SmartMarketConfig.LogDebug($"ContextBuilder: Product.Properties[_items][{i}] type={(it==null?"<null>":it.GetType().FullName)} extractedEffectId='{effectInfo?.EffectId}' extractedEffectName='{effectInfo?.EffectName}'");
                                                if (effectInfo != null && !string.IsNullOrEmpty(effectInfo.DisplayName))
                                                {
                                                    effects.Add(effectInfo);
                                                }
                                            }
                                            handled = true;
                                        }
                                        else
                                        {
                                            foreach (var it in EnumerateCollection(itemsArr))
                                            {
                                                var effectInfo = ExtractProductEffectInfoFromObject(it);
                                                SmartMarketConfig.LogDebug($"ContextBuilder: Product.Properties[_items] enumerated type={(it==null?"<null>":it.GetType().FullName)} extractedEffectId='{effectInfo?.EffectId}' extractedEffectName='{effectInfo?.EffectName}'");
                                                if (effectInfo != null && !string.IsNullOrEmpty(effectInfo.DisplayName))
                                                {
                                                    effects.Add(effectInfo);
                                                }
                                            }
                                            handled = true;
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            SmartMarketConfig.LogDebug($"ContextBuilder: Exception while handling Il2Cpp list _items: {ex.Message}");
                        }

                        if (!handled)
                        {
                            var items = new System.Collections.Generic.List<object>();
                            foreach (var item in EnumerateCollection(propertiesValue))
                                items.Add(item);

                            if (items.Count == 0)
                            {
                                SmartMarketConfig.LogDebug($"ContextBuilder: Product.Properties enumeration produced no items for '{product.name}'");
                            }
                            else
                            {
                                for (int index = 0; index < items.Count; index++)
                                {
                                    var propItem = items[index];
                                    if (propItem == null)
                                    {
                                        SmartMarketConfig.LogDebug($"ContextBuilder: Product.Properties[{index}] is null");
                                        continue;
                                    }

                                    var effectInfo = ExtractProductEffectInfoFromObject(propItem);
                                    SmartMarketConfig.LogDebug($"ContextBuilder: Product.Properties[{index}] type={propItem.GetType().FullName} extractedEffectId='{effectInfo?.EffectId}' extractedEffectName='{effectInfo?.EffectName}'");
                                    if (effectInfo != null && !string.IsNullOrEmpty(effectInfo.DisplayName))
                                    {
                                        effects.Add(effectInfo);
                                    }
                                }
                            }
                        }
                    }
                }

                if (effects.Count == 0)
                {
                    foreach (var property in productType.GetProperties())
                    {
                        if (property.Name.IndexOf("Effect", StringComparison.OrdinalIgnoreCase) >= 0 || property.Name.IndexOf("Effects", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            try
                            {
                                var value = property.GetValue(product);
                                if (value is string stringValue && !string.IsNullOrEmpty(stringValue))
                                {
                                    effects.Add(new ProductEffectInfo { EffectName = stringValue });
                                    break;
                                }

                                if (value is System.Collections.IEnumerable enumerable)
                                {
                                    foreach (var item in enumerable)
                                    {
                                        var effectInfo = ExtractProductEffectInfoFromObject(item);
                                        if (effectInfo != null && !string.IsNullOrEmpty(effectInfo.DisplayName))
                                        {
                                            effects.Add(effectInfo);
                                            break;
                                        }
                                    }
                                }
                            }
                            catch
                            {
                            }

                            if (effects.Count > 0)
                                break;
                        }
                    }
                }
            }
            catch
            {
            }

            if (effects.Count > 0)
                SmartMarketConfig.LogDebug($"ContextBuilder: Extracted {effects.Count} product effect(s) from product '{product.name}' ({product.SaveFileName})");
            else
                SmartMarketConfig.LogDebug($"ContextBuilder: No product effect found for '{product?.name ?? "<null>"}' ({product?.SaveFileName ?? "<null>"})");

            return effects;
        }

        // Overload to handle objects that are not strongly typed ProductDefinition at compile time
        public static System.Collections.Generic.List<ProductEffectInfo> ExtractProductEffects(object productObj)
        {
            var effects = new System.Collections.Generic.List<ProductEffectInfo>();
            try
            {
                if (productObj == null) return effects;
                if (productObj is ProductDefinition pd) return ExtractProductEffects(pd);

                var t = productObj.GetType();
                foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    try
                    {
                        if (prop.Name.IndexOf("Effect", StringComparison.OrdinalIgnoreCase) >= 0 || prop.Name.IndexOf("Effects", StringComparison.OrdinalIgnoreCase) >= 0 || prop.Name.IndexOf("Properties", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var val = prop.GetValue(productObj);
                            if (val == null) continue;

                            if (val is string s)
                                effects.Add(new ProductEffectInfo { EffectId = s, EffectName = s });
                            else if (val is System.Collections.IEnumerable en)
                            {
                                foreach (var item in en)
                                {
                                    try { if (item != null) effects.Add(ExtractProductEffectInfoFromObject(item)); } catch { }
                                }
                            }
                            else
                            {
                                var info = ExtractProductEffectInfoFromObject(val);
                                if (info != null) effects.Add(info);
                            }
                        }
                    }
                    catch { }
                }

                foreach (var field in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    try
                    {
                        if (field.Name.IndexOf("Effect", StringComparison.OrdinalIgnoreCase) >= 0 || field.Name.IndexOf("Effects", StringComparison.OrdinalIgnoreCase) >= 0 || field.Name.IndexOf("Properties", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var val = field.GetValue(productObj);
                            if (val == null) continue;

                            if (val is string s)
                                effects.Add(new ProductEffectInfo { EffectId = s, EffectName = s });
                            else if (val is System.Collections.IEnumerable en)
                            {
                                foreach (var item in en)
                                {
                                    try { if (item != null) effects.Add(ExtractProductEffectInfoFromObject(item)); } catch { }
                                }
                            }
                            else
                            {
                                var info = ExtractProductEffectInfoFromObject(val);
                                if (info != null) effects.Add(info);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return effects;
        }

        public static (string EffectId, string EffectName) ExtractProductEffectInfo(ProductDefinition product)
        {
            var effects = ExtractProductEffects(product);
            if (effects.Count > 0)
            {
                var result = (effects[0].EffectId ?? string.Empty, effects[0].EffectName ?? string.Empty);
                MelonLogger.Msg($"[TRACE-EFFECT-EXTRACT] ExtractProductEffectInfo FOUND effect: Id='{result.Item1}' Name='{result.Item2}' from product={product?.name}");
                return result;
            }
            MelonLogger.Msg($"[TRACE-EFFECT-EXTRACT] ExtractProductEffectInfo NO effects found in product={product?.name}");
            return (string.Empty, string.Empty);
        }

        /// <summary>
        /// Extract ALL effects from a product as separate Ids and Names
        /// Returns two lists: (EffectIds, EffectNames)
        /// Used for multi-effect contracts where NPC requests multiple effects simultaneously
        /// </summary>
        public static (List<string> EffectIds, List<string> EffectNames) ExtractAllProductEffects(ProductDefinition product)
        {
            var allEffects = ExtractProductEffects(product);
            var effectIds = new List<string>();
            var effectNames = new List<string>();

            foreach (var effect in allEffects)
            {
                if (effect != null && !string.IsNullOrEmpty(effect.DisplayName))
                {
                    effectIds.Add(effect.EffectId ?? string.Empty);
                    effectNames.Add(effect.EffectName ?? string.Empty);
                }
            }

            if (effectIds.Count > 0)
            {
                MelonLogger.Msg($"[TRACE-EFFECT-EXTRACT-ALL] ExtractAllProductEffects FOUND {effectIds.Count} effect(s) from product={product?.name}: {string.Join(", ", effectNames)}");
            }
            else
            {
                MelonLogger.Msg($"[TRACE-EFFECT-EXTRACT-ALL] ExtractAllProductEffects NO effects found in product={product?.name}");
            }

            return (effectIds, effectNames);
        }

        private static string ExtractProductEffect(ProductDefinition product)
        {
            var effectInfo = ExtractProductEffectInfo(product);
            if (!string.IsNullOrEmpty(effectInfo.EffectName))
                return effectInfo.EffectName;
            return effectInfo.EffectId ?? string.Empty;
        }

        private static ProductEffectInfo ExtractProductEffectInfoFromObject(object effectObj)
        {
            if (effectObj == null)
                return null;

            var effectInfo = new ProductEffectInfo();
            if (effectObj is string stringValue)
            {
                effectInfo.EffectName = stringValue;
                return effectInfo;
            }

            try
            {
                var effectType = effectObj.GetType();

                var idProp = effectType.GetProperty("ID") ?? effectType.GetProperty("Id");
                if (idProp != null)
                {
                    var idValue = idProp.GetValue(effectObj);
                    var idString = idValue as string;
                    if (string.IsNullOrEmpty(idString) && idValue != null)
                        idString = idValue.ToString();

                    effectInfo.EffectId = idString ?? string.Empty;
                }

                var nameProp = effectType.GetProperty("Name");
                if (nameProp != null)
                {
                    var nameValue = nameProp.GetValue(effectObj);
                    var nameString = nameValue as string;
                    if (string.IsNullOrEmpty(nameString) && nameValue != null)
                        nameString = nameValue.ToString();

                    effectInfo.EffectName = nameString ?? string.Empty;
                }
            }
            catch
            {
            }

            if (string.IsNullOrEmpty(effectInfo.DisplayName))
            {
                effectInfo.EffectName = effectObj.ToString();
            }

            return effectInfo;
        }
        private static System.Collections.Generic.IEnumerable<object> EnumerateCollection(object collection)
        {
            if (collection == null)
                yield break;

            if (collection is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                    yield return item;

                yield break;
            }

            var type = collection.GetType();
            var getEnumerator = type.GetMethod("GetEnumerator", Type.EmptyTypes);
            if (getEnumerator != null)
            {
                object enumerator = null;
                try
                {
                    enumerator = getEnumerator.Invoke(collection, null);
                }
                catch
                {
                }

                if (enumerator != null)
                {
                    var moveNext = enumerator.GetType().GetMethod("MoveNext", Type.EmptyTypes);
                    var currentProp = enumerator.GetType().GetProperty("Current");
                    if (moveNext != null && currentProp != null)
                    {
                        while (true)
                        {
                            var moved = moveNext.Invoke(enumerator, null);
                            if (!(moved is bool movedBool) || !movedBool)
                                break;

                            yield return currentProp.GetValue(enumerator);
                        }

                        yield break;
                    }
                }
            }

            var countProp = type.GetProperty("Count");
            if (countProp != null)
            {
                object countValue = null;
                try
                {
                    countValue = countProp.GetValue(collection);
                }
                catch
                {
                }

                if (countValue is int count)
                {
                    var itemProp = type.GetProperty("Item", new[] { typeof(int) });
                    if (itemProp == null)
                    {
                        foreach (var property in type.GetProperties())
                        {
                            var indexParams = property.GetIndexParameters();
                            if (property.Name == "Item" && indexParams.Length == 1 && indexParams[0].ParameterType == typeof(int))
                            {
                                itemProp = property;
                                break;
                            }
                        }
                    }

                    if (itemProp != null)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            yield return itemProp.GetValue(collection, new object[] { i });
                        }
                    }
                }
            }
        }

        private static bool IsRepeatPurchaseRequest(CustomerMemory memory, string productId, string productName)
        {
            if (memory == null || memory.PurchaseHistory == null || memory.PurchaseHistory.Count == 0)
                return false;

            foreach (var record in memory.PurchaseHistory)
            {
                if (!string.IsNullOrEmpty(productId) && string.Equals(record.ProductID, productId, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (!string.IsNullOrEmpty(productName) && string.Equals(record.ProductName, productName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string DetermineMotivationReason(MessageContext context)
        {
            if (context == null)
                return "unknown";

            if (context.IsWordOfMouth)
                return "word_of_mouth";

            if (context.IsRepeatRequest)
                return "repeat_customer";

            if (context.HasGoodQualityFocus)
                return "quality_seek";

            return "direct_request";
        }
    }
}
