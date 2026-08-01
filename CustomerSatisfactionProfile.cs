/*
 * CustomerSatisfactionProfile.cs
 * Purpose:
 *   Almacena el estado emocional/comercial persistente de cada NPC hacia el jugador.
 *   Provee métodos para registrar tratos buenos/malos, respuestas ignoradas y decidir
 *   si el NPC aceptará comprar del jugador o preferirá la competencia.
 *
 * Dependencies:
 *   - UnityEngine (Application.persistentDataPath, JsonUtility)
 *   - System.IO for file operations
 *   - Il2CppScheduleOne.Product.ProductDefinition (para inspeccionar producto)
 *   - Il2CppScheduleOne.ItemFramework.EQuality (calidades)
 *   - SmartMarket.Core.MemorySystem (no obligatorio pero compatible)
 *
 * Risks / Integration notes:
 *   - Persiste por archivo JSON por cliente en Application.persistentDataPath/SmartMarket/customers/
 *     (extiende el sistema existente sin reemplazar MemorySystem central).
 *   - Usa reflexión/safeguards al leer propiedades de ProductDefinition para evitar crashes.
 *   - Se diseñó para coexistir con ProfileManager (no lo reemplaza). Los datos son adicionales
 *     y no cambian el comportamiento inmediato salvo cuando otros módulos consulten este profile.
 */

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MelonLogger = SmartMarket.SmartMarketLogger;
using MelonLoader;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.ItemFramework;

namespace SmartMarket.Customers
{
    [Serializable]
    public class CustomerSatisfactionProfile
    {
        // --- Persisted state (0.0 - 1.0 floats) ---
        public string CustomerID;
        public float Satisfaction = 0.5f; // 0 = muy mal / 1 = muy satisfecho
        public float Trust = 0.5f; // 0 = no confía / 1 = confía ciegamente
        public float AddictionLevel = 0.5f; // snapshot / historial (0-1 normalized)

        // Preferences
        public List<string> PreferredEffects = new List<string>();
        public List<string> DislikedEffects = new List<string>();

        // Minimum accepted quality
        public EQuality MinAcceptedQuality = EQuality.Standard;

        // Counters
        public int ConsecutiveGoodDeals = 0;
        public int ConsecutiveBadDeals = 0;

        // Dates / activity
        public DateTime LastPurchaseDate = DateTime.MinValue;
        public DateTime LastMessageDate = DateTime.MinValue;
        public int DaysWithoutResponse = 0;

        // Non-persisted runtime helpers (not serialized)
        [NonSerialized] private static readonly string BaseFolder = Path.Combine(Application.persistentDataPath, "SmartMarket", "customers");

        // -------------------- Persistence --------------------
        private string FilePath => Path.Combine(BaseFolder, SanitizeFileName(CustomerID) + ".json");

