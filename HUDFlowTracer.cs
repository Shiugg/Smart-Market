// HUDFlowTracer.cs
// Rastreador DEFINITIVO del flujo completo del HUD
// Objetivo: Identificar el PRIMER método que modifica el HUD después de Dealer.AddContract()
// 
// Pregunta a responder:
// ¿Qué método se ejecuta inmediatamente después de Dealer.AddContract() y crea/actualiza
// la línea correspondiente en el HUD izquierdo?

using HarmonyLib;
using MelonLoader;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.Economy;
using System;
using System.Reflection;

namespace SmartMarket.Patches
{
    /// <summary>
    /// Rastreador completo del flujo: Dealer.AddContract → HUD Update
    /// Con información detallada de cada paso
    /// </summary>
    public static class HUDFlowTracer
    {
        private const string MARKER = "[HUD-FLOW-TRACE]";

        /// <summary>
        /// Punto de entrada: Dealer.AddContract()
        /// Registra cuándo termina y qué sigue
        /// </summary>
        [HarmonyPatch(typeof(Dealer), nameof(Dealer.AddContract))]
        public static class Trace_Dealer_AddContract
        {
            public static void Postfix(Dealer __instance, Contract contract)
            {
                try
                {
                    string dealerName = __instance?.gameObject?.name ?? "<null>";
                    string customerName = "<unknown>";
                    
                    try
                    {
                        var customer = contract?.Customer?.TryCast<Customer>();
                        if (customer != null)
                        {
                            customerName = customer.gameObject?.name ?? "<null>";
                        }
                    }
                    catch { }

                    MelonLogger.Msg($"{MARKER} ════════════════════════════════════════════");
                    MelonLogger.Msg($"{MARKER} Dealer.AddContract() COMPLETADO");
                    MelonLogger.Msg($"{MARKER} Dealer: {dealerName}");
                    MelonLogger.Msg($"{MARKER} Customer: {customerName}");
                    MelonLogger.Msg($"{MARKER} ════════════════════════════════════════════");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"{MARKER} Error tracing Dealer.AddContract: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// QuestManager.ContractAccepted() - Crea el Contract
        /// </summary>
        [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.ContractAccepted))]
        public static class Trace_QuestManager_ContractAccepted
        {
            public static void Postfix(QuestManager __instance, ref Contract __result)
            {
                try
                {
                    if (__result == null)
                        return;

                    string questTitle = __result?.Title ?? "<null>";
                    MelonLogger.Msg($"{MARKER} QuestManager.ContractAccepted() RETORNÓ Contract");
                    MelonLogger.Msg($"{MARKER}   Title: {questTitle}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"{MARKER} Error tracing ContractAccepted: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// QuestHUDUI.Initialize() - Crea panel principal del quest
        /// </summary>
        [HarmonyPatch(typeof(QuestHUDUI), nameof(QuestHUDUI.Initialize))]
        public static class Trace_QuestHUDUI_Initialize
        {
            public static void Prefix(QuestHUDUI __instance, Quest quest)
            {
                try
                {
                    if (quest == null)
                        return;

                    string questType = quest.GetType().Name;
                    string questTitle = quest.Title ?? "<null>";
                    string isContract = quest.TryCast<Contract>() != null ? "✓ CONTRACT" : "plain quest";

                    MelonLogger.Msg($"{MARKER} QuestHUDUI.Initialize() ENTRANDO");
                    MelonLogger.Msg($"{MARKER}   Quest Type: {questType} ({isContract})");
                    MelonLogger.Msg($"{MARKER}   Title: {questTitle}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"{MARKER} Error tracing QuestHUDUI.Initialize: {ex.Message}");
                }
            }

            public static void Postfix(QuestHUDUI __instance, Quest quest)
            {
                try
                {
                    if (quest == null)
                        return;

                    var mainLabel = __instance?.MainLabel;
                    string labelText = "<no label>";
                    
                    if (mainLabel != null)
                    {
                        try
                        {
                            var textProperty = mainLabel.GetType().GetProperty("text");
                            if (textProperty != null)
                                labelText = textProperty.GetValue(mainLabel) as string ?? "<null>";
                        }
                        catch { }
                    }

                    MelonLogger.Msg($"{MARKER} QuestHUDUI.Initialize() COMPLETADO");
                    MelonLogger.Msg($"{MARKER}   MainLabel text: {labelText}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"{MARKER} Error in QuestHUDUI.Initialize postfix: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// QuestHUDUI.UpdateUI() - Actualiza panel principal
        /// </summary>
        [HarmonyPatch(typeof(QuestHUDUI), nameof(QuestHUDUI.UpdateUI))]
        public static class Trace_QuestHUDUI_UpdateUI
        {
            public static void Prefix(QuestHUDUI __instance)
            {
                try
                {
                    var quest = __instance?.Quest;
                    if (quest == null)
                        return;

                    string isContract = quest.TryCast<Contract>() != null ? "✓ CONTRACT" : "plain quest";
                    MelonLogger.Msg($"{MARKER} QuestHUDUI.UpdateUI() ENTRANDO ({isContract})");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"{MARKER} Error in QuestHUDUI.UpdateUI prefix: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// QuestEntry.CreateEntryUI() - Crea cada línea individual
        /// </summary>
        [HarmonyPatch(typeof(QuestEntry), nameof(QuestEntry.CreateEntryUI))]
        public static class Trace_QuestEntry_CreateEntryUI
        {
            public static void Prefix(QuestEntry __instance)
            {
                try
                {
                    if (__instance == null)
                        return;

                    string entryTitle = __instance.Title ?? "<null>";
                    string parentQuestTitle = __instance.ParentQuest?.Title ?? "<null>";
                    string parentType = __instance.ParentQuest?.GetType().Name ?? "<null>";
                    string isContract = __instance.ParentQuest?.TryCast<Contract>() != null ? "✓ CONTRACT" : "plain quest";

                    MelonLogger.Msg($"{MARKER} QuestEntry.CreateEntryUI() ENTRANDO");
                    MelonLogger.Msg($"{MARKER}   Entry Title: {entryTitle}");
                    MelonLogger.Msg($"{MARKER}   Parent Quest: {parentQuestTitle} ({parentType}, {isContract})");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"{MARKER} Error in CreateEntryUI prefix: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// QuestEntryHUDUI.Initialize() - Inicializa cada línea individual
        /// ESTE ES NUESTRO PUNTO DE INYECCIÓN HIPOTÉTICO
        /// </summary>
        [HarmonyPatch(typeof(QuestEntryHUDUI), nameof(QuestEntryHUDUI.Initialize), new[] { typeof(QuestEntry) })]
        public static class Trace_QuestEntryHUDUI_Initialize
        {
            public static void Prefix(QuestEntryHUDUI __instance, QuestEntry entry)
            {
                try
                {
                    if (entry == null)
                        return;

                    string entryTitle = entry.Title ?? "<null>";
                    string parentQuestTitle = entry.ParentQuest?.Title ?? "<null>";
                    string isContract = entry.ParentQuest?.TryCast<Contract>() != null ? "✓ CONTRACT" : "plain quest";

                    MelonLogger.Msg($"{MARKER} ★★★ QuestEntryHUDUI.Initialize() ENTRANDO ★★★");
                    MelonLogger.Msg($"{MARKER}   Entry: {entryTitle}");
                    MelonLogger.Msg($"{MARKER}   Parent: {parentQuestTitle} ({isContract})");
                    
                    // Log específico para verificar si el patch va a ejecutarse
                    if (entry.ParentQuest?.TryCast<Contract>() != null)
                    {
                        var customer = entry.ParentQuest.TryCast<Contract>()?.Customer?.TryCast<Customer>();
                        if (customer != null)
                        {
                            MelonLogger.Msg($"{MARKER}   → Customer available for SmartMarket lookup: {customer.gameObject?.name ?? "<null>"}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"{MARKER} Error in QuestEntryHUDUI.Initialize prefix: {ex.Message}");
                }
            }

            public static void Postfix(QuestEntryHUDUI __instance, QuestEntry entry)
            {
                try
                {
                    if (__instance == null || entry == null)
                        return;

                    var mainLabel = __instance.MainLabel;
                    string labelText = "<no label>";

                    if (mainLabel != null)
                    {
                        try
                        {
                            var textProperty = mainLabel.GetType().GetProperty("text");
                            if (textProperty != null)
                                labelText = textProperty.GetValue(mainLabel) as string ?? "<null>";
                        }
                        catch { }
                    }

                    MelonLogger.Msg($"{MARKER} QuestEntryHUDUI.Initialize() COMPLETADO");
                    MelonLogger.Msg($"{MARKER}   MainLabel.text: {labelText}");
                    
                    // Verificar si nuestro patch inyectó contenido
                    if (labelText.Contains("Requested:"))
                    {
                        MelonLogger.Msg($"{MARKER} ✅ SmartMarket overlay DETECTADO en el texto");
                    }
                    else if (entry.ParentQuest?.TryCast<Contract>() != null)
                    {
                        MelonLogger.Msg($"{MARKER} ⚠ Contract pero sin overlay SmartMarket aún (puede inyectarse luego)");
                    }
                    
                    MelonLogger.Msg($"{MARKER} ★★★ PUEDE MODIFICAR ESTE TEXTO ★★★");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"{MARKER} Error in QuestEntryHUDUI.Initialize postfix: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// QuestEntryHUDUI.UpdateUI() - Actualiza cada línea
        /// </summary>
        [HarmonyPatch(typeof(QuestEntryHUDUI), nameof(QuestEntryHUDUI.UpdateUI))]
        public static class Trace_QuestEntryHUDUI_UpdateUI
        {
            public static void Prefix(QuestEntryHUDUI __instance)
            {
                try
                {
                    var entry = __instance?.QuestEntry;
                    if (entry == null)
                        return;

                    string entryTitle = entry.Title ?? "<null>";
                    string isContract = entry.ParentQuest?.TryCast<Contract>() != null ? "✓ CONTRACT" : "plain quest";

                    MelonLogger.Msg($"{MARKER} QuestEntryHUDUI.UpdateUI() ENTRANDO");
                    MelonLogger.Msg($"{MARKER}   Entry: {entryTitle} ({isContract})");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"{MARKER} Error in UpdateUI prefix: {ex.Message}");
                }
            }

            public static void Postfix(QuestEntryHUDUI __instance)
            {
                try
                {
                    var mainLabel = __instance?.MainLabel;
                    if (mainLabel == null)
                        return;

                    string labelText = "<no label>";
                    try
                    {
                        var textProperty = mainLabel.GetType().GetProperty("text");
                        if (textProperty != null)
                            labelText = textProperty.GetValue(mainLabel) as string ?? "<null>";
                    }
                    catch { }

                    MelonLogger.Msg($"{MARKER} QuestEntryHUDUI.UpdateUI() COMPLETADO");
                    MelonLogger.Msg($"{MARKER}   MainLabel.text: {labelText}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"{MARKER} Error in UpdateUI postfix: {ex.Message}");
                }
            }
        }
    }
}
