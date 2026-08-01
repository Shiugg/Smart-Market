using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using MelonLoader;
using MelonLogger = SmartMarket.SmartMarketLogger;
using UnityEngine;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Economy;
using SmartMarket.Customers;
using SmartMarket.Core;

namespace SmartMarket.Patches
{
    [HarmonyPatch]
    public static class RobberyPatches
    {
        private static readonly Dictionary<int, bool> PlayerAttackCandidates = new Dictionary<int, bool>();

        private const float KillTrustPenalty = 0.25f;
        private const float KillSatisfactionPenalty = 0.20f;
        private const int KillDaysWithoutResponse = 3;

        private const float PickpocketTrustPenalty = 0.15f;
        private const float PickpocketSatisfactionPenalty = 0.10f;
        private const int PickpocketDaysWithoutResponse = 2;

        private const float CorpseLootTrustPenalty = 0.20f;
        private const float CorpseLootSatisfactionPenalty = 0.15f;
        private const int CorpseLootDaysWithoutResponse = 3;

        [HarmonyPatch(typeof(NPCHealth), nameof(NPCHealth.NotifyAttackedByPlayer))]
        public static class NPCHealth_NotifyAttackedByPlayer_Patch
        {
            public static void Postfix(NPCHealth __instance, Player player)
            {
                if (__instance == null || __instance.gameObject == null) return;

                try
                {
                    int key = __instance.gameObject.GetInstanceID();
                    PlayerAttackCandidates[key] = true;
                    SmartMarketConfig.LogDebug($"[Robbery] Player attacked NPC {GetObjectName(__instance.gameObject)} (flagged for kill detection).");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SmartMarket] NPCHealth.NotifyAttackedByPlayer patch failed: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(NPCHealth), nameof(NPCHealth.Die))]
        public static class NPCHealth_Die_Patch
        {
            public static void Postfix(NPCHealth __instance)
            {
                if (__instance == null || __instance.gameObject == null) return;

                try
                {
                    int key = __instance.gameObject.GetInstanceID();
                    bool wasPlayerAttack = PlayerAttackCandidates.ContainsKey(key) && PlayerAttackCandidates[key];
                    if (PlayerAttackCandidates.ContainsKey(key))
                        PlayerAttackCandidates.Remove(key);

                    if (!wasPlayerAttack)
                        return;

                    HandleNpcKilledByPlayer(__instance.gameObject);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SmartMarket] NPCHealth.Die patch failed: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(NPCInventory), nameof(NPCInventory.StartPickpocket))]
        public static class NPCInventory_StartPickpocket_Patch
        {
            public static void Postfix(NPCInventory __instance)
            {
                if (__instance == null || __instance.gameObject == null) return;

                try
                {
                    var rootObject = ResolveNpcRoot(__instance.gameObject);
                    if (rootObject != null)
                        HandlePickpocketAttempt(rootObject);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SmartMarket] NPCInventory.StartPickpocket patch failed: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(ItemPickup), nameof(ItemPickup.Pickup))]
        public static class ItemPickup_Pickup_Patch
        {
            public static void Postfix(ItemPickup __instance)
            {
                if (__instance == null || __instance.gameObject == null) return;

                try
                {
                    var npc = __instance.GetComponent<NPC>() ?? __instance.gameObject.GetComponentInParent<NPC>();
                    if (npc != null && npc.gameObject != null)
                    {
                        HandleCorpseLoot(npc.gameObject, __instance.gameObject.name);
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SmartMarket] ItemPickup.Pickup patch failed: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(NetworkedItemPickup), nameof(NetworkedItemPickup.Pickup))]
        public static class NetworkedItemPickup_Pickup_Patch
        {
            public static void Postfix(NetworkedItemPickup __instance)
            {
                if (__instance == null || __instance.gameObject == null) return;

                try
                {
                    var npc = __instance.GetComponent<NPC>() ?? __instance.gameObject.GetComponentInParent<NPC>();
                    if (npc != null && npc.gameObject != null)
                    {
                        HandleCorpseLoot(npc.gameObject, __instance.gameObject.name);
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SmartMarket] NetworkedItemPickup.Pickup patch failed: {ex.Message}");
                }
            }
        }

        private static void HandleNpcKilledByPlayer(GameObject npcObject)
        {
            if (npcObject == null) return;
 
            string customerId = GetCustomerIdFromNpc(npcObject);
            if (string.IsNullOrEmpty(customerId)) return;
 
            MelonLogger.Msg("CUSTOMER", $"NPC muerto por el jugador detectado: {customerId}. Aplicando penalización de lealtad.");
            ApplyCustomerPenalty(customerId, "KILL", KillTrustPenalty, KillSatisfactionPenalty, KillDaysWithoutResponse);
        }
 
        private static void HandlePickpocketAttempt(GameObject npcObject)
        {
            if (npcObject == null) return;
 
            string customerId = GetCustomerIdFromNpc(npcObject);
            if (string.IsNullOrEmpty(customerId)) return;
 
            MelonLogger.Msg("CUSTOMER", $"Intento de pickpocket detectado en {customerId}. Aplicando penalización de lealtad.");
            ApplyCustomerPenalty(customerId, "PICKPOCKET", PickpocketTrustPenalty, PickpocketSatisfactionPenalty, PickpocketDaysWithoutResponse);
        }
 