        private static string SanitizeFileName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "unknown";
            foreach (var c in Path.GetInvalidFileNameChars()) id = id.Replace(c, '_');
            return id;
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(BaseFolder)) Directory.CreateDirectory(BaseFolder);
                string json = SmartMarket.Core.JsonUtilityHelper.ToJson(this, true);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomerSatisfactionProfile] Save failed for {CustomerID}: {ex.Message}");
            }
        }

        public static CustomerSatisfactionProfile Load(string customerId)
        {
            try
            {
                if (string.IsNullOrEmpty(customerId)) return null;
                string folder = BaseFolder;
                string path = Path.Combine(folder, SanitizeFileName(customerId) + ".json");
                if (!File.Exists(path)) return null;
                var txt = File.ReadAllText(path);
                var obj = SmartMarket.Core.JsonUtilityHelper.FromJson<CustomerSatisfactionProfile>(txt);
                if (obj != null) return obj;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomerSatisfactionProfile] Load failed for {customerId}: {ex.Message}");
            }
            return null;
        }

        public static CustomerSatisfactionProfile GetOrCreate(string customerId, Il2CppScheduleOne.Economy.Customer maybeCustomerObject = null)
        {
            try
            {
                var loaded = Load(customerId);
                if (loaded != null)
                {
                    // ensure lists not null after deserialization
                    if (loaded.PreferredEffects == null) loaded.PreferredEffects = new List<string>();
                    if (loaded.DislikedEffects == null) loaded.DislikedEffects = new List<string>();
                    return loaded;
                }

                // Create default with sensible defaults
                var profile = new CustomerSatisfactionProfile();
                profile.CustomerID = customerId;
                profile.Satisfaction = 0.5f;
                profile.Trust = 0.5f;
                profile.AddictionLevel = 0.5f;
                profile.PreferredEffects = new List<string>();
                profile.DislikedEffects = new List<string>();
                profile.MinAcceptedQuality = EQuality.Standard;
                profile.ConsecutiveGoodDeals = 0;
                profile.ConsecutiveBadDeals = 0;
                profile.DaysWithoutResponse = 0;

                // If a Customer object is provided, try to snapshot CurrentAddiction and known affinities
                if (maybeCustomerObject != null)
                {
                    try
                    {
                        // CurrentAddiction getter exists in dumped API
                        var prop = maybeCustomerObject.GetType().GetProperty("CurrentAddiction");
                        if (prop != null)
                        {
                            var val = prop.GetValue(maybeCustomerObject);
                            if (val is float f) profile.AddictionLevel = Mathf.Clamp01(f / 10f); // normalize if needed
                        }
                    }
                    catch { }

                    try
                    {
                        // Try to read affinity data if exists (heuristic)
                        var affinityProp = maybeCustomerObject.GetType().GetProperty("AffinityData");
                        if (affinityProp != null)
                        {
                            var aff = affinityProp.GetValue(maybeCustomerObject);
                            if (aff != null)
                            {
                                // If affinity exposes PreferredEffects as IEnumerable<string>
                                var prefProp = aff.GetType().GetProperty("PreferredEffects");
                                if (prefProp != null)
                                {
                                    var val = prefProp.GetValue(aff) as System.Collections.IEnumerable;
                                    if (val != null)
                                    {
                                        foreach (var it in val)
                                        {
                                            if (it != null) profile.PreferredEffects.Add(it.ToString());
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                profile.Save();
                return profile;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomerSatisfactionProfile] GetOrCreate error for {customerId}: {ex.Message}");
                return new CustomerSatisfactionProfile { CustomerID = customerId };
            }
        }

        // -------------------- Deal recording --------------------
        // Decide if a deal is "good" by heuristics: match effect, quality >= min, price reasonable
        public bool IsGoodDeal(ProductDefinition product, EQuality quality, float price)
        {
            bool effectMatch = false;
            try
            {
                // Inspect product for any effect-like properties (similar to ContractPatches heuristics)
                var ptype = product.GetType();
                foreach (var prop in ptype.GetProperties())
                {
                    if (prop.Name.IndexOf("Effect", StringComparison.OrdinalIgnoreCase) >= 0 || prop.Name.IndexOf("Effects", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var val = prop.GetValue(product);
                        if (val is string s && !string.IsNullOrEmpty(s))
                        {
                            if (PreferredEffects.Contains(s)) { effectMatch = true; break; }
                            if (DislikedEffects.Contains(s)) { effectMatch = false; break; }
                        }
                        else if (val is System.Collections.IEnumerable en)
                        {
                            foreach (var it in en)
                            {
                                if (it == null) continue;
                                var nm = it.ToString();
                                if (PreferredEffects.Contains(nm)) { effectMatch = true; break; }
                                if (DislikedEffects.Contains(nm)) { effectMatch = false; break; }
                            }
                            if (effectMatch) break;
                        }
                    }
                }
            }
            catch { }

            bool qualityOk = true;
            try
            {
                qualityOk = ((int)quality) >= ((int)MinAcceptedQuality);
            }
            catch { }

            bool priceReasonable = true;
            try
            {
                var priceProp = product.GetType().GetProperty("Price");
                if (priceProp != null)
                {
                    var pval = priceProp.GetValue(product);
                    if (pval is float pf)
                    {
                        priceReasonable = price <= pf * 1.1f; // within 10% of base price
                    }
                }
            }
            catch { }

            // Compose final judgement: require quality and price, and prefer effect
            return effectMatch && qualityOk && priceReasonable;
        }

        public void RecordDeal(ProductDefinition product, EQuality quality, float price, bool wasGoodDeal)
        {
            try
            {
                if (wasGoodDeal)
                {
                    Satisfaction = Mathf.Clamp01(Satisfaction + 0.1f);
                    Trust = Mathf.Clamp01(Trust + 0.05f);
                    ConsecutiveGoodDeals++;
                    ConsecutiveBadDeals = 0;
                }
                else
                {
                    Satisfaction = Mathf.Clamp01(Satisfaction - 0.15f);
                    Trust = Mathf.Clamp01(Trust - 0.08f);
                    ConsecutiveBadDeals++;
                    ConsecutiveGoodDeals = 0;
                }

                LastPurchaseDate = DateTime.Now;
                DaysWithoutResponse = 0;
                Save();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomerSatisfactionProfile] RecordDeal failed for {CustomerID}: {ex.Message}");
            }
        }

        public void RecordNoResponse()
        {
            try
            {
                Trust = Mathf.Clamp01(Trust - 0.03f);
                AddictionLevel = Mathf.Clamp01(AddictionLevel - 0.05f);
                DaysWithoutResponse++;
                Save();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomerSatisfactionProfile] RecordNoResponse failed for {CustomerID}: {ex.Message}");
            }
        }

        // -------------------- Decision helpers --------------------
        public bool WillBuyFromPlayer(ProductDefinition product = null, EQuality? quality = null)
        {
            try
            {
                if (Satisfaction < 0.2f) return false;
                if (Trust < 0.15f) return false;
                if (AddictionLevel < 0.1f && Trust < 0.5f) return false;

                if (product != null)
                {
                    bool effectPreferred = false;
                    try
                    {
                        var ptype = product.GetType();
                        foreach (var prop in ptype.GetProperties())
                        {
                            if (prop.Name.IndexOf("Effect", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                var val = prop.GetValue(product);
                                if (val is string s && PreferredEffects.Contains(s)) { effectPreferred = true; break; }
                                if (val is System.Collections.IEnumerable en)
                                {
                                    foreach (var it in en) { if (it != null && PreferredEffects.Contains(it.ToString())) { effectPreferred = true; break; } }
                                    if (effectPreferred) break;
                                }
                            }
                        }
                    }
                    catch { }

                    if (!effectPreferred && Satisfaction < 0.6f) return false;

                    if (quality.HasValue)
                    {
                        try { if ((int)quality.Value < (int)MinAcceptedQuality && Trust < 0.7f) return false; } catch { }
                    }
                }

                return true;
            }
            catch { return true; }
        }

        public bool WillBuyFromRival()
        {
            try
            {
                if (Trust < 0.3f && AddictionLevel > 0.3f) return true;
                if (DaysWithoutResponse > 3 && AddictionLevel > 0.2f) return true;
                return false;
            }
            catch { return false; }
        }

        // -------------------- Utilities --------------------
        public static List<string> ExtractProductEffects(ProductDefinition product)
        {
            var outList = new List<string>();
            if (product == null) return outList;
            try
            {
                var ptype = product.GetType();
                foreach (var prop in ptype.GetProperties())
                {
                    if (prop.Name.IndexOf("Effect", StringComparison.OrdinalIgnoreCase) >= 0 || prop.Name.IndexOf("Effects", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var val = prop.GetValue(product);
                        if (val == null) continue;
                        if (val is string s && !string.IsNullOrEmpty(s)) outList.Add(s);
                        else if (val is System.Collections.IEnumerable en)
                        {
                            foreach (var it in en) if (it != null) outList.Add(it.ToString());
                        }
                        else
                        {
                            outList.Add(val.ToString());
                        }
                    }
                }
            }
            catch { }
            return outList;
        }
    }
}
