using System;
using System.IO;
using UnityEngine;
using MelonLogger = SmartMarket.SmartMarketLogger;
using MelonLoader;

namespace SmartMarket.Core
{
    public static class SmartMarketConfig
    {
        private const string FileName = "SmartMarket_Config.txt";
        private const string EventsConfigFileName = "SmartMarket_Events.json";
        private static string ConfigPath => Path.Combine(Application.persistentDataPath, FileName);
        private static string EventsConfigPath => Path.Combine(Application.persistentDataPath, EventsConfigFileName);

        // Tunable values
        public static float NeighborhoodRecommendationAcceptanceScore { get; set; } = 2.5f; // 0-10 scale; 1 = 10%, 10 = 100%
        public static float NeighborhoodRecommendationReachScore { get; set; } = 3.5f; // 0-10 scale; 1 = 10%, 10 = 100%
        public static int MaxDailyViralEvents { get; set; } = 6;
        public static float WordOfMouthChance { get; set; } = 0.5f; // PROBLEM 4: Probability (0-1) that Word of Mouth event is accepted as such

        public static bool DebugEnabled { get; set; } = false;
        // Seasonal change event enabled (global profile)
        public static bool SeasonalChangeEnabled { get; set; } = true; // enabled by default

        // Product scoring weights (influence customer valuation)
        // Defaults adjusted for testing (less extreme than production-high)
        // Recommended test set: effect and quality matter but are not overpowering.
        public static float EffectMatchWeight { get; set; } = 1.5f; // multiplier when product has preferred effect
        public static float EffectMismatchWeight { get; set; } = 0.20f; // multiplier when product has disliked effect
        public static float QualityMatchWeight { get; set; } = 1.5f; // multiplier when product quality >= min
        public static float QualityMismatchWeight { get; set; } = 0.15f; // penalty when quality below min
        public static float AddictionWeight { get; set; } = 1.0f;
        public static float TrustWeight { get; set; } = 1.0f;
        public static float SatisfactionWeight { get; set; } = 1.0f;

        // Message placeholder colors (Hex format, e.g. #RRGGBB). These control how dynamic values are colored in SMS messages.
        public static string ProductColorHex { get; set; } = "#f39c12"; // orange
        public static string EffectColorHex { get; set; } = "#8e44ad"; // purple
        public static string QualityColorHex { get; set; } = "#27ae60"; // green
        public static string QuantityColorHex { get; set; } = "#d35400"; // darker orange
        public static string PriceColorHex { get; set; } = "#c0392b"; // red


        // New weights: under-supply (delivering less quantity than requested) and wrong product (selling a different product)
        public static float UnderSupplyWeight { get; set; } = 0.5f; // penalty applied when sold quantity < requested
        public static float WrongProductWeight { get; set; } = 0.75f; // penalty when product doesn't match customer's requested product


        // Events configuration
        public static EventsConfig Events { get; set; } = new EventsConfig();

        static SmartMarketConfig()
        {
            Load();
            LoadEvents();
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    Save();
                    MelonLogger.Msg($"[SmartMarket] Config file not found. Creating default at: {ConfigPath}");
                    return;
                }

