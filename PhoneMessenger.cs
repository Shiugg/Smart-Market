using System;
using UnityEngine;
using MelonLogger = SmartMarket.SmartMarketLogger;
using MelonLoader;

namespace SmartMarket.Core
{
    public static class PhoneMessenger
    {
        // Try to send a phone message from the given customer name to the player immediately.
        // Best-effort: will try to locate an in-scene Customer and call NotifyPlayerOfContract
        // with a MessageChain. If that fails, it falls back to queuing the message in MemorySystem
        // (so it will be injected later when the customer next notifies the player).
        public static void SendMessageFromCustomer(string customerId, string text)
        {
            try
            {
                if (string.IsNullOrEmpty(customerId) || string.IsNullOrEmpty(text)) return;

                // Prefer queueing messages to avoid racing with the game's notify flow.
                try
                {
                    var mem = MemorySystem.GetMemory(customerId);
                    if (mem != null)
                    {
                        if (mem.PendingOutgoingMessages == null) mem.PendingOutgoingMessages = new System.Collections.Generic.List<string>();
                        mem.PendingOutgoingMessages.Add(text);
                        MemorySystem.Save();
                        MelonLogger.Msg($"[SmartMarket] Phone message queued for {customerId}: {text}");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SmartMarket] Failed to queue phone message for {customerId}: {ex.Message}");
                }

                // If queuing failed, still attempt best-effort live injection as fallback
                try
                {
                    var all = UnityEngine.Object.FindObjectsOfType<Il2CppScheduleOne.Economy.Customer>();
                    if (all != null)
                    {
                        foreach (var c in all)
                        {
                            try
                            {
                                if (c != null && c.gameObject != null && string.Equals(c.gameObject.name, customerId, StringComparison.OrdinalIgnoreCase))
                                {
                                    var chain = new Il2CppScheduleOne.UI.Phone.Messages.MessageChain();
                                    chain.Messages = new Il2CppSystem.Collections.Generic.List<string>();
                                    chain.Messages.Add(text);
                                    try
                                    {
                                        c.NotifyPlayerOfContract(null, chain, false, false, false);
                                        MelonLogger.Msg($"[SmartMarket] Phone message injected (fallback) from {customerId}: {text}");
                                        return;
                                    }
                                    catch (Exception ex)
                                    {
                                        MelonLogger.Warning($"[SmartMarket] Fallback NotifyPlayerOfContract failed for {customerId}: {ex.Message}");
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SmartMarket] Error during fallback live injection for {customerId}: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] PhoneMessenger.SendMessageFromCustomer error: {ex.Message}");
            }
        }
    }
}