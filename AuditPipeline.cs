// AuditPipeline.cs
// Auditoría exhaustiva del pipeline de generación de contratos
// Logs COMPLETOS de cada paso para identificar exactamente dónde se rompen las cosas

using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Economy;
using MelonLoader;
using MelonLogger = SmartMarket.SmartMarketLogger;

namespace SmartMarket.Core
{
    public static class AuditPipeline
    {
        /// <summary>
        /// Audita completamente la lista de productos disponibles
        /// Muestra por cada producto si está marcado como ForSale y por qué fue incluido/excluido
        /// </summary>
        public static void AuditAvailableProducts(Customer customer, Dealer dealer, Il2CppSystem.Collections.Generic.List<ProductDefinition> availableProducts)
        {
            try
            {
                string customerName = customer?.gameObject?.name ?? "Unknown";
                MelonLogger.Msg($"[AUDIT-PRODUCTS] ========== AUDITORÍA DE PRODUCTOS DISPONIBLES ==========");
                MelonLogger.Msg($"[AUDIT-PRODUCTS] NPC: {customerName}, Dealer: {dealer?.gameObject?.name ?? "None"}");
                
                if (availableProducts == null)
                {
                    MelonLogger.Msg($"[AUDIT-PRODUCTS] ⚠️ availableProducts es NULL");
                    return;
                }

                MelonLogger.Msg($"[AUDIT-PRODUCTS] Total de productos disponibles: {availableProducts.Count}");

                // Obtener lista de productos marcados como ForSale
                var listedProducts = new HashSet<string>();
                try
                {
                    if (ProductManager.Instance != null)
                    {
                        // Intentar obtener ListedProducts usando reflexión
                        var pmType = ProductManager.Instance.GetType();
                        var listedProp = pmType.GetProperty("ListedProducts");
                        if (listedProp != null)
                        {
                            var listedObj = listedProp.GetValue(ProductManager.Instance);
                            if (listedObj is System.Collections.IEnumerable listedList)
                            {
                                foreach (var item in listedList)
                                {
                                    if (item is string productId)
                                        listedProducts.Add(productId);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[AUDIT-PRODUCTS] No se pudo obtener lista de ListedProducts: {ex.Message}");
                }

                // Auditar cada producto disponible
                for (int i = 0; i < availableProducts.Count; i++)
                {
                    var product = availableProducts[i];
                    if (product == null)
                    {
                        MelonLogger.Msg($"[AUDIT-PRODUCTS] [{i}] NULL");
                        continue;
                    }

                    string productName = product.name ?? "Unknown";
                    string productId = product.SaveFileName ?? productName;
                    bool isListed = listedProducts.Contains(productId);
                    
                    try
                    {
                        float price = product.Price;
                        EDrugType drugType = product.DrugType;
                        float addictiveness = product.GetAddictiveness();
                        bool isUnlocked = true; // Assume unlocked if it's in the available list

                        MelonLogger.Msg($"[AUDIT-PRODUCTS] [{i}] Name: {productName} | ID: {productId}");
                        MelonLogger.Msg($"[AUDIT-PRODUCTS]       ForSale: {isListed} | Type: {drugType} | Price: {price:F2} | Addiction: {addictiveness:F2}");
                        MelonLogger.Msg($"[AUDIT-PRODUCTS]       ✓ INCLUDED (in GetAvailableProducts list)");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Msg($"[AUDIT-PRODUCTS] [{i}] {productName} - Error reading properties: {ex.Message}");
                    }
                }

                MelonLogger.Msg($"[AUDIT-PRODUCTS] ========== FIN AUDITORÍA PRODUCTOS ==========");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[AUDIT-PRODUCTS] Error en AuditAvailableProducts: {ex.Message}");
            }
        }

        /// <summary>
        /// Audita la decisión de Word of Mouth con comparación completa
        /// </summary>
        public static void AuditWordOfMouthDecision(string customerName, string pendingWordOfMouthProduct, string requestedProductId, bool result)
        {
            try
            {
                MelonLogger.Msg($"[AUDIT-WOM] ========== AUDITORÍA WORD OF MOUTH ==========");
                MelonLogger.Msg($"[AUDIT-WOM] Customer: {customerName}");
                MelonLogger.Msg($"[AUDIT-WOM] PendingWordOfMouthProduct: {(string.IsNullOrEmpty(pendingWordOfMouthProduct) ? "NULL/EMPTY" : pendingWordOfMouthProduct)}");
                MelonLogger.Msg($"[AUDIT-WOM] RequestedProductId: {(string.IsNullOrEmpty(requestedProductId) ? "NULL/EMPTY" : requestedProductId)}");
                
                if (string.IsNullOrEmpty(pendingWordOfMouthProduct))
                {
                    MelonLogger.Msg($"[AUDIT-WOM] REASON: No viral event pending");
                }
                else if (string.IsNullOrEmpty(requestedProductId))
                {
                    MelonLogger.Msg($"[AUDIT-WOM] REASON: RequestedProductId is empty");
                }
                else
                {
                    bool matches = string.Equals(pendingWordOfMouthProduct, requestedProductId, StringComparison.OrdinalIgnoreCase);
                    MelonLogger.Msg($"[AUDIT-WOM] String comparison (case-insensitive): {matches}");
                }
                
                MelonLogger.Msg($"[AUDIT-WOM] RESULT: IsWordOfMouth = {result}");
                MelonLogger.Msg($"[AUDIT-WOM] ========== FIN AUDITORÍA WOM ==========");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[AUDIT-WOM] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Audita la clasificación de producto (base vs mezcla) y extracción de efectos
        /// </summary>
        public static void AuditProductClassification(string customerName, ProductDefinition product, bool isBase, string extractedEffect, string extractedQuality)
        {
            try
            {
                MelonLogger.Msg($"[AUDIT-CLASSIFY] ========== AUDITORÍA CLASIFICACIÓN PRODUCTO ==========");
                MelonLogger.Msg($"[AUDIT-CLASSIFY] Customer: {customerName}");
                MelonLogger.Msg($"[AUDIT-CLASSIFY] Product: {product?.name ?? "NULL"}");
                MelonLogger.Msg($"[AUDIT-CLASSIFY] IsBaseProduct: {isBase}");
                MelonLogger.Msg($"[AUDIT-CLASSIFY] IsMix: {!isBase}");
                
                if (isBase)
                {
                    MelonLogger.Msg($"[AUDIT-CLASSIFY] RequestedEffect extracted: {(string.IsNullOrEmpty(extractedEffect) ? "NONE" : extractedEffect)}");
                    MelonLogger.Msg($"[AUDIT-CLASSIFY] RequestedQuality extracted: {(string.IsNullOrEmpty(extractedQuality) ? "NONE" : extractedQuality)}");
                }
                else
                {
                    MelonLogger.Msg($"[AUDIT-CLASSIFY] REASON: Product is a named mix - NO effects extracted");
                    MelonLogger.Msg($"[AUDIT-CLASSIFY] RequestedEffect: (empty - mix)");
                    MelonLogger.Msg($"[AUDIT-CLASSIFY] RequestedQuality: (empty - mix)");
                }
                
                MelonLogger.Msg($"[AUDIT-CLASSIFY] ========== FIN AUDITORÍA CLASIFICACIÓN ==========");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[AUDIT-CLASSIFY] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Audita la sustitución de producto (vanilla vs SmartMarket)
        /// </summary>
        public static void AuditProductSubstitution(string customerName, string vanillaProduct, string finalProduct, bool wasSubstituted, string reason = "")
        {
            try
            {
                MelonLogger.Msg($"[AUDIT-SUBSTITUTION] ========== AUDITORÍA SUSTITUCIÓN PRODUCTO ==========");
                MelonLogger.Msg($"[AUDIT-SUBSTITUTION] Customer: {customerName}");
                MelonLogger.Msg($"[AUDIT-SUBSTITUTION] Vanilla selected: {vanillaProduct}");
                MelonLogger.Msg($"[AUDIT-SUBSTITUTION] Final product: {finalProduct}");
                MelonLogger.Msg($"[AUDIT-SUBSTITUTION] Substituted: {wasSubstituted}");
                if (!string.IsNullOrEmpty(reason))
                    MelonLogger.Msg($"[AUDIT-SUBSTITUTION] Reason: {reason}");
                MelonLogger.Msg($"[AUDIT-SUBSTITUTION] ========== FIN AUDITORÍA SUSTITUCIÓN ==========");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[AUDIT-SUBSTITUTION] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Audita la persistencia en CustomerMemory - qué se guardó
        /// </summary>
        public static void AuditMemoryPersistence(string customerName, CustomerMemory memory, string phase = "BEFORE SAVE")
        {
            try
            {
                MelonLogger.Msg($"[AUDIT-MEMORY-{phase}] ========== AUDITORÍA PERSISTENCIA MEMORIA ==========");
                MelonLogger.Msg($"[AUDIT-MEMORY-{phase}] Customer: {customerName}");
                
                if (memory == null)
                {
                    MelonLogger.Msg($"[AUDIT-MEMORY-{phase}] ⚠️ CustomerMemory es NULL");
                    return;
                }

                MelonLogger.Msg($"[AUDIT-MEMORY-{phase}] PendingRequestedProductId: {(string.IsNullOrEmpty(memory.PendingRequestedProductId) ? "(empty)" : memory.PendingRequestedProductId)}");
                MelonLogger.Msg($"[AUDIT-MEMORY-{phase}] PendingRequestedEffectId: {(string.IsNullOrEmpty(memory.PendingRequestedEffectId) ? "(empty)" : memory.PendingRequestedEffectId)}");
                MelonLogger.Msg($"[AUDIT-MEMORY-{phase}] PendingRequestedEffectName: {(string.IsNullOrEmpty(memory.PendingRequestedEffectName) ? "(empty)" : memory.PendingRequestedEffectName)}");
                MelonLogger.Msg($"[AUDIT-MEMORY-{phase}] PendingRequestedQuality: {(string.IsNullOrEmpty(memory.PendingRequestedQuality) ? "(empty)" : memory.PendingRequestedQuality)}");
                
                if (memory.PendingRequestedEffectNames != null)
                {
                    MelonLogger.Msg($"[AUDIT-MEMORY-{phase}] PendingRequestedEffectNames count: {memory.PendingRequestedEffectNames.Count}");
                    for (int i = 0; i < memory.PendingRequestedEffectNames.Count && i < 5; i++)
                    {
                        MelonLogger.Msg($"[AUDIT-MEMORY-{phase}]   - {memory.PendingRequestedEffectNames[i]}");
                    }
                }
                
                MelonLogger.Msg($"[AUDIT-MEMORY-{phase}] PendingRequestedQuantity: {memory.PendingRequestedQuantity}");
                MelonLogger.Msg($"[AUDIT-MEMORY-{phase}] PendingWordOfMouthProduct: {(string.IsNullOrEmpty(memory.PendingWordOfMouthProduct) ? "(empty)" : memory.PendingWordOfMouthProduct)}");
                MelonLogger.Msg($"[AUDIT-MEMORY-{phase}] ========== FIN AUDITORÍA MEMORIA ({phase}) ==========");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[AUDIT-MEMORY-{phase}] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Audita la decisión del RNG sobre qué incluir en el contrato
        /// </summary>
        public static void AuditRandomRollDecision(string customerName, bool canRequestEffect, bool canRequestQuality, string selectedEffect, string selectedQuality, string reason = "")
        {
            try
            {
                MelonLogger.Msg($"[AUDIT-RNG] ========== AUDITORÍA RANDOM ROLL ==========");
                MelonLogger.Msg($"[AUDIT-RNG] Customer: {customerName}");
                MelonLogger.Msg($"[AUDIT-RNG] CanRequestEffect: {canRequestEffect}");
                MelonLogger.Msg($"[AUDIT-RNG] CanRequestQuality: {canRequestQuality}");
                
                if (!string.IsNullOrEmpty(selectedEffect) || !string.IsNullOrEmpty(selectedQuality))
                {
                    MelonLogger.Msg($"[AUDIT-RNG] Selected Effect: {(string.IsNullOrEmpty(selectedEffect) ? "NONE" : selectedEffect)}");
                    MelonLogger.Msg($"[AUDIT-RNG] Selected Quality: {(string.IsNullOrEmpty(selectedQuality) ? "NONE" : selectedQuality)}");
                }
                else
                {
                    MelonLogger.Msg($"[AUDIT-RNG] Selected: NEITHER (empty request)");
                    if (!string.IsNullOrEmpty(reason))
                        MelonLogger.Msg($"[AUDIT-RNG] Reason: {reason}");
                }
                
                MelonLogger.Msg($"[AUDIT-RNG] ========== FIN AUDITORÍA RNG ==========");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[AUDIT-RNG] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Audita el estado final del contrato antes de devolverlo
        /// </summary>
        public static void AuditFinalContractState(string customerName, Il2CppScheduleOne.Product.ProductDefinition product, string quality, object contract)
        {
            try
            {
                MelonLogger.Msg($"[AUDIT-FINAL] ========== AUDITORÍA CONTRATO FINAL ==========");
                MelonLogger.Msg($"[AUDIT-FINAL] Customer: {customerName}");
                
                if (product != null)
                    MelonLogger.Msg($"[AUDIT-FINAL] Product: {product.name} (ID: {product.SaveFileName})");
                else
                    MelonLogger.Msg($"[AUDIT-FINAL] Product: NULL");
                
                MelonLogger.Msg($"[AUDIT-FINAL] Quality: {(string.IsNullOrEmpty(quality) ? "NONE" : quality)}");
                
                if (contract != null)
                {
                    try
                    {
                        var productsProp = contract.GetType().GetProperty("Products");
                        if (productsProp != null)
                        {
                            var products = productsProp.GetValue(contract);
                            if (products != null)
                            {
                                var entriesProp = products.GetType().GetProperty("entries");
                                if (entriesProp != null)
                                {
                                    var entries = entriesProp.GetValue(products);
                                    if (entries is System.Collections.IEnumerable enumerable)
                                    {
                                        int count = 0;
                                        foreach (var entry in enumerable)
                                        {
                                            if (count >= 3) break;
                                            
                                            var productIdProp = entry.GetType().GetProperty("ProductID");
                                            var qualityProp = entry.GetType().GetProperty("Quality");
                                            var quantityProp = entry.GetType().GetProperty("Quantity");
                                            
                                            string pId = productIdProp?.GetValue(entry)?.ToString() ?? "UNKNOWN";
                                            string q = qualityProp?.GetValue(entry)?.ToString() ?? "UNKNOWN";
                                            string qty = quantityProp?.GetValue(entry)?.ToString() ?? "0";
                                            
                                            MelonLogger.Msg($"[AUDIT-FINAL]   [{count}] ProductID: {pId} | Quality: {q} | Quantity: {qty}");
                                            count++;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[AUDIT-FINAL] Error reading contract details: {ex.Message}");
                    }
                }
                
                MelonLogger.Msg($"[AUDIT-FINAL] ========== FIN AUDITORÍA FINAL ==========");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[AUDIT-FINAL] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Audita el estado del contrato justo antes de construir la UI (HUD)
        /// </summary>
        public static void AuditHUDContractState(string customerName, object contract, CustomerMemory memory)
        {
            try
            {
                MelonLogger.Msg($"[AUDIT-HUD] ========== AUDITORÍA ESTADO HUD ==========");
                MelonLogger.Msg($"[AUDIT-HUD] Customer: {customerName}");
                
                string productName = "UNKNOWN";
                string productId = "UNKNOWN";
                string quality = "NONE";
                
                if (contract != null)
                {
                    try
                    {
                        var productsProp = contract.GetType().GetProperty("Products");
                        if (productsProp != null)
                        {
                            var products = productsProp.GetValue(contract);
                            if (products != null)
                            {
                                var entriesProp = products.GetType().GetProperty("entries");
                                if (entriesProp != null)
                                {
                                    var entries = entriesProp.GetValue(products);
                                    if (entries is System.Collections.IEnumerable enumerable)
                                    {
                                        foreach (var entry in enumerable)
                                        {
                                            var productIdProp = entry.GetType().GetProperty("ProductID");
                                            var qualityProp = entry.GetType().GetProperty("Quality");
                                            
                                            productId = productIdProp?.GetValue(entry)?.ToString() ?? "UNKNOWN";
                                            quality = qualityProp?.GetValue(entry)?.ToString() ?? "NONE";
                                            productName = productId;
                                            break; // Solo el primer entry
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[AUDIT-HUD] Error reading contract: {ex.Message}");
                    }
                }
                
                MelonLogger.Msg($"[AUDIT-HUD] Contract Product: {productName} (ID: {productId})");
                MelonLogger.Msg($"[AUDIT-HUD] Contract Quality: {quality}");
                
                if (memory != null)
                {
                    MelonLogger.Msg($"[AUDIT-HUD] Memory Product: {memory.PendingRequestedProductId}");
                    MelonLogger.Msg($"[AUDIT-HUD] Memory Effect: {(string.IsNullOrEmpty(memory.PendingRequestedEffectName) ? "NONE" : memory.PendingRequestedEffectName)}");
                    MelonLogger.Msg($"[AUDIT-HUD] Memory Quality: {(string.IsNullOrEmpty(memory.PendingRequestedQuality) ? "NONE" : memory.PendingRequestedQuality)}");
                }
                
                MelonLogger.Msg($"[AUDIT-HUD] ========== FIN AUDITORÍA HUD ==========");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[AUDIT-HUD] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Audita la construcción completa del MessageContext
        /// </summary>
        public static void AuditMessageContext(MessageContext context)
        {
            try
            {
                if (context == null)
                {
                    MelonLogger.Msg($"[AUDIT-CONTEXT] ⚠️ MessageContext es NULL");
                    return;
                }

                MelonLogger.Msg($"[AUDIT-CONTEXT] ========== AUDITORÍA MESSAGE CONTEXT ==========");
                MelonLogger.Msg($"[AUDIT-CONTEXT] Customer: {context.CustomerName}");
                MelonLogger.Msg($"[AUDIT-CONTEXT] RequestedProduct: {context.RequestedProductName} (ID: {context.RequestedProductId})");
                MelonLogger.Msg($"[AUDIT-CONTEXT] RequestedEffect: {(string.IsNullOrEmpty(context.RequestedEffect) ? "(empty)" : context.RequestedEffect)}");
                MelonLogger.Msg($"[AUDIT-CONTEXT] RequestedEffectId: {(string.IsNullOrEmpty(context.RequestedEffectId) ? "(empty)" : context.RequestedEffectId)}");
                MelonLogger.Msg($"[AUDIT-CONTEXT] RequestedQuality: {(string.IsNullOrEmpty(context.RequestedQuality) ? "(empty)" : context.RequestedQuality)}");
                MelonLogger.Msg($"[AUDIT-CONTEXT] IsWordOfMouth: {context.IsWordOfMouth}");
                MelonLogger.Msg($"[AUDIT-CONTEXT] IsEffectDriven: {context.IsEffectDriven}");
                MelonLogger.Msg($"[AUDIT-CONTEXT] IsRepeatRequest: {context.IsRepeatRequest}");
                MelonLogger.Msg($"[AUDIT-CONTEXT] RequestedQuantity: {context.RequestedQuantity}");
                MelonLogger.Msg($"[AUDIT-CONTEXT] Profile Type: {context.Profile?.Type.ToString() ?? "Unknown"}");
                MelonLogger.Msg($"[AUDIT-CONTEXT] ========== FIN AUDITORÍA CONTEXT ==========");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[AUDIT-CONTEXT] Error: {ex.Message}");
            }
        }
    }
}