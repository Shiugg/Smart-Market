// Estado actual: Gestiona la memoria de compras por cliente y detecta tendencias virales.
// Riesgos si se modifica: Cambiar la forma de persistencia puede corromper archivos existentes.
// Integración propuesta: Usar persistencia de texto simple para evitar incompatibilidades de JSON en el runtime IL2CPP.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using MelonLogger = SmartMarket.SmartMarketLogger;
using MelonLoader;
using Il2CppScheduleOne.Product;

namespace SmartMarket.Core
{
    public class MemoryRecord
    {
        public string ProductID;
        public string ProductName;
        public string EffectGained;
        public float EnjoymentScore;
        public int DayPurchased;
        // Score computed at delivery time (new scoring architecture)
        public float Score;
    }

    public class CustomerMemory
    {
        public string CustomerID;
        public List<MemoryRecord> PurchaseHistory = new List<MemoryRecord>();
        public string PendingWordOfMouthProduct; // Producto que un amigo le recomendó
        // Pending requested info (set when the game notifies player of a contract)
        public string PendingRequestedProductId; // product id expected in the pending offer
        // Legacy singular fields — kept for backwards compat; scoring/handover still read these
        public string PendingRequestedEffectId; // stable effect identifier expected in the pending offer (first/primary)
        public string PendingRequestedEffectName; // display effect name expected in the pending offer (first/primary)
        // Multi-effect lists (new) — populated when a product has multiple effects
        public List<string> PendingRequestedEffectIds = new List<string>();
        public List<string> PendingRequestedEffectNames = new List<string>();
        public string PendingRequestedQuality; // requested product quality expected in the pending offer
        public int PendingRequestedQuantity = 0; // requested quantity (0 = unspecified)
        // Outgoing messages queued for injection into phone UI (kept per customer)
        public List<string> PendingOutgoingMessages = new List<string>();
    }

    public static class MemorySystem
    {
        private const string FileHeader = "# SmartMarketMemory v1";
        private static string SavePath => Path.Combine(Application.persistentDataPath, "SmartMarket_Memory.json");
        private static Dictionary<string, CustomerMemory> _memories = new Dictionary<string, CustomerMemory>();
        private static int _lastViralDayToken = -1;
        private static int _dailyViralEvents = 0;
        private static Dictionary<string, int> _dailyProductUsage = new Dictionary<string, int>();
        // Default kept for backwards compatibility; can be overridden by config
        private static int MaxDailyViralEvents => SmartMarketConfig.MaxDailyViralEvents;

        // Neighborhood-level recent recommendations (productId, sourceCustomerName, dayToken)
        private class NeighborhoodRecommendation { public string ProductId; public string SourceName; public int DayToken; }
        private static Dictionary<Neighborhood, List<NeighborhoodRecommendation>> _neighborhoodRecs = new Dictionary<Neighborhood, List<NeighborhoodRecommendation>>();
 
        private static string Escape(string value)
        {
            return value == null ? string.Empty : System.Uri.EscapeDataString(value);
        }

        private static string Unescape(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : System.Uri.UnescapeDataString(value);
        }

