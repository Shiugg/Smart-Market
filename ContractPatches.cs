// ContractPatches.cs
// Parches específicos para la generación y manejo de contratos
// Responsables de capturar especificaciones de contratos y almacenarlas en memoria
//
// Flujo de ejecución:
//   1. Customer.TryGenerateContract()  → genera ContractInfo
//   2. Customer.NotifyPlayerOfContract() ← PUNTO DE INYECCIÓN
//      ├─ Extractores de efectos y calidad
//      └─ Almacenamiento en MemorySystem

using HarmonyLib;
using MelonLoader;
using MelonLogger = SmartMarket.SmartMarketLogger;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI.Phone.Messages;
using Il2CppScheduleOne.Product;
using SmartMarket.Core;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SmartMarket.Patches
{
    /// <summary>
    /// PARCHE 1: Customer.NotifyPlayerOfContract Prefix
    /// 
    /// Captura las especificaciones del contrato (producto, efectos, calidad, cantidad)
    /// ANTES de que el jugador vea el mensaje en el celular, y las almacena en MemorySystem
    /// para que otros componentes (HUD, Decisiones de IA) puedan consultarlas.
    /// 
    /// El momento de ejecución es crítico: ANTES de NotifyPlayerOfContract,
    /// garantiza que los datos estén disponibles para:
    ///   - QuestHUDPatches (leer e inyectar en el overlay)
    ///   - DealerPatches (decisiones de aceptación de contrato)
    ///   - MessageGenerator (generar respuestas contextuales)
    /// </summary>
    [HarmonyPatch(typeof(Customer), nameof(Customer.NotifyPlayerOfContract))]
    public static class Customer_NotifyPlayer_Patch
    {
        public static void Prefix(
            Customer __instance,
            ContractInfo contract,
            MessageChain offerMessage,
            bool canAccept,
            bool canReject,
            bool canCounterOffer
        )
        {
            try
            {
                if (__instance == null || contract == null)
                    return;

                // Obtener nombre del cliente desde GameObject
                string customerName = __instance.gameObject != null
                    ? __instance.gameObject.name
                    : "UnknownCustomer";

                // Obtener o crear registro de memoria para este cliente
                var memory = MemorySystem.GetMemory(customerName);
                if (memory == null)
                {
                    memory = new CustomerMemory();
                }

                // ═══════════════════════════════════════════════════════════
                // SECCIÓN 1: EXTRACCIÓN DE ESPECIFICACIONES DEL CONTRATO
                // ═══════════════════════════════════════════════════════════
                
                // Usar MessageContextBuilder para construir contexto completo del contrato
                // (Build extrae ProductID, Quantity, Quality, ProductDefinition internamente)
                var ctx = MessageContextBuilder.Build(__instance, contract);
                
                string requestedProductId = ctx?.RequestedProductId ?? string.Empty;
                int requestedQuantity = ctx?.RequestedQuantity ?? 0;

                // Almacenar IDs y cantidades
                memory.PendingRequestedProductId = requestedProductId;
                memory.PendingRequestedQuantity = requestedQuantity;

                // ═══════════════════════════════════════════════════════════
                // SECCIÓN 2: EXTRACCIÓN DE EFECTOS Y CALIDAD
                // ═══════════════════════════════════════════════════════════

                try
                {
                    if (ctx != null && ctx.RequestedProductDefinition != null)
                    {
                        // ✅ PARCHE 1 ACTUALIZADO: Extraer TODOS los efectos (no solo el primero)
                        var (allEffectIds, allEffectNames) = MessageContextBuilder.ExtractAllProductEffects(ctx.RequestedProductDefinition);

                        // Rellenar listas de efectos múltiples con TODOS los efectos
                        memory.PendingRequestedEffectIds = allEffectIds;
                        memory.PendingRequestedEffectNames = allEffectNames;

                        // Mantener campos singulares legacy poblados con primer elemento para retrocompatibilidad
                        memory.PendingRequestedEffectId = allEffectIds.Count > 0 ? allEffectIds[0] : string.Empty;
                        memory.PendingRequestedEffectName = allEffectNames.Count > 0 ? allEffectNames[0] : string.Empty;

                        // La calidad viene del MessageContext
                        memory.PendingRequestedQuality = ctx.RequestedQuality ?? string.Empty;

                        MelonLogger.Msg($"[SmartMarket] Contrato múltiple-efectos guardado para {customerName}: {allEffectNames.Count} efecto(s), Quality={memory.PendingRequestedQuality}");
                    }
                    else
                    {
                        // No se pudo construir contexto, inicializar vacío
                        memory.PendingRequestedEffectId = string.Empty;
                        memory.PendingRequestedEffectName = string.Empty;
                        memory.PendingRequestedEffectIds = new List<string>();
                        memory.PendingRequestedEffectNames = new List<string>();
                        memory.PendingRequestedQuality = string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SmartMarket] Error al extraer efectos para {customerName}: {ex.Message}");
                    memory.PendingRequestedEffectId = string.Empty;
                    memory.PendingRequestedEffectName = string.Empty;
                    memory.PendingRequestedEffectIds = new List<string>();
                    memory.PendingRequestedEffectNames = new List<string>();
                    memory.PendingRequestedQuality = string.Empty;
                }

                // Persistir cambios en memoria
                MemorySystem.Save();

                // ═══════════════════════════════════════════════════════════
                // SECCIÓN 3: INYECCIÓN DE MENSAJE DINÁMICO (SMS)
                // ═══════════════════════════════════════════════════════════
                try
                {
                    if (offerMessage != null && offerMessage.Messages != null && offerMessage.Messages.Count > 0)
                    {
                        if (ctx != null)
                        {
                            string dynamicMessage = MessageGenerator.GenerateMessage(ctx);
                            if (!string.IsNullOrEmpty(dynamicMessage))
                            {
                                // IMPORTANT: Use applyColor = false because this is the Phone SMS, 
                                // and the phone doesn't support HTML <color> tags properly.
                                // Actually, MessageGenerator already handles calling MessageStyler internally.
                                // But since MessageGenerator is static, let's just assign it.
                                offerMessage.Messages[0] = dynamicMessage;
                                MelonLogger.Msg($"[SmartMarket] SMS dinámico inyectado para {customerName}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SmartMarket] Error al inyectar SMS para {customerName}: {ex.Message}");
                }

                MelonLogger.Msg($"[ContractPatches] ✅ Prefix ejecutado para {customerName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ContractPatches] ❌ Error en Prefix: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// PARCHE 2: Customer.TryGenerateContract Postfix (OBSOLETO - Placeholder)
    /// 
    /// Este parche fue originalmente responsable de algunas validaciones.
    /// La lógica principal se ha movido a Customer_NotifyPlayer_Patch.Prefix
    /// para garantizar un orden correcto de ejecución.
    /// 
    /// Se mantiene como placeholder por compatibilidad con logs históricos.
    /// </summary>
    [HarmonyPatch(typeof(Customer), nameof(Customer.TryGenerateContract))]
    public static class Customer_TryGenerateContract_Patch
    {
        public static void Postfix(Customer __instance, Dealer dealer, ref ContractInfo __result)
        {
            try
            {
                if (__instance == null || __result == null)
                    return;

                string customerName = __instance.gameObject != null ? __instance.gameObject.name : "UnknownCustomer";
                
                // AUDITORÍA: Log de generación de contrato
                // Usar MessageContextBuilder.Build para extraer datos completos
                var ctx = MessageContextBuilder.Build(__instance, __result);
                string productId = ctx?.RequestedProductId ?? "UNKNOWN";
                int quantity = ctx?.RequestedQuantity ?? 0;
                
                MelonLogger.Msg($"[SmartMarket][AUDIT] Contrato generado para {customerName}: ProductID={productId}, Qty={quantity}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] Error en TryGenerateContract.Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Busca un ProductDefinition por su ID en una lista de productos
        /// </summary>
        public static ProductDefinition FindMatchingProduct(string productId, Il2CppSystem.Collections.Generic.List<ProductDefinition> allProducts)
        {
            if (allProducts == null || string.IsNullOrEmpty(productId))
                return null;

            try
            {
                foreach (var product in allProducts)
                {
                    if (product != null && (product.SaveFileName == productId || product.name == productId))
                        return product;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] Error en FindMatchingProduct: {ex.Message}");
            }

            return null;
        }
    }
}