                var lines = File.ReadAllLines(ConfigPath);
                foreach (var raw in lines)
                {
                    var line = raw.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                    var parts = line.Split('=');
                    if (parts.Length != 2) continue;
                    var key = parts[0].Trim();
                    var val = parts[1].Trim();
                    try
                    {
                        switch (key)
                        {
                            case "NeighborhoodRecommendationAcceptanceScore":
                            case "NeighborhoodRecommendationAcceptanceChance":
                                NeighborhoodRecommendationAcceptanceScore = float.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
                                if (NeighborhoodRecommendationAcceptanceScore <= 1f)
                                    NeighborhoodRecommendationAcceptanceScore *= 10f;
                                NeighborhoodRecommendationAcceptanceScore = Mathf.Clamp(NeighborhoodRecommendationAcceptanceScore, 0f, 10f);
                                break;
                            case "NeighborhoodRecommendationReachScore":
                            case "NeighborhoodRecommendationReachChance":
                                NeighborhoodRecommendationReachScore = float.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
                                if (NeighborhoodRecommendationReachScore <= 1f)
                                    NeighborhoodRecommendationReachScore *= 10f;
                                NeighborhoodRecommendationReachScore = Mathf.Clamp(NeighborhoodRecommendationReachScore, 0f, 10f);
                                break;
                            case "MaxDailyViralEvents": MaxDailyViralEvents = int.Parse(val); break;
                            case "DebugEnabled": DebugEnabled = bool.Parse(val); break;
                            case "SeasonalChangeEnabled": SeasonalChangeEnabled = bool.Parse(val); break;
                            case "EffectMatchWeight": EffectMatchWeight = float.Parse(val, System.Globalization.CultureInfo.InvariantCulture); break;
                            case "EffectMismatchWeight": EffectMismatchWeight = float.Parse(val, System.Globalization.CultureInfo.InvariantCulture); break;
                            case "QualityMatchWeight": QualityMatchWeight = float.Parse(val, System.Globalization.CultureInfo.InvariantCulture); break;
                            case "QualityMismatchWeight": QualityMismatchWeight = float.Parse(val, System.Globalization.CultureInfo.InvariantCulture); break;
                            case "AddictionWeight": AddictionWeight = float.Parse(val, System.Globalization.CultureInfo.InvariantCulture); break;
                            case "TrustWeight": TrustWeight = float.Parse(val, System.Globalization.CultureInfo.InvariantCulture); break;
                            case "SatisfactionWeight": SatisfactionWeight = float.Parse(val, System.Globalization.CultureInfo.InvariantCulture); break;
                            case "UnderSupplyWeight": UnderSupplyWeight = float.Parse(val, System.Globalization.CultureInfo.InvariantCulture); break;
                            case "WrongProductWeight": WrongProductWeight = float.Parse(val, System.Globalization.CultureInfo.InvariantCulture); break;

                            // Message color config (hex color strings)
                            case "ProductColorHex": ProductColorHex = val; break;
                            case "EffectColorHex": EffectColorHex = val; break;
                            case "QualityColorHex": QualityColorHex = val; break;
                            case "QuantityColorHex": QuantityColorHex = val; break;
                            case "PriceColorHex": PriceColorHex = val; break;
                        }
                    }
                    catch { }
                }

