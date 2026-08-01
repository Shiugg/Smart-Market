using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using MelonLogger = SmartMarket.SmartMarketLogger;
using MelonLoader;

namespace SmartMarket.Core
{
    [Serializable]
    public class ProductInfo
    {
        public string id;
        public string name;
        public string drugType;
        public string sourceType;
    }

    [Serializable]
    public class ZoneInfo
    {
        public string id; // numeric value as string
        public string name; // friendly name
        public int value;
    }

    [Serializable]
    public class ExportWrapper
    {
        public ProductInfo[] products;
        public ZoneInfo[] zones;
        public string generatedAt;
    }

    public static class EventDataExtractor
    {
        private const string FileName = "SmartMarket_products_zones.json";

        public static string GetOutputPath()
        {
            return Path.Combine(Application.persistentDataPath, FileName);
        }

        public static string GetDesktopPath()
        {
            try { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), FileName); }
            catch { return null; }
        }

        public static void ExtractAndSave()
        {
            try
            {
                MelonLogger.Msg("[SmartMarket] Starting product/zone extraction...");

                var products = ExtractProducts();
                var zones = ExtractZones();

                var wrapper = new ExportWrapper
                {
                    products = products.ToArray(),
                    zones = zones.ToArray(),
                    generatedAt = DateTime.UtcNow.ToString("o")
                };

                string json = SimpleJsonSerialize(wrapper);
                var outPath = GetOutputPath();
                File.WriteAllText(outPath, json);
                MelonLogger.Msg($"[SmartMarket] Exported {wrapper.products.Length} products and {wrapper.zones.Length} zones to: {outPath}");

                var desk = GetDesktopPath();
                if (!string.IsNullOrEmpty(desk))
                {
                    try
                    {
                        File.WriteAllText(desk, json);
                        MelonLogger.Msg($"[SmartMarket] Also wrote output to Desktop: {desk}");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[SmartMarket] Failed writing to Desktop: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SmartMarket] Extraction failed: {ex}");
            }
        }

        private static string SimpleJsonSerialize(ExportWrapper wrapper)
        {
            // Simple JSON serialization for our data structures
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"products\": [");
            for (int i = 0; i < wrapper.products.Length; i++)
            {
                var p = wrapper.products[i];
                sb.Append($"    {{ \"id\": \"{EscapeJson(p.id)}\", \"name\": \"{EscapeJson(p.name)}\", \"drugType\": \"{EscapeJson(p.drugType)}\", \"sourceType\": \"{EscapeJson(p.sourceType)}\" }}");
                if (i < wrapper.products.Length - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("  ],");
            sb.AppendLine($"  \"zones\": [");
            for (int i = 0; i < wrapper.zones.Length; i++)
            {
                var z = wrapper.zones[i];
                sb.Append($"    {{ \"id\": \"{EscapeJson(z.id)}\", \"name\": \"{EscapeJson(z.name)}\", \"value\": {z.value} }}");
                if (i < wrapper.zones.Length - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("  ],");
            sb.AppendLine($"  \"generatedAt\": \"{EscapeJson(wrapper.generatedAt)}\"");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string EscapeJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private static List<ProductInfo> ExtractProducts()
        {
            var result = new List<ProductInfo>();

            try
            {
                // Try generic approach: Find all types with "ProductDefinition" in the name
                var productTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => SafeGetTypes(a))
                    .Where(t => t.Name.Contains("ProductDefinition") || t.Name.Contains("Product"))
                    .ToList();

                MelonLogger.Msg($"[SmartMarket] Found {productTypes.Count} potential product types.");

                foreach (var productType in productTypes)
                {
                    try
                    {
                        // Use generic reflection without Resources.FindObjectsOfTypeAll
                        var instances = TryFindInstancesOfType(productType);
                        foreach (var instance in instances)
                        {
                            try
                            {
                                string name = GetStringPropertyOrField(instance, productType, "Name") ?? 
                                             GetStringPropertyOrField(instance, productType, "name") ?? 
                                             instance.ToString();
                                string id = GetStringPropertyOrField(instance, productType, "ID") ?? 
                                           GetStringPropertyOrField(instance, productType, "Id") ?? 
                                           GetStringPropertyOrField(instance, productType, "id") ?? 
                                           string.Empty;
                                string drugType = GetStringPropertyOrField(instance, productType, "DrugType") ?? string.Empty;

                                if (!string.IsNullOrEmpty(name) && !result.Any(p => p.id == id && p.name == name))
                                {
                                    result.Add(new ProductInfo
                                    {
                                        id = id,
                                        name = name,
                                        drugType = drugType,
                                        sourceType = productType.Name
                                    });
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                if (result.Count == 0)
                {
                    MelonLogger.Warning("[SmartMarket] No product instances found. Using hardcoded defaults.");
                    result.Add(new ProductInfo { id = "cocaina", name = "Cocaína", drugType = "Stimulant", sourceType = "Hardcoded" });
                    result.Add(new ProductInfo { id = "heroina", name = "Heroína", drugType = "Depressant", sourceType = "Hardcoded" });
                    result.Add(new ProductInfo { id = "marihuana", name = "Marihuana", drugType = "Cannabis", sourceType = "Hardcoded" });
                }

                MelonLogger.Msg($"[SmartMarket] Extracted {result.Count} product definitions.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SmartMarket] Product extraction error: {ex.Message}");
            }

            return result;
        }

        private static List<object> TryFindInstancesOfType(Type targetType)
        {
            var result = new List<object>();
            try
            {
                // Try to find instances via FindObjectsOfTypeAll using the type itself
                var method = typeof(Resources).GetMethod("FindObjectsOfTypeAll", 
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                
                if (method != null)
                {
                    var genericMethod = method.MakeGenericMethod(targetType);
                    var instances = genericMethod.Invoke(null, null);
                    
                    if (instances is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var item in enumerable)
                        {
                            result.Add(item);
                        }
                    }
                }
            }
            catch
            {
                // Fallback: try non-generic approach
                try
                {
                    var method = typeof(Resources).GetMethod("FindObjectsOfTypeAll", 
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
                        null, new[] { typeof(Type) }, null);
                    
                    if (method != null)
                    {
                        var instances = method.Invoke(null, new object[] { targetType });
                        if (instances is System.Collections.IEnumerable enumerable)
                        {
                            foreach (var item in enumerable)
                            {
                                result.Add(item);
                            }
                        }
                    }
                }
                catch { }
            }

            return result;
        }

        private static List<ZoneInfo> ExtractZones()
        {
            var result = new List<ZoneInfo>();

            try
            {
                // Try to find an enum named EMapRegion or MapRegion
                var regionType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => SafeGetTypes(a))
                    .FirstOrDefault(t => t.IsEnum && (t.Name == "EMapRegion" || t.Name.Contains("MapRegion") || t.Name.Contains("Region")));

                if (regionType != null && regionType.IsEnum)
                {
                    var names = Enum.GetNames(regionType);
                    foreach (var n in names)
                    {
                        try
                        {
                            var val = (int)Enum.Parse(regionType, n);
                            result.Add(new ZoneInfo { id = val.ToString(), name = n, value = val });
                        }
                        catch { }
                    }
                    MelonLogger.Msg($"[SmartMarket] Extracted {result.Count} zones from enum {regionType.Name}.");
                    return result;
                }

                // Fallback: try to find zone/neighborhood/region objects
                var zoneTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => SafeGetTypes(a))
                    .Where(t => !t.IsEnum && (t.Name.Contains("Neighborhood") || t.Name.Contains("Region") || t.Name.Contains("Zone")))
                    .ToList();

                MelonLogger.Msg($"[SmartMarket] Found {zoneTypes.Count} potential zone types.");

                foreach (var zoneType in zoneTypes.Take(5)) // Limit to avoid too many reflections
                {
                    try
                    {
                        var instances = TryFindInstancesOfType(zoneType);
                        foreach (var instance in instances)
                        {
                            try
                            {
                                var name = GetStringPropertyOrField(instance, zoneType, "Name") ?? 
                                          GetStringPropertyOrField(instance, zoneType, "name") ?? 
                                          instance.ToString();
                                var id = GetStringPropertyOrField(instance, zoneType, "ID") ?? 
                                        GetStringPropertyOrField(instance, zoneType, "Id") ?? 
                                        string.Empty;
                                
                                if (!string.IsNullOrEmpty(name) && !result.Any(z => z.id == id && z.name == name))
                                {
                                    result.Add(new ZoneInfo { id = id, name = name, value = 0 });
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                if (result.Count == 0)
                {
                    MelonLogger.Warning("[SmartMarket] No zone instances found. Using hardcoded defaults.");
                    result.Add(new ZoneInfo { id = "0", name = "Suburbia", value = 0 });
                    result.Add(new ZoneInfo { id = "1", name = "Docks", value = 1 });
                    result.Add(new ZoneInfo { id = "2", name = "Downtown", value = 2 });
                    result.Add(new ZoneInfo { id = "3", name = "Warehouse District", value = 3 });
                }

                MelonLogger.Msg($"[SmartMarket] Extracted {result.Count} zones.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SmartMarket] Zone extraction error: {ex.Message}");
            }

            return result;
        }

        private static string GetStringPropertyOrField(object obj, Type type, string name)
        {
            try
            {
                var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (prop != null && prop.PropertyType == typeof(string))
                    return prop.GetValue(obj) as string;

                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (field != null && field.FieldType == typeof(string))
                    return field.GetValue(obj) as string;

                // try methods named get_Name
                var getMethod = type.GetMethod("get_" + name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (getMethod != null && getMethod.ReturnType == typeof(string))
                    return getMethod.Invoke(obj, null) as string;
            }
            catch { }
            return null;
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch { return new Type[0]; }
        }
    }
}