        private static void HandleCorpseLoot(GameObject npcRoot, string itemName)
        {
            if (npcRoot == null) return;
 
            string customerId = GetCustomerIdFromNpc(npcRoot);
            if (string.IsNullOrEmpty(customerId)) return;
 
            MelonLogger.Msg("CUSTOMER", $"Loot en cadáver detectado para {customerId} (objeto: {itemName}). Aplicando penalización de lealtad.");
            ApplyCustomerPenalty(customerId, "LOOT_CORPSE", CorpseLootTrustPenalty, CorpseLootSatisfactionPenalty, CorpseLootDaysWithoutResponse);
        }

        private static readonly Dictionary<string, DateTime> SuppressedContracts = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        private static void ApplyCustomerPenalty(string customerId, string penaltyType, float trustPenalty, float satisfactionPenalty, int daysWithoutResponse)
        {
            if (string.IsNullOrEmpty(customerId)) return;

            try
            {
                // Avoid applying repeated penalties in quick succession for the same customer.
                if (SuppressedContracts.ContainsKey(customerId))
                {
                    var expiry = SuppressedContracts[customerId];
                    if (DateTime.UtcNow <= expiry)
                    {
                        SmartMarketConfig.LogDebug($"[Robbery] Skipping duplicate penalty for {customerId} because suppression active until {expiry:O}");
                        return;
                    }
                    else
                    {
                        // expired - remove and continue
                        SuppressedContracts.Remove(customerId);
                    }
                }

                var profile = CustomerSatisfactionProfile.GetOrCreate(customerId);
                float oldTrust = profile.Trust;
                float oldSatisfaction = profile.Satisfaction;

                profile.Trust = Mathf.Clamp01(profile.Trust - trustPenalty);
                profile.Satisfaction = Mathf.Clamp01(profile.Satisfaction - satisfactionPenalty);
                profile.DaysWithoutResponse += daysWithoutResponse;
                profile.Save();

                var eventsPath = Path.Combine(Application.persistentDataPath, "SmartMarket_events.log");
                var line = $"{DateTime.Now:O}\t{penaltyType}\t{customerId}\tTrust={oldTrust:0.00}->{profile.Trust:0.00}\tSatisfaction={oldSatisfaction:0.00}->{profile.Satisfaction:0.00}\tDaysWithoutResponse={profile.DaysWithoutResponse}\n";
                try { File.AppendAllText(eventsPath, line); } catch { }

                SmartMarketConfig.LogDebug($"[Robbery] {penaltyType} penalty for {customerId}: trust {oldTrust:0.00}->{profile.Trust:0.00}, satisfaction {oldSatisfaction:0.00}->{profile.Satisfaction:0.00}");
                MelonLogger.Msg($"[SmartMarket][EVENT] {customerId} penalizado por {penaltyType}: TRUST -{trustPenalty*100:0}% SAT -{satisfactionPenalty*100:0}%");

                ClearPendingContractState(customerId);

                try
                {
                    var liveCustomer = FindCustomerById(customerId);
                    if (liveCustomer != null)
                    {
                        CustomerBehaviorPatches.TryUpdateVanillaRelationship(liveCustomer, profile);
                        SmartMarketConfig.LogDebug($"[Robbery] Actualizada relación vanilla para {customerId}");
                    }
                }
                catch { }

                try
                {
                    var liveCustomer = FindCustomerById(customerId);
                    var penaltyProfile = liveCustomer != null ? ProfileManager.GetOrCreateProfile(liveCustomer) : new ConsumerProfile(customerId, ConsumerType.Classic, Neighborhood.Westville);
                    // Create suppression immediately so any subsequent Contract notifications in the same tick
                    // do not inject their normal contract text. Keep suppression short-lived.
                    try
                    {
                        SuppressedContracts[customerId] = DateTime.UtcNow.AddMinutes(2);
                        SmartMarketConfig.LogDebug($"[Robbery] Suppressing next contract notification for {customerId} until {SuppressedContracts[customerId]:O}");
                    }
                    catch { }

                    var penaltyMessage = MessageGenerator.GeneratePenaltyMessage(penaltyProfile);
                    // Queue the penalty message instead of attempting immediate live injection to avoid duplicate Notify calls
                    try
                    {
                        var mem = MemorySystem.GetMemory(customerId);
                        if (mem != null)
                        {
                            if (mem.PendingOutgoingMessages == null) mem.PendingOutgoingMessages = new System.Collections.Generic.List<string>();
                            mem.PendingOutgoingMessages.Add(penaltyMessage);
                            MemorySystem.Save();
                            MelonLogger.Msg($"[SmartMarket] Penalty message queued for {customerId}: {penaltyMessage}");

                            // Try to trigger an immediate injection by invoking NotifyPlayerOfContract on the live Customer if present.
                            try
                            {
                                var liveCustomerForNotify = FindCustomerById(customerId);
                                if (liveCustomerForNotify != null)
                                {
                                    try
                                    {
                                        var chain = new Il2CppScheduleOne.UI.Phone.Messages.MessageChain();
                                        chain.Messages = new Il2CppSystem.Collections.Generic.List<string>();
                                        // We do NOT add the penaltyMessage directly here because ContractPatches will inject PendingOutgoingMessages.
                                        // Still, sending an empty chain is enough to trigger the ContractPatches prefix which will handle queued messages.
                                        liveCustomerForNotify.NotifyPlayerOfContract(null, chain, false, false, false);
                                        MelonLogger.Msg($"[SmartMarket] Triggered NotifyPlayerOfContract for {customerId} to inject queued penalty message.");
                                    }
                                    catch (Exception notifyEx)
                                    {
                                        MelonLogger.Warning($"[SmartMarket] NotifyPlayerOfContract failed for {customerId}: {notifyEx.Message}");
                                    }
                                }
                                else
                                {
                                    SmartMarketConfig.LogDebug($"[Robbery] No live Customer instance found for {customerId} to trigger immediate notify; queued message will be injected on next contract.");
                                }
                            }
                            catch (Exception exNotify)
                            {
                                MelonLogger.Warning($"[SmartMarket] Error attempting immediate notify for {customerId}: {exNotify.Message}");
                            }
                        }
                        else
                        {
                            MelonLogger.Warning($"[SmartMarket] Could not find memory for {customerId} to queue penalty message.");
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[SmartMarket] Error encolando mensaje de penalización para {customerId}: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SmartMarket] Error enviando mensaje de penalización para {customerId}: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] ApplyCustomerPenalty failed for {customerId}: {ex.Message}");
            }
        }

        public static bool TryConsumeSuppressedContract(string customerId)
        {
            if (string.IsNullOrEmpty(customerId)) return false;
            try
            {
                if (SuppressedContracts.ContainsKey(customerId))
                {
                    var expiry = SuppressedContracts[customerId];
                    if (DateTime.UtcNow <= expiry)
                    {
                        // still valid: consume and allow suppression
                        SuppressedContracts.Remove(customerId);
                        return true;
                    }
                    else
                    {
                        // expired: remove and don't suppress
                        SuppressedContracts.Remove(customerId);
                        return false;
                    }
                }
            }
            catch { }
            return false;
        }

        private static void ClearPendingContractState(string customerId)
        {
            if (string.IsNullOrEmpty(customerId)) return;
            try
            {
                MessagingMonitor.ClearPending(customerId);
            }
            catch { }

            try
            {
                MemorySystem.ClearPendingRequest(customerId);
            }
            catch { }

            try
            {
                if (!SuppressedContracts.ContainsKey(customerId))
                    SuppressedContracts[customerId] = DateTime.UtcNow;
            }
            catch { }
        }

        private static string GetCustomerIdFromNpc(GameObject npcRoot)
        {
            if (npcRoot == null) return string.Empty;

            try
            {
                var customer = FindCustomerForNpc(npcRoot);
                if (customer != null)
                    return GetCustomerId(customer);
            }
            catch { }

            return npcRoot.name ?? string.Empty;
        }

        private static Customer FindCustomerForNpc(GameObject npcRoot)
        {
            if (npcRoot == null)
                return null;

            try
            {
                foreach (var customer in UnityEngine.Object.FindObjectsOfType<Customer>())
                {
                    if (customer == null || customer.gameObject == null) continue;
                    if (string.Equals(customer.gameObject.name, npcRoot.name, StringComparison.OrdinalIgnoreCase))
                        return customer;
                    try
                    {
                        if (customer.NPC != null && customer.NPC.gameObject == npcRoot)
                            return customer;
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        private static string GetCustomerId(Customer customer)
        {
            if (customer == null) return string.Empty;
            try
            {
                if (customer.gameObject != null && !string.IsNullOrEmpty(customer.gameObject.name))
                    return customer.gameObject.name;
            }
            catch { }
            return string.Empty;
        }

        private static Customer FindCustomerById(string customerId)
        {
            if (string.IsNullOrEmpty(customerId)) return null;

            try
            {
                foreach (var customer in UnityEngine.Object.FindObjectsOfType<Customer>())
                {
                    if (customer == null || customer.gameObject == null) continue;
                    if (string.Equals(customer.gameObject.name, customerId, StringComparison.OrdinalIgnoreCase))
                        return customer;
                }
            }
            catch { }

            return null;
        }

        private static GameObject ResolveNpcRoot(GameObject source)
        {
            if (source == null) return null;
            try
            {
                var npc = source.GetComponent<NPC>() ?? source.GetComponentInParent<NPC>();
                if (npc != null && npc.gameObject != null)
                    return npc.gameObject;
            }
            catch { }
            return source;
        }

        private static string GetObjectName(GameObject gameObject)
        {
            return gameObject != null && gameObject.name != null ? gameObject.name : "Desconocido";
        }
    }
}
