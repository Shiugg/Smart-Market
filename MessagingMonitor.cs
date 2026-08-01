using System;
using System.Collections.Generic;
using UnityEngine;
using MelonLoader;
using MelonLogger = SmartMarket.SmartMarketLogger;
using SmartMarket.Customers;

namespace SmartMarket.Core
{
    // Monitors pending messages that require player response. If the player doesn't respond within
    // a timeout (real seconds), mark RecordNoResponse() on the customer's satisfaction profile.
    // This is non-invasive: it does not modify vanilla messaging flows, only tracks timings.
    public static class MessagingMonitor
    {
        private static Dictionary<string, DateTime> _pending = new Dictionary<string, DateTime>();
        private static int _timeoutSeconds = 120; // 2 minutes real-time

        public static void RegisterPending(string customerId)
        {
            try
            {
                if (string.IsNullOrEmpty(customerId)) return;
                _pending[customerId] = DateTime.UtcNow;
                MelonLogger.Msg($"[MessagingMonitor] Registered pending message for {customerId} at {_pending[customerId]}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MessagingMonitor] RegisterPending failed: {ex.Message}");
            }
        }

        public static void ClearPending(string customerId)
        {
            try
            {
                if (string.IsNullOrEmpty(customerId)) return;
                if (_pending.ContainsKey(customerId))
                {
                    _pending.Remove(customerId);
                    MelonLogger.Msg($"[MessagingMonitor] Cleared pending message for {customerId}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MessagingMonitor] ClearPending failed: {ex.Message}");
            }
        }

        // Call this regularly (e.g., from SmartMarketMod.OnUpdate)
        public static void Update()
        {
            try
            {
                if (_pending.Count == 0) return;
                var now = DateTime.UtcNow;
                var expired = new List<string>();
                foreach (var kv in _pending)
                {
                    if ((now - kv.Value).TotalSeconds >= _timeoutSeconds)
                    {
                        expired.Add(kv.Key);
                    }
                }

                foreach (var cid in expired)
                {
                    try
                    {
                        MelonLogger.Msg($"[MessagingMonitor] Pending message expired for {cid}, recording no-response.");
                        var profile = CustomerSatisfactionProfile.GetOrCreate(cid);
                        if (profile != null)
                        {
                            profile.RecordNoResponse();

                            // After applying RecordNoResponse, attempt to update vanilla UI bars for this customer so player can notice.
                            try
                            {
                                var all = UnityEngine.Object.FindObjectsOfType<Il2CppScheduleOne.Economy.Customer>();
                                foreach (var c in all)
                                {
                                    try
                                    {
                                        if (c != null && c.gameObject != null && c.gameObject.name == cid)
                                        {
                                            // Use the helper in CustomerBehaviorPatches to update vanilla fields (best-effort)
                                            SmartMarket.Patches.CustomerBehaviorPatches.TryUpdateVanillaRelationship(c, profile);
                                            break;
                                        }
                                    }
                                    catch { }
                                }
                            }
                            catch (Exception ex)
                            {
                                MelonLogger.Warning($"[MessagingMonitor] Failed trying to update vanilla UI after no-response for {cid}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[MessagingMonitor] Handling expired pending {cid} failed: {ex.Message}");
                    }
                    _pending.Remove(cid);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MessagingMonitor] Update failed: {ex.Message}");
            }
        }

        public static void SetTimeoutSeconds(int seconds)
        {
            _timeoutSeconds = Mathf.Clamp(seconds, 10, 3600);
        }

        public static int GetTimeoutSeconds() => _timeoutSeconds;
    }
}
