using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using MelonLogger = SmartMarket.SmartMarketLogger;
using MelonLoader;

namespace SmartMarket.Core
{
    public static class SeasonalChangeManager
    {
        public static bool IsSeasonalActive { get; private set; } = false;
        private static string lastDominantWeather = null;
        // expiry based on in-game day when possible
        private static int expiryGameDay = -1;
        private static bool subscribedToDayEvent = false;
        private static bool fallbackRealtimeExpiry = false;
        private static float activeUntilGameTime = 0f; // fallback realtime timer
        private static int lastCheckFrame = 0;

        public static void Init()
        {
            IsSeasonalActive = false;
            lastDominantWeather = null;
            expiryGameDay = -1;
            subscribedToDayEvent = false;
            fallbackRealtimeExpiry = false;
            activeUntilGameTime = 0f;
            MelonLogger.Msg("[SmartMarket] SeasonalChangeManager initialized.");

            // Note: subscription to day events is attempted lazily inside GetCurrentGameDay()
            // to avoid depending on a missing TrySubscribeToDayPass helper in different builds.
        }

        // Called every frame from SmartMarketMod.OnUpdate
        public static void Update()
        {
            // cheap throttle to once per 30 frames (~0.5s)
            if (Time.frameCount == lastCheckFrame) return;
            if (Time.frameCount - lastCheckFrame < 30) return;
            lastCheckFrame = Time.frameCount;

            try
            {
                // Discover the EnvironmentManager type in loaded assemblies
                var envType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("EnvironmentManager"))
                    .FirstOrDefault(t => t != null);

                if (envType == null) return;

                // Try to find an instance: prefer static Instance/Singleton, otherwise FindObjectOfType
                object envInstance = null;
                var propInst = envType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (propInst != null)
                    envInstance = propInst.GetValue(null);

                if (envInstance == null)
                {
                    var fieldInst = envType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (fieldInst != null)
                        envInstance = fieldInst.GetValue(null);
                }

                if (envInstance == null)
                {
                    // fallback: try to call Object.FindObjectOfType(Type) via reflection (some builds may expose different signatures)
                    try
                    {
                        var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new Type[] { typeof(Type) });
                        if (findMethod != null)
                        {
                            var unityObj = findMethod.Invoke(null, new object[] { envType });
                            envInstance = unityObj;
                        }
                    }
                    catch { /* ignore if not available */ }
                }

                if (envInstance == null) return;

                // Get CurrentWeatherConditions property
                var weatherProp = envType.GetProperty("CurrentWeatherConditions", BindingFlags.Public | BindingFlags.Instance);
                if (weatherProp == null) return;

                var weatherObj = weatherProp.GetValue(envInstance);
                if (weatherObj == null) return;

                // Examine float properties like Sunny, Rainy, Cloudy, Stormy, Foggy, Windy, Hail, Sleet
                var weatherType = weatherObj.GetType();
                var floatProps = weatherType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.PropertyType == typeof(float)).ToList();

                if (floatProps.Count == 0) return;

                string dominant = null;
                float maxVal = -1f;
                foreach (var p in floatProps)
                {
                    try
                    {
                        var v = (float)p.GetValue(weatherObj);
                        if (v > maxVal)
                        {
                            maxVal = v;
                            dominant = p.Name;
                        }
                    }
                    catch { }
                }

                if (dominant == null) return;

                // If dominant changed and new weather is calmer-ish, trigger seasonal change
                bool isCalm = IsCalmWeatherName(dominant);
                if (dominant != lastDominantWeather)
                {
                    MelonLogger.Msg($"[SmartMarket] Weather changed: {lastDominantWeather ?? "(null)"} -> {dominant} (calm={isCalm})");
                    lastDominantWeather = dominant;

                    if (isCalm && Core.SmartMarketConfig.SeasonalChangeEnabled && Core.SmartMarketConfig.Events.cambioEstacional.enabled)
                    {
                        TriggerSeasonalChange();
                    }
                }