        public static void Init()
        {
            _memories = new Dictionary<string, CustomerMemory>();
            if (!File.Exists(SavePath))
            {
                MelonLogger.Msg("MEMORY", "No se encontró archivo de memoria. Iniciando en blanco.");
                return;
            }
 
            try
            {
                MelonLogger.Msg("MEMORY", $"Cargando memoria desde: {SavePath}");
                var lines = File.ReadAllLines(SavePath);
                if (lines.Length == 0)
                {
                    MelonLogger.Msg("MEMORY", "Archivo de memoria vacío. Usando datos en blanco.");
                    return;
                }
 
                if (!lines[0].StartsWith(FileHeader))
                {
                    MelonLogger.Warning("MEMORY", "Encabezado de archivo de memoria no coincide. Se usará memoria vacía.");
                    throw new System.InvalidOperationException("Formato de archivo de memoria desconocido");
                }

                CustomerMemory currentCustomer = null;
                foreach (var rawLine in lines)
                {
                    if (string.IsNullOrWhiteSpace(rawLine) || rawLine.StartsWith("#"))
                        continue;

                    var line = rawLine.Trim();
                    if (line.StartsWith("CustomerID:"))
                    {
                        currentCustomer = new CustomerMemory { CustomerID = Unescape(line.Substring("CustomerID:".Length)) };
                        continue;
                    }

                    if (line.StartsWith("PendingWordOfMouthProduct:"))
                    {
                        if (currentCustomer != null)
                            currentCustomer.PendingWordOfMouthProduct = Unescape(line.Substring("PendingWordOfMouthProduct:".Length));
                        continue;
                    }

                    if (line.StartsWith("PendingRequestedProductId:"))
                    {
                        if (currentCustomer != null)
                            currentCustomer.PendingRequestedProductId = Unescape(line.Substring("PendingRequestedProductId:".Length));
                        continue;
                    }

                    if (line.StartsWith("PendingRequestedEffectId:"))
                    {
                        if (currentCustomer != null)
                            currentCustomer.PendingRequestedEffectId = Unescape(line.Substring("PendingRequestedEffectId:".Length));
                        continue;
                    }

                    if (line.StartsWith("PendingRequestedEffectName:"))
                    {
                        if (currentCustomer != null)
                            currentCustomer.PendingRequestedEffectName = Unescape(line.Substring("PendingRequestedEffectName:".Length));
                        continue;
                    }

                    if (line.StartsWith("PendingRequestedEffectIds:"))
                    {
                        if (currentCustomer != null)
                        {
                            var val = Unescape(line.Substring("PendingRequestedEffectIds:".Length));
                            if (!string.IsNullOrEmpty(val))
                            {
                                if (currentCustomer.PendingRequestedEffectIds == null)
                                    currentCustomer.PendingRequestedEffectIds = new List<string>();
                                foreach (var id in val.Split('|'))
                                    if (!string.IsNullOrEmpty(id)) currentCustomer.PendingRequestedEffectIds.Add(id);
                            }
                        }
                        continue;
                    }

                    if (line.StartsWith("PendingRequestedEffectNames:"))
                    {
                        if (currentCustomer != null)
                        {
                            var val = Unescape(line.Substring("PendingRequestedEffectNames:".Length));
                            if (!string.IsNullOrEmpty(val))
                            {
                                if (currentCustomer.PendingRequestedEffectNames == null)
                                    currentCustomer.PendingRequestedEffectNames = new List<string>();
                                foreach (var nm in val.Split('|'))
                                    if (!string.IsNullOrEmpty(nm)) currentCustomer.PendingRequestedEffectNames.Add(nm);
                            }
                        }
                        continue;
                    }

                    if (line.StartsWith("PendingRequestedQuality:"))
                    {
                        if (currentCustomer != null)
                            currentCustomer.PendingRequestedQuality = Unescape(line.Substring("PendingRequestedQuality:".Length));
                        continue;
                    }

                    if (line.StartsWith("PendingRequestedQuantity:"))
                    {
                        if (currentCustomer != null && int.TryParse(Unescape(line.Substring("PendingRequestedQuantity:".Length)), out int q))
                            currentCustomer.PendingRequestedQuantity = q;
                        continue;
                    }

                    if (line.StartsWith("PendingMsg:"))
                    {
                    if (currentCustomer != null)
                    {
                        var msg = Unescape(line.Substring("PendingMsg:".Length));
                        if (!string.IsNullOrEmpty(msg)) currentCustomer.PendingOutgoingMessages.Add(msg);
                    }
                    continue;
                    }

                    if (line.StartsWith("Record:"))
                    {
                        if (currentCustomer == null)
                            continue;

                        var payload = line.Substring("Record:".Length);
                        var parts = payload.Split('|');
                        if (parts.Length >= 5)
                        {
                            if (float.TryParse(Unescape(parts[3]), out float enjoyment) && int.TryParse(Unescape(parts[4]), out int dayPurchased))
                            {
                                float score = 0f;
                                if (parts.Length >= 6)
                                    float.TryParse(Unescape(parts[5]), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out score);

                                currentCustomer.PurchaseHistory.Add(new MemoryRecord
                                {
                                    ProductID = Unescape(parts[0]),
                                    ProductName = Unescape(parts[1]),
                                    EffectGained = Unescape(parts[2]),
                                    EnjoymentScore = enjoyment,
                                    DayPurchased = dayPurchased,
                                    Score = score
                                });
                            }
                        }
                        continue;
                    }

                    if (line == "EndCustomer")
                    {
                        if (currentCustomer != null && !string.IsNullOrEmpty(currentCustomer.CustomerID))
                        {
                            _memories[currentCustomer.CustomerID] = currentCustomer;
                        }
                        currentCustomer = null;
                        continue;
                    }
                }

                if (_memories.Count > 0)
                {
                    MelonLogger.Msg($"[SmartMarket] Memoria cargada exitosamente: {_memories.Count} clientes recordados.");
                    return;
                }

                MelonLogger.Msg("[SmartMarket] Archivo de memoria válido pero no se encontraron clientes. Usando datos en blanco.");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] No se pudo cargar la memoria. Se usará memoria vacía. Error: {ex.Message}");
                _memories = new Dictionary<string, CustomerMemory>();

                try
                {
                    string backupPath = SavePath + ".bad";
                    if (File.Exists(SavePath))
                    {
                        File.Move(SavePath, backupPath);
                        MelonLogger.Warning($"[SmartMarket] Archivo de memoria incompatible movido a: {backupPath}");
                    }
                }
                catch (System.Exception moveEx)
                {
                    MelonLogger.Warning($"[SmartMarket] No se pudo mover el archivo de memoria corrupto: {moveEx.Message}");
                }
            }
        }