                MelonLogger.Msg($"[SmartMarket] Config loaded from: {ConfigPath}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] Error loading config: {ex.Message}");
            }
        }

        public static void Save()
        {
            try
            {
                using (var writer = new StreamWriter(ConfigPath, false))
                {
                    writer.WriteLine("# SmartMarket configuration - editable values");
                    writer.WriteLine("# NeighborhoodRecommendationAcceptanceScore: rating for recommendation acceptance, scale 0-10 (1=10%, 10=100%)");
                    writer.WriteLine($"NeighborhoodRecommendationAcceptanceScore={NeighborhoodRecommendationAcceptanceScore.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    writer.WriteLine("# NeighborhoodRecommendationReachScore: rating for recommendation reach, scale 0-10 (1=10%, 10=100%)");
                    writer.WriteLine($"NeighborhoodRecommendationReachScore={NeighborhoodRecommendationReachScore.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    writer.WriteLine("# MaxDailyViralEvents: maximum viral events allowed per day");
                    writer.WriteLine($"MaxDailyViralEvents={MaxDailyViralEvents}");
                    writer.WriteLine("# DebugEnabled: true/false to enable extra debug logging");
                    writer.WriteLine($"DebugEnabled={DebugEnabled}");
                    writer.WriteLine("# SeasonalChangeEnabled: true/false to enable the automatic seasonal-change event (no params)");
                    writer.WriteLine($"SeasonalChangeEnabled={SeasonalChangeEnabled}");

                    writer.WriteLine("# Product scoring weights (used to influence customer valuation)");
                    writer.WriteLine("# EffectMatchWeight: multiplier applied when product has a preferred effect (default 2.0)");
                    writer.WriteLine($"EffectMatchWeight={EffectMatchWeight.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    writer.WriteLine("# EffectMismatchWeight: multiplier applied when product has a disliked effect (default 0.25)");
                    writer.WriteLine($"EffectMismatchWeight={EffectMismatchWeight.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    writer.WriteLine("# QualityMatchWeight: multiplier when quality >= min accepted (default 2.0)");
                    writer.WriteLine($"QualityMatchWeight={QualityMatchWeight.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    writer.WriteLine("# QualityMismatchWeight: multiplier when quality < min accepted (default 0.2)");
                    writer.WriteLine($"QualityMismatchWeight={QualityMismatchWeight.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    writer.WriteLine("# AddictionWeight / TrustWeight / SatisfactionWeight: relative weights used when scoring products");
                    writer.WriteLine($"AddictionWeight={AddictionWeight.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    writer.WriteLine($"TrustWeight={TrustWeight.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    writer.WriteLine($"SatisfactionWeight={SatisfactionWeight.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    writer.WriteLine("# UnderSupplyWeight: penalización cuando se entrega menos cantidad que la solicitada (aplica al score antes de aceptar)");
                    writer.WriteLine($"UnderSupplyWeight={UnderSupplyWeight.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    writer.WriteLine("# WrongProductWeight: penalización cuando el producto ofrecido no coincide con el solicitado por el cliente (aplica al score)");
                    writer.WriteLine($"WrongProductWeight={WrongProductWeight.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

                    // Message placeholder color configuration (hex color strings, e.g. #RRGGBB)
                    writer.WriteLine("# ProductColorHex: color para el nombre del producto/droga");
                    writer.WriteLine($"ProductColorHex={ProductColorHex}");
                    writer.WriteLine("# EffectColorHex: color para el nombre del efecto");
                    writer.WriteLine($"EffectColorHex={EffectColorHex}");
                    writer.WriteLine("# QualityColorHex: color para la calidad (Premium, Standard, etc.)");
                    writer.WriteLine($"QualityColorHex={QualityColorHex}");
                    writer.WriteLine("# QuantityColorHex: color para las cantidades solicitadas (ej. 4 g)");
                    writer.WriteLine($"QuantityColorHex={QuantityColorHex}");
                    writer.WriteLine("# PriceColorHex: color para precios cuando aparezcan en mensajes (ej. $500)");
                    writer.WriteLine($"PriceColorHex={PriceColorHex}");
                }

                MelonLogger.Msg($"[SmartMarket] Config saved to: {ConfigPath}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] Error saving config: {ex.Message}");
            }
        }

        public static void LogDebug(string message)
        {
            if (DebugEnabled)
                MelonLogger.Msg("[SmartMarket][DEBUG] " + message);
        }

        public static void LoadEvents()
        {
            try
            {
                if (!File.Exists(EventsConfigPath))
                {
                    MelonLogger.Msg($"[SmartMarket] Events config not found. Creating defaults at: {EventsConfigPath}");
                    SaveEvents();
                    return;
                }

                string json = File.ReadAllText(EventsConfigPath);
                Events = JsonUtilityHelper.FromJson<EventsConfig>(json);
                MelonLogger.Msg($"[SmartMarket] Events config loaded from: {EventsConfigPath}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] Error loading events config: {ex.Message}. Using defaults.");
                Events = new EventsConfig();
                SaveEvents();
            }
        }

        public static void SaveEvents()
        {
            try
            {
                string json = JsonUtilityHelper.ToJson(Events, true);
                File.WriteAllText(EventsConfigPath, json);
                MelonLogger.Msg($"[SmartMarket] Events config saved to: {EventsConfigPath}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] Error saving events config: {ex.Message}");
            }
        }
    }

    // Helper para serializar/deserializar EventsConfig sin depender de JsonUtility (que falla en Il2Cpp)
    public static class JsonUtilityHelper
    {
        public static string ToJson<T>(T obj, bool prettyPrint = false)
        {
            // Fallback: simple manual serialization (JsonUtility no está disponible en Il2Cpp)
            return SimpleJsonSerialize(obj);
        }

        public static T FromJson<T>(string json)
        {
            // Fallback: return new instance with defaults (JsonUtility no está disponible en Il2Cpp)
            return (T)Activator.CreateInstance(typeof(T));
        }

        private static string SimpleJsonSerialize(object obj)
        {
            // Very basic JSON serialization for EventsConfig
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");

            var type = obj.GetType();
            var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            bool first = true;
            foreach (var prop in properties)
            {
                if (!first) sb.Append(",\n");
                first = false;

                object value = prop.GetValue(obj);
                string jsonValue = SerializeValue(value);
                sb.Append($"  \"{prop.Name}\": {jsonValue}");
            }

            sb.AppendLine();
            sb.Append("}");
            return sb.ToString();
        }

        private static string SerializeValue(object value)
        {
            if (value == null) return "null";
            if (value is bool b) return b ? "true" : "false";
            if (value is int i) return i.ToString();
            if (value is float f) return f.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (value is string s) return $"\"{EscapeJsonString(s)}\"";
            if (value is System.Collections.IEnumerable list && !(value is string))
            {
                var arr = new System.Text.StringBuilder("[");
                bool first = true;
                foreach (var item in (System.Collections.IEnumerable)list)
                {
                    if (!first) arr.Append(", ");
                    first = false;
                    arr.Append(SerializeValue(item));
                }
                arr.Append("]");
                return arr.ToString();
            }
            return value.ToString();
        }

        private static string EscapeJsonString(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