                // handle expiry by game day if set
                if (IsSeasonalActive)
                {
                    int? currentDay = GetCurrentGameDay();
                    if (currentDay.HasValue && expiryGameDay >= 0)
                    {
                        if (currentDay.Value >= expiryGameDay)
                        {
                            EndSeasonalChange();
                        }
                    }
                    else if (fallbackRealtimeExpiry && activeUntilGameTime > 0f && Time.realtimeSinceStartup >= activeUntilGameTime)
                    {
                        EndSeasonalChange();
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] SeasonalChangeManager.Update exception: {ex.Message}");
            }
        }

        public static void DrawOverlay()
        {
            if (!IsSeasonalActive) return;

            // Small top-left debug label
            GUI.color = Color.cyan;
            GUI.Label(new Rect(10, 10, 400, 24), "[SmartMarket] Cambio estacional activo: efectos aplicados");
            GUI.color = Color.white;
        }

        private static bool IsCalmWeatherName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            name = name.ToLowerInvariant();
            // Consider these as calmer states
            string[] calm = new[] { "rainy", "rain", "foggy", "fog", "cloudy", "cloud", "snowy", "snow" };
            return calm.Any(c => name.Contains(c));
        }

        private static void TriggerSeasonalChange()
        {
            if (IsSeasonalActive) return; // already active

            IsSeasonalActive = true;
            expiryGameDay = -1;
            fallbackRealtimeExpiry = false;
            activeUntilGameTime = 0f;

            // Determine current game day and set expiry to next day (default 1 in-game day duration)
            int? currentDay = GetCurrentGameDay();
            if (currentDay.HasValue)
            {
                expiryGameDay = currentDay.Value + 1; // active until next in-game day
                MelonLogger.Msg($"[SmartMarket] Cambio estacional trigger: expirará en game day {expiryGameDay} (current {currentDay.Value}).");
            }
            else
            {
                // fallback to realtime short timer if day info/event not available
                float intensity = Core.SmartMarketConfig.Events.cambioEstacional.intensity;
                float seconds = Mathf.Lerp(10f, 60f, Mathf.Clamp01(intensity / 10f));
                activeUntilGameTime = Time.realtimeSinceStartup + seconds;
                fallbackRealtimeExpiry = true;
                MelonLogger.Msg($"[SmartMarket] Cambio estacional trigger: no se encontró día del juego, usando fallback tiempo real {seconds:0.0}s.");
            }

            // Apply effects to player and some random NPCs for immersion
            ApplyEffectsByNameToEntities(new[] { "Sedating", "LongFaced", "Calming" });
        }

        private static void EndSeasonalChange()
        {
            IsSeasonalActive = false;
            expiryGameDay = -1;
            activeUntilGameTime = 0f;
            fallbackRealtimeExpiry = false;
            MelonLogger.Msg("[SmartMarket] Cambio estacional finalizado.");
            // No explicit removal of effects (game systems may handle effect duration), this is best-effort
        }

        public static void ForceSeasonalChangeDebug()
        {
            MelonLogger.Msg("[SmartMarket] ForceSeasonalChangeDebug invoked.");
            TriggerSeasonalChange();
        }

        private static void ApplyEffectsByName(string[] effectTypeNames)
        {
            // kept for backward compatibility, not used by new method
            ApplyEffectsByNameToEntities(effectTypeNames);
        }

        private static void ApplyEffectsByNameToEntities(string[] effectTypeNames)
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var name in effectTypeNames)
                {
                    Type effType = null;
                    foreach (var a in assemblies)
                    {
                        var t = a.GetTypes().FirstOrDefault(tt => tt.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                        if (t != null) { effType = t; break; }
                    }
                    if (effType == null)
                    {
                        MelonLogger.Msg($"[SmartMarket] Effect type '{name}' not found in assemblies.");
                        continue;
                    }

                    object effInstance = null;
                    try
                    {
                        effInstance = Activator.CreateInstance(effType);
                    }
                    catch
                    {
                        MelonLogger.Msg($"[SmartMarket] Could not Activator.CreateInstance for '{name}', trying ScriptableObject.CreateInstance if UnityEngine.ScriptableObject.");
                        if (typeof(UnityEngine.ScriptableObject).IsAssignableFrom(effType))
                        {
                            var method = typeof(UnityEngine.ScriptableObject).GetMethod("CreateInstance", new Type[] { typeof(Type) });
                            effInstance = method?.Invoke(null, new object[] { effType });
                        }
                    }

                    if (effInstance == null)
                    {
                        MelonLogger.Msg($"[SmartMarket] Could not create instance of effect '{name}'.");
                        continue;
                    }

                    // Try to apply to player if method exists
                    var applyToPlayer = effType.GetMethod("ApplyToPlayer", BindingFlags.Public | BindingFlags.Instance);
                    if (applyToPlayer != null)
                    {
                        object playerObj = FindPlayerInstance();
                        if (playerObj != null)
                        {
                            try
                            {
                                applyToPlayer.Invoke(effInstance, new object[] { playerObj });
                                MelonLogger.Msg($"[SmartMarket] Applied effect '{name}' to player instance.");
                            }
                            catch (Exception ex)
                            {
                                MelonLogger.Warning($"[SmartMarket] Failed ApplyToPlayer invoke for {name}: {ex.Message}");
                            }
                        }
                    }

                    // Try to apply to a small random subset of NPCs
                    var applyToNPC = effType.GetMethod("ApplyToNPC", BindingFlags.Public | BindingFlags.Instance);
                    if (applyToNPC != null)
                    {
                        // find candidate NPC types
                        var npcTypes = AppDomain.CurrentDomain.GetAssemblies()
                            .SelectMany(a => {
                                try { return a.GetTypes(); } catch { return new Type[0]; }
                            })
                            .Where(t => t != null && (t.Name.Equals("NPC", StringComparison.OrdinalIgnoreCase)
                                                       || t.Name.IndexOf("Citizen", StringComparison.OrdinalIgnoreCase) >= 0
                                                       || t.Name.IndexOf("Ped", StringComparison.OrdinalIgnoreCase) >= 0
                                                       || t.Name.IndexOf("Character", StringComparison.OrdinalIgnoreCase) >= 0))
                            .ToList();

                        foreach (var npcType in npcTypes)
                        {
                            try
                            {
                                // call FindObjectsOfType(Type) via reflection if available
                                object instancesObj = null;
                                try
                                {
                                    var findMany = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType", new Type[] { typeof(Type) });
                                    if (findMany != null)
                                        instancesObj = findMany.Invoke(null, new object[] { npcType });
                                }
                                catch { }
                                if (instancesObj == null) continue;
                                var instances = instancesObj as Array;
                                if (instances == null || instances.Length == 0) continue;

                                // decide how many NPCs to affect: small number based on intensity
                                float intensity = Core.SmartMarketConfig.Events.cambioEstacional.intensity;
                                int count = Mathf.Clamp(Mathf.RoundToInt(intensity / 2f), 1, 8); // 0-10 -> 1-8 NPCs

                                // pick random unique indices
                                System.Random rnd = new System.Random();
                                var chosen = new System.Collections.Generic.HashSet<int>();
                                int total = instances.Length;
                                for (int i = 0; i < count && chosen.Count < total; i++)
                                {
                                    int idx = rnd.Next(0, total);
                                    if (chosen.Contains(idx)) { i--; continue; }
                                    chosen.Add(idx);
                                    try
                                    {
                                        var inst = instances.GetValue(idx);
                                        applyToNPC.Invoke(effInstance, new object[] { inst });
                                        MelonLogger.Msg($"[SmartMarket] Applied effect '{name}' to NPC of type {npcType.Name}.");
                                    }
                                    catch (Exception ex)
                                    {
                                        MelonLogger.Warning($"[SmartMarket] Failed ApplyToNPC for {name} on {npcType.Name}: {ex.Message}");
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] ApplyEffectsByNameToEntities exception: {ex.Message}");
            }
        }

        private static object FindPlayerInstance()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var a in assemblies)
            {
                Type[] types;
                try { types = a.GetTypes(); } catch { continue; }
                var playerType = types.FirstOrDefault(t => string.Equals(t.Name, "Player", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t.Name, "LocalPlayer", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t.Name, "GamePlayer", StringComparison.OrdinalIgnoreCase));
                if (playerType == null) continue;

                // try static Instance
                var prop = playerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (prop != null)
                {
                    var inst = prop.GetValue(null);
                    if (inst != null) return inst;
                }
                var field = playerType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                if (field != null)
                {
                    var inst = field.GetValue(null);
                    if (inst != null) return inst;
                }

                // fallback to FindObjectOfType
                // try to call Object.FindObjectOfType(Type) via reflection
                try
                {
                    var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new Type[] { typeof(Type) });
                    if (findMethod != null)
                    {
                        var unityObj = findMethod.Invoke(null, new object[] { playerType });
                        if (unityObj != null) return unityObj;
                    }
                }
                catch { }

            }
            return null;
        }

        private static int? GetCurrentGameDay()
        {
            try
            {
                // attempt to find TimeManager type
                var timeType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
                    .FirstOrDefault(t => string.Equals(t.Name, "TimeManager", StringComparison.OrdinalIgnoreCase)
                                         || string.Equals(t.Name, "GameTimeManager", StringComparison.OrdinalIgnoreCase));
                if (timeType == null) return null;

                // prefer static property CurrentDay/Day/DayCount/DaysPassed
                var props = new[] { "CurrentDay", "Day", "DayCount", "DaysPassed", "GameDay" };
                foreach (var pn in props)
                {
                    var p = timeType.GetProperty(pn, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
                    if (p != null)
                    {
                        object val = null;
                        if (p.GetMethod.IsStatic)
                            val = p.GetValue(null);
                        else
                        {
                            var inst = timeType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                                       ?? timeType.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                                       ?? InvokeFindObjectOfType(timeType);
                            if (inst != null) val = p.GetValue(inst);
                        }

                        if (val is int ii) return ii;
                        if (val is long ll) return (int)ll;
                        if (val is float ff) return (int)ff;
                    }
                }

                // try to subscribe to an onDayPass event if present (subscribe once)
                if (!subscribedToDayEvent)
                {
                    var ev = timeType.GetEvent("onDayPass", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
                    if (ev != null)
                    {
                        try
                        {
                            // create a generic handler matching Action or Action<int>
                            MethodInfo handler = typeof(SeasonalChangeManager).GetMethod("OnDayPassedHandler", BindingFlags.NonPublic | BindingFlags.Static);
                            if (handler != null)
                            {
                                Delegate d = null;
                                var handlerType = ev.EventHandlerType;
                                if (handlerType != null)
                                {
                                    d = Delegate.CreateDelegate(handlerType, handler);
                                    ev.AddEventHandler(null, d);
                                    subscribedToDayEvent = true;
                                    MelonLogger.Msg("[SmartMarket] Subscribed to TimeManager.onDayPass event.");
                                }
                            }
                        }
                        catch (Exception ex) { MelonLogger.Warning($"[SmartMarket] Subscribe to onDayPass failed: {ex.Message}"); }
                    }
                }

                // helper to attempt FindObjectOfType(Type) via reflection
                object InvokeFindObjectOfType(Type t)
                {
                    try
                    {
                        var m = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new Type[] { typeof(Type) });
                        if (m != null) return m.Invoke(null, new object[] { t });
                    }
                    catch { }
                    return null;
                }

                // no direct day property found
                return null;
            }
            catch { return null; }
        }

        // event handler used if we successfully subscribed
        private static void OnDayPassedHandler()
        {
            // when a day passes, if seasonal active and expiryGameDay set to next day, end it
            try
            {
                if (!IsSeasonalActive) return;
                int? currentDay = GetCurrentGameDay();
                if (!currentDay.HasValue)
                {
                    // if we don't know day, just end on first onDayPass
                    EndSeasonalChange();
                    return;
                }

                if (expiryGameDay >= 0 && currentDay.Value >= expiryGameDay)
                {
                    EndSeasonalChange();
                }
            }
            catch { }
        }
    }
}