        public static void Save()
        {
            try
            {
                using (var writer = new StreamWriter(SavePath, false))
                {
                    writer.WriteLine(FileHeader);
                    foreach (var memory in _memories.Values)
                    {
                        writer.WriteLine($"CustomerID:{Escape(memory.CustomerID)}");
                        writer.WriteLine($"PendingWordOfMouthProduct:{Escape(memory.PendingWordOfMouthProduct)}");
                        writer.WriteLine($"PendingRequestedProductId:{Escape(memory.PendingRequestedProductId)}");
                        writer.WriteLine($"PendingRequestedEffectId:{Escape(memory.PendingRequestedEffectId)}");
                        writer.WriteLine($"PendingRequestedEffectName:{Escape(memory.PendingRequestedEffectName)}");
                        // Multi-effect lists
                        if (memory.PendingRequestedEffectIds != null && memory.PendingRequestedEffectIds.Count > 0)
                            writer.WriteLine($"PendingRequestedEffectIds:{Escape(string.Join("|", memory.PendingRequestedEffectIds))}");
                        if (memory.PendingRequestedEffectNames != null && memory.PendingRequestedEffectNames.Count > 0)
                            writer.WriteLine($"PendingRequestedEffectNames:{Escape(string.Join("|", memory.PendingRequestedEffectNames))}");
                        writer.WriteLine($"PendingRequestedQuality:{Escape(memory.PendingRequestedQuality)}");
                        writer.WriteLine($"PendingRequestedQuantity:{Escape(memory.PendingRequestedQuantity.ToString())}");
                        // write any pending outgoing messages
                        if (memory.PendingOutgoingMessages != null)
                        {
                            foreach (var pm in memory.PendingOutgoingMessages)
                            {
                                writer.WriteLine($"PendingMsg:{Escape(pm)}");
                            }
                        }

                        foreach (var record in memory.PurchaseHistory)
                        {
                            // Format: ProductID|ProductName|EffectGained|Enjoyment|DayPurchased|Score
                            writer.WriteLine($"Record:{Escape(record.ProductID)}|{Escape(record.ProductName)}|{Escape(record.EffectGained)}|{Escape(record.EnjoymentScore.ToString(System.Globalization.CultureInfo.InvariantCulture))}|{Escape(record.DayPurchased.ToString())}|{Escape(record.Score.ToString(System.Globalization.CultureInfo.InvariantCulture))}");
                        }
                        writer.WriteLine("EndCustomer");
                    }
                }

                if (SmartMarketConfig.DebugEnabled)
                    MelonLogger.Msg($"[SmartMarket] MemorySystem saved {_memories.Count} memories to {SavePath}");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] Error guardando la memoria: {ex.Message}");
            }
        }

        public static CustomerMemory GetMemory(string customerId)
        {
            if (string.IsNullOrEmpty(customerId)) return null;
            if (!_memories.ContainsKey(customerId))
            {
                _memories[customerId] = new CustomerMemory { CustomerID = customerId };
                SmartMarketConfig.LogDebug($"MemorySystem: Created new memory for {customerId}");
            }
            else
            {
                SmartMarketConfig.LogDebug($"MemorySystem: Retrieved existing memory for {customerId} (PendingWOM='{_memories[customerId].PendingWordOfMouthProduct}', PendingMsgs={_memories[customerId].PendingOutgoingMessages?.Count ?? 0})");
            }
            return _memories[customerId];
        }

        public static void ClearPendingRequest(string customerId)
        {
            if (string.IsNullOrEmpty(customerId)) return;
            try
            {
                var mem = GetMemory(customerId);
                if (mem != null)
                {
                    mem.PendingRequestedProductId = "";
                    mem.PendingRequestedEffectId = "";
                    mem.PendingRequestedEffectName = "";
                    mem.PendingRequestedEffectIds?.Clear();
                    mem.PendingRequestedEffectNames?.Clear();
                    mem.PendingRequestedQuality = "";
                    mem.PendingRequestedQuantity = 0;
                    mem.PendingOutgoingMessages?.Clear();
                    mem.PendingWordOfMouthProduct = "";
                    Save();
                }
            }
            catch (System.Exception ex)
            {
                MelonLoader.MelonLogger.Warning($"[SmartMarket] Error clearing pending request for {customerId}: {ex.Message}");
            }
        }

        private static int GetCurrentDayToken()
        {
            var now = System.DateTime.UtcNow;
            return now.Year * 1000 + now.DayOfYear;
        }

        private static void ResetDailyViralCounterIfNeeded()
        {
            int currentDay = GetCurrentDayToken();
            if (_lastViralDayToken != currentDay)
            {
                _lastViralDayToken = currentDay;
                _dailyViralEvents = 0;
                _dailyProductUsage.Clear();
            }
        }

        public static bool CanTriggerViralEvent()
        {
            ResetDailyViralCounterIfNeeded();
            return _dailyViralEvents < MaxDailyViralEvents;
        }

        public static void RecordViralEvent(string productId)
        {
            ResetDailyViralCounterIfNeeded();
            _dailyViralEvents++;
            if (!string.IsNullOrEmpty(productId))
            {
                if (!_dailyProductUsage.ContainsKey(productId))
                    _dailyProductUsage[productId] = 0;
                _dailyProductUsage[productId]++;
            }
            SmartMarketConfig.LogDebug($"MemorySystem: Recorded viral event for product '{productId}'. DailyViralEvents={_dailyViralEvents}");
        }

        private static int GetDailyProductUsage(string productId)
        {
            ResetDailyViralCounterIfNeeded();
            return string.IsNullOrEmpty(productId) || !_dailyProductUsage.ContainsKey(productId) ? 0 : _dailyProductUsage[productId];
        }

        public static string SelectViralProductForNeighborhood(Neighborhood neighborhood, Il2CppSystem.Collections.Generic.List<ProductDefinition> availableProducts = null)
        {
            var candidates = new List<ProductDefinition>();
            if (availableProducts != null)
            {
                foreach (var product in availableProducts)
                {
                    if (product != null)
                        candidates.Add(product);
                }
            }

            if (candidates.Count == 0 && ProductManager.Instance != null && ProductManager.Instance.AllProducts != null)
            {
                foreach (var product in ProductManager.Instance.AllProducts)
                {
                    if (product != null)
                        candidates.Add(product);
                }
            }

            if (candidates.Count == 0)
                return string.Empty;

            var weights = new Dictionary<string, float>();
            float totalWeight = 0f;
            foreach (var product in candidates)
            {
                string productId = !string.IsNullOrEmpty(product.SaveFileName) ? product.SaveFileName : product.name;
                if (string.IsNullOrEmpty(productId) || weights.ContainsKey(productId))
                    continue;

                float baseWeight = CalculateViralWeight(productId, neighborhood);
                int usage = GetDailyProductUsage(productId);
                float penalty = 1f - Mathf.Clamp01(0.2f * usage);
                float weight = Mathf.Max(baseWeight * penalty, 0.1f);
                weights[productId] = weight;
                totalWeight += weight;
            }

            if (weights.Count == 0)
                return string.Empty;

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;
            string chosen = string.Empty;
            foreach (var kvp in weights)
            {
                cumulative += kvp.Value;
                if (roll <= cumulative)
                {
                    chosen = kvp.Key;
                    break;
                }
            }
            if (string.IsNullOrEmpty(chosen)) chosen = weights.Keys.First();

            // Debug: print weights and selection to help trace why viral product was/was not chosen
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"MemorySystem: Viral selection for {neighborhood} roll={roll:0.00} total={totalWeight:0.00} chosen={chosen}");
                foreach (var kvp in weights)
                {
                    sb.AppendLine($"  {kvp.Key} -> weight={kvp.Value:0.000}");
                }
                SmartMarketConfig.LogDebug(sb.ToString());
            }
            catch { }

            return chosen;
        }

        private static float CalculateViralWeight(string productId, Neighborhood neighborhood)
        {
            string productKey = productId + "_" + neighborhood.ToString();
            if (_memories.ContainsKey(productKey) && _memories[productKey].PurchaseHistory.Count > 0)
            {
                var record = _memories[productKey].PurchaseHistory[^1];
                float latestHype = record.EnjoymentScore;
                float countBonus = Mathf.Log(1f + _memories[productKey].PurchaseHistory.Count);
                float weight = 1f + Mathf.Clamp(latestHype * 0.5f + countBonus * 0.5f, 0.5f, 8f);
                return Mathf.Max(weight, 1f);
            }

            return 1f;
        }

        public static void AddPurchaseRecord(string customerId, string productId, string productName, string effect, float enjoyment, float addictiveness, float price, ConsumerProfile profile, float score = 0f)
        {
            var memory = GetMemory(customerId);
            var record = new MemoryRecord
            {
                ProductID = productId,
                ProductName = productName,
                EffectGained = effect,
                EnjoymentScore = enjoyment,
                DayPurchased = System.DateTime.UtcNow.DayOfYear,
                Score = score
            };
            memory.PurchaseHistory.Add(record);

            SmartMarketConfig.LogDebug($"MemorySystem: Added purchase record for {customerId} -> {productId} (effect='{effect}') enjoyment={enjoyment:0.00} score={score:0.00}");

            if (profile != null && profile.HomeNeighborhood != default)
            {
                ProcessForViralDemand(productId, productName, addictiveness, price, profile.HomeNeighborhood);
                // Register neighborhood-level recommendation source for propagation
                RegisterNeighborhoodRecommendation(profile.HomeNeighborhood, productId, customerId);
            }

            Save();
        }

        private static void RegisterNeighborhoodRecommendation(Neighborhood neighborhood, string productId, string sourceName)
        {
            try
            {
                ResetDailyViralCounterIfNeeded();
                int day = GetCurrentDayToken();
                if (!_neighborhoodRecs.ContainsKey(neighborhood))
                    _neighborhoodRecs[neighborhood] = new List<NeighborhoodRecommendation>();

                _neighborhoodRecs[neighborhood].Add(new NeighborhoodRecommendation { ProductId = productId, SourceName = sourceName, DayToken = day });

                // Trim to last 50 entries to avoid unbounded growth
                var list = _neighborhoodRecs[neighborhood];
                if (list.Count > 50)
                    list.RemoveRange(0, list.Count - 50);
            }
            catch { }
        }

        public static (string productId, string sourceName) GetNeighborhoodRecommendationForCustomer(string customerId, Neighborhood neighborhood)
        {
            try
            {
                ResetDailyViralCounterIfNeeded();
                if (!_neighborhoodRecs.ContainsKey(neighborhood) || _neighborhoodRecs[neighborhood].Count == 0)
                    return (string.Empty, string.Empty);

                // Build weighted list favoring recent entries and those with higher hype
                var candidates = new List<(NeighborhoodRecommendation rec, float weight)>();
                int nowDay = GetCurrentDayToken();
                foreach (var rec in _neighborhoodRecs[neighborhood])
                {
                    if (rec == null || string.IsNullOrEmpty(rec.ProductId)) continue;
                    float ageFactor = Mathf.Clamp01(1f - ((nowDay - rec.DayToken) / 7f)); // prefer last week
                    float usagePenalty = 1f - Mathf.Clamp01(0.2f * GetDailyProductUsage(rec.ProductId));
                    float weight = 1f * ageFactor * usagePenalty;
                    if (weight <= 0f) continue;
                    candidates.Add((rec, weight));
                }

                if (candidates.Count == 0) return (string.Empty, string.Empty);

                float total = 0f; foreach (var c in candidates) total += c.weight;
                float roll = UnityEngine.Random.Range(0f, total);
                float acc = 0f;
                foreach (var c in candidates)
                {
                    acc += c.weight;
                    if (roll <= acc)
                    {
                        // small randomness whether this recommendation actually reaches the customer
                        if (UnityEngine.Random.Range(0f, 1f) <= SmartMarketConfig.NeighborhoodRecommendationReachScore / 10f)
                        {
                            SmartMarketConfig.LogDebug($"MemorySystem: Recommendation reached customer {customerId} -> product {c.rec.ProductId} from source {c.rec.SourceName}");
                            return (c.rec.ProductId, c.rec.SourceName);
                        }
                        else
                        {
                            SmartMarketConfig.LogDebug($"MemorySystem: Recommendation sampled but did NOT reach customer {customerId} -> product {c.rec.ProductId} (source {c.rec.SourceName})");
                        }
                        break;
                    }
                }
            }
            catch { }

            return (string.Empty, string.Empty);
        }

        private static void ProcessForViralDemand(string productId, string productName, float addictiveness, float price, Neighborhood neighborhood)
        {
            // Preferir ProductID para los registros virales, pero almacenar el nombre legible también.
            string productKey = (!string.IsNullOrEmpty(productId) ? productId : productName) + "_" + neighborhood.ToString();
            
            if (!_memories.ContainsKey(productKey))
            {
                _memories[productKey] = new CustomerMemory { CustomerID = productKey };
            }

            var productHypeRecord = _memories[productKey];
            if (productHypeRecord.PurchaseHistory.Count == 0)
            {
                productHypeRecord.PurchaseHistory.Add(new MemoryRecord
                {
                    ProductID = productId,
                    ProductName = productName,
                    EffectGained = $"Adicción: {addictiveness}, Precio: {price}",
                    EnjoymentScore = (addictiveness * 0.7f) + (Mathf.Clamp01((100f - price) / 100f) * 0.3f)
                });
                return;
            }

            float currentHype = productHypeRecord.PurchaseHistory[^1].EnjoymentScore;
            float newHypeScore = (addictiveness * 0.7f) + (Mathf.Clamp01((100f - price) / 100f) * 0.3f);
            float hypeMultiplier = 1.0f + (currentHype / 10f);
            productHypeRecord.PurchaseHistory.Add(new MemoryRecord
            {
                ProductID = productId,
                ProductName = productName,
                EffectGained = $"Adicción: {addictiveness}, Precio: {price}",
                EnjoymentScore = newHypeScore * hypeMultiplier
            });
        }

        public static bool CheckForViralDemand(string customerId, Neighborhood neighborhood)
        {
            // Omitido temporalmente para no causar crasheos
            return false;
        }

        // Nueva función para obtener el producto más vendido/viral de un barrio
        public static string GetTopViralProductForNeighborhood(Neighborhood neighborhood)
        {
            string bestProductId = "";
            float highestHype = -1f;

            foreach (var kvp in _memories)
            {
                if (kvp.Key.EndsWith("_" + neighborhood.ToString()) && kvp.Value.PurchaseHistory.Count > 0)
                {
                    float currentHype = kvp.Value.PurchaseHistory[^1].EnjoymentScore;
                    if (currentHype > highestHype)
                    {
                        highestHype = currentHype;
                        bestProductId = !string.IsNullOrEmpty(kvp.Value.PurchaseHistory[^1].ProductID)
                            ? kvp.Value.PurchaseHistory[^1].ProductID
                            : kvp.Value.PurchaseHistory[^1].ProductName;
                    }
                }
            }

            if (string.IsNullOrEmpty(bestProductId))
            {
                Dictionary<string, int> productCounts = new Dictionary<string, int>();
                foreach (var memory in _memories.Values)
                {
                    foreach (var record in memory.PurchaseHistory)
                    {
                        string key = !string.IsNullOrEmpty(record.ProductID) ? record.ProductID : record.ProductName;
                        if (string.IsNullOrEmpty(key) || key.Contains("Adicción:"))
                            continue;

                        if (!productCounts.ContainsKey(key)) productCounts[key] = 0;
                        productCounts[key]++;
                    }
                }

                int maxCount = 0;
                foreach (var kvp in productCounts)
                {
                    if (kvp.Value > maxCount)
                    {
                        maxCount = kvp.Value;
                        bestProductId = kvp.Key;
                    }
                }
            }

            if (!string.IsNullOrEmpty(bestProductId))
                return bestProductId;

            if (ProductManager.Instance != null && ProductManager.Instance.AllProducts != null && ProductManager.Instance.AllProducts.Count > 0)
            {
                var fallback = ProductManager.Instance.AllProducts[UnityEngine.Random.Range(0, ProductManager.Instance.AllProducts.Count)];
                if (fallback != null)
                    return !string.IsNullOrEmpty(fallback.SaveFileName) ? fallback.SaveFileName : fallback.name;
            }

            return string.Empty;
        }
    }
}
