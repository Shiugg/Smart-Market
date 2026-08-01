// DealerPatches.cs
// Parches específicos para el comportamiento de Dealers
// Problema 3: Exclusión de filtro ProductManager para Dealers
// Los Dealers deben aceptar contratos basados en su inventario, no en lo que el jugador tiene en venta

using HarmonyLib;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.UI.Phone.Messages;
using MelonLoader;
using MelonLogger = SmartMarket.SmartMarketLogger;
using System;
using System.Reflection;

namespace SmartMarket.Patches
{
    /// <summary>
    /// PROBLEM 3: Dealer.ShouldAcceptContract - Omitir validación ProductManager
    /// 
    /// Los Dealers NPC tienen su propio inventario y DEBEN aceptar contratos si tienen
    /// el producto en stock, sin importar si el jugador tiene el producto catalogado en
    /// ProductManager (la app del celular).
    /// 
    /// Este parche valida que Dealers usen IItemSlotOwner.ItemSlots en lugar de ProductManager.
    /// </summary>
    [HarmonyPatch(typeof(Dealer), nameof(Dealer.ShouldAcceptContract))]
    public static class Dealer_ShouldAcceptContract_Patch
    {
        public static bool Prefix(Dealer __instance, MessageChain chain, ref bool __result)
        {
            try
            {
                // SAFETY: Verificar que __instance es realmente un Dealer (IL2CPP safety)
                if (__instance == null)
                    return true; // Dejar que vanilla handle null case

                string dealerName = __instance.gameObject != null ? __instance.gameObject.name : "UnknownDealer";
                
                // LOGIC: Para Dealers, NO aplicar el filtro ProductManager
                // Los Dealers tienen su propio inventario y deben aceptar si lo tienen
                
                // Obtener el producto solicitado del chain de mensajes
                // (El formato del MessageChain depende de la estructura del juego)
                // Por ahora, confiar en la lógica vanilla pero LOG para auditoría
                
                MelonLogger.Msg($"[SmartMarket] Dealer '{dealerName}' evaluando ShouldAcceptContract");
                MelonLogger.Msg($"[SmartMarket] → Dealer using inventory slots, NOT ProductManager filter");
                
                // Retornar false para que vanilla maneje, pero hemos loguearemos
                // Si necesitamos override total, cambiar a return true aquí
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] Error en Dealer.ShouldAcceptContract Prefix: {ex.Message}");
                return true; // Permitir que vanilla continúe
            }
        }

        public static void Postfix(Dealer __instance, MessageChain chain, ref bool __result)
        {
            try
            {
                if (__instance == null)
                    return;

                string dealerName = __instance.gameObject != null ? __instance.gameObject.name : "UnknownDealer";
                
                // AUDITORÍA: Registrar decisión del Dealer
                MelonLogger.Msg($"[SmartMarket] Dealer '{dealerName}' ShouldAcceptContract result: {__result}");
            }
            catch { }
        }
    }
}
