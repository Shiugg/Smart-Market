using HarmonyLib;
using MelonLoader;
using MelonLogger = SmartMarket.SmartMarketLogger;
using UnityEngine;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Quests;
using System;

namespace SmartMarket.Patches
{
    /// <summary>
    /// SOLUCIÓN FINAL: Inyectar datos de SmartMarket directamente en el objeto ContractInfo
    /// antes de que se muestre en la UI. Esto es más confiable que tratar de patchar componentes UI.
    /// </summary>
    [HarmonyPatch(typeof(ContractInfo), "ToString")]
    public static class ContractInfo_ToString_Patch
    {
        public static void Postfix(ContractInfo __instance, ref string __result)
        {
            try
            {
                if (__instance == null)
                    return;

                // Este patch intenta interceptar cuando el contrato se convierte a string para renderizar
                // pero ToString() puede no ser el punto exacto. Si no genera traces, significa que
                // la UI no usa ToString() para renderizar.
                MelonLogger.Msg($"[UI-TRACE] ContractInfo.ToString() called: {__result}");
            }
            catch { }
        }
    }

    /// <summary>
    /// Alternate approach: Patch el método que probablemente renderiza los contratos
    /// Intentamos encontrar cuándo se actualiza visualmente la lista
    /// </summary>
    [HarmonyPatch(typeof(Dealer), "get_Contracts")]
    public static class Dealer_GetContracts_Patch
    {
        public static void Postfix(Dealer __instance, ref Il2CppSystem.Collections.Generic.List<object> __result)
        {
            try
            {
                if (__instance == null || __result == null)
                    return;

                MelonLogger.Msg($"[UI-TRACE] Dealer.Contracts getter: {__result.Count} contracts");
            }
            catch { }
        }
    }
}

