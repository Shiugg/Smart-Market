using System;
using System.Collections.Generic;
using UnityEngine;
using MelonLoader;
using MelonLogger = SmartMarket.SmartMarketLogger;
using SmartMarket.Customers;

namespace SmartMarket.Core
{
    // Best-effort watcher that detects when Customer gameobjects disappear from the scene shortly
    // after a recent transaction and applies a significant loyalty penalty (simulating the player
    // killing/robbing the NPC). This is defensive and conservative to avoid false positives.
    public static class CustomerWatcher
    {
        // How long after LastPurchaseDate we consider a disappearance likely to be post-sale robbery (minutes)
        private const int RecentPurchaseWindowMinutes = 10;

        // Penalties applied when a customer is detected as killed/robbed
        private const float KillTrustPenalty = 0.25f; // -25% trust
        private const float KillSatisfactionPenalty = 0.20f; // -20% satisfaction
        private const int KillDaysWithoutResponseIncrease = 3; // NPC stops messaging for days

        // Cache of currently observed customer IDs
        private static HashSet<string> _knownCustomers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _initialized = false;
        
        // Throttle: ejecutar cada 5 frames (~0.083s a 60 FPS)
        private static int _lastUpdateFrame = 0;
        private const int THROTTLE_FRAMES = 5;

        public static void Init()
        {
            try
            {
                UpdateKnownCustomers();
                _initialized = true;
                MelonLogger.Msg("[SmartMarket] CustomerWatcher initialized.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] CustomerWatcher.Init failed: {ex.Message}");
            }
        }

        // Called every frame from SmartMarketMod.OnUpdate
        public static void Update()
        {
            try
            {
                if (!_initialized) Init();

                // THROTTLE: ejecutar solo cada 5 frames para evitar FindObjectsOfType() cada frame
                if (Time.frameCount - _lastUpdateFrame < THROTTLE_FRAMES) return;
                _lastUpdateFrame = Time.frameCount;

                // Gather current in-scene customers
                var current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var all = UnityEngine.Object.FindObjectsOfType<Il2CppScheduleOne.Economy.Customer>();
                    if (all != null)
                    {
                        foreach (var c in all)
                        {
                            try
                            {
                                if (c != null && c.gameObject != null && !string.IsNullOrEmpty(c.gameObject.name))
                                    current.Add(c.gameObject.name);
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                // Detect customers that were known before but are now missing
                var disappeared = new List<string>();
                foreach (var id in _knownCustomers)
                {
                    if (!current.Contains(id)) disappeared.Add(id);
                }

                // Process disappeared customers
                foreach (var id in disappeared)
                {
                    try
                    {
                        MelonLogger.Msg($"[CustomerWatcher] Customer missing from scene: {id}");

                        // Load profile if exists
                        var profile = CustomerSatisfactionProfile.Load(id);
                        if (profile == null)
                        {
                            MelonLogger.Msg($"[CustomerWatcher] No persisted profile found for {id}. No robbery penalty applied.");
                            // No persisted profile: skip aggressive penalties
                            _knownCustomers.Remove(id);
                            continue;
                        }
                        MelonLogger.Msg($"[CustomerWatcher] Perfil cargado para {id}: LastPurchaseDate={profile.LastPurchaseDate:O}, DaysWithoutResponse={profile.DaysWithoutResponse}, Trust={profile.Trust:0.00}, Satisfaction={profile.Satisfaction:0.00}");
 
                        // If customer's last purchase was recent (within window), assume potential robbery after sale
                        if (profile.LastPurchaseDate != DateTime.MinValue)
                        {
                            var age = DateTime.Now - profile.LastPurchaseDate;
                            if (age.TotalMinutes <= RecentPurchaseWindowMinutes)
                            {
                                MelonLogger.Msg($"[CustomerWatcher] {id} desapareció dentro de la ventana reciente ({age.TotalMinutes:0.0} min). Aplicando penalización por posible robo/asesinato.");
                                // Apply penalties
                                profile.Trust = Mathf.Clamp01(profile.Trust - KillTrustPenalty);
                                profile.Satisfaction = Mathf.Clamp01(profile.Satisfaction - KillSatisfactionPenalty);
                                profile.DaysWithoutResponse += KillDaysWithoutResponseIncrease;
                                profile.Save();

                                // Log event
                                try
                                {
                                    var eventsPath = System.IO.Path.Combine(Application.persistentDataPath, "SmartMarket_events.log");
                                    var line = $"{DateTime.Now:O}\tCUSTOMER_KILLED_ROBBED\t{id}\tTrust=-{KillTrustPenalty:0.00}\tSatisfaction=-{KillSatisfactionPenalty:0.00}\tLastPurchaseAgeMin={age.TotalMinutes:0.0}\n";
                                    System.IO.File.AppendAllText(eventsPath, line);
                                }
                                catch { }

                                MelonLogger.Msg($"[SmartMarket][EVENT] {id} parece haber sido asesinado/robado tras la compra. LEALTAD -{KillTrustPenalty*100:0}% SAT -{KillSatisfactionPenalty*100:0}%");

                                // Attempt to send a phone message (or queue it) informing the player
                                try
                                {
                                    Il2CppScheduleOne.Economy.Customer liveCustomer = null;
                                    try
                                    {
                                        var allCustomers = UnityEngine.Object.FindObjectsOfType<Il2CppScheduleOne.Economy.Customer>();
                                        if (allCustomers != null)
                                        {
                                            foreach (var c in allCustomers)
                                            {
                                                if (c != null && c.gameObject != null && string.Equals(c.gameObject.name, id, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    liveCustomer = c;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    catch { }

                                    var consumerProfile = liveCustomer != null ? ProfileManager.GetOrCreateProfile(liveCustomer) : null;
                                    var text = MessageGenerator.GeneratePenaltyMessage(consumerProfile);
                                    SmartMarket.Core.PhoneMessenger.SendMessageFromCustomer(id, text);
                                }
                                catch { }
                            }
                        }

                        // Remove from known cache to avoid repeated processing
                        _knownCustomers.Remove(id);
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[SmartMarket] CustomerWatcher processing failed for {id}: {ex.Message}");
                        _knownCustomers.Remove(id);
                    }
                }

                // Refresh known customers with current ones
                foreach (var id in current) if (!_knownCustomers.Contains(id)) _knownCustomers.Add(id);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] CustomerWatcher.Update failed: {ex.Message}");
            }
        }

        private static void UpdateKnownCustomers()
        {
            _knownCustomers.Clear();
            try
            {
                var all = UnityEngine.Object.FindObjectsOfType<Il2CppScheduleOne.Economy.Customer>();
                if (all != null)
                {
                    foreach (var c in all)
                    {
                        try
                        {
                            if (c != null && c.gameObject != null && !string.IsNullOrEmpty(c.gameObject.name))
                                _knownCustomers.Add(c.gameObject.name);
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }
    }
}
