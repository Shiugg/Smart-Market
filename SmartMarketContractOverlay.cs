using System;
using System.Collections.Generic;
using MelonLoader;
using MelonLogger = SmartMarket.SmartMarketLogger;
using UnityEngine;
using Il2CppScheduleOne.Economy;
using UnityEngine.UI;

namespace SmartMarket.Core
{
    /// <summary>
    /// SmartMarketContractOverlay: Capa independiente que renderiza información adicional 
    /// para contratos que tienen especificaciones de SmartMarket (efectos, calidad).
    /// 
    /// Arquitectura:
    /// - Static manager (NO MonoBehaviour) para evitar problemas con Il2Cpp AddComponent
    /// - Escucha eventos de Dealer.AddContract() y Dealer.CustomerContractEnded()
    /// - Mantiene un caché: Dictionary<Contract, GameObject> para cada overlay
    /// - NO busca UI cada frame; búsqueda única del contenedor con reintentos espaciados
    /// - NO modifica el renderer vanilla ni el objeto Contract
    /// </summary>
    public static class SmartMarketContractOverlay
    {
        /// <summary>
        /// Caché de overlays: Contract → GameObject del overlay
        /// </summary>
        private static Dictionary<object, GameObject> contractOverlays = new Dictionary<object, GameObject>();

        /// <summary>
        /// Referencia cacheada al contenedor del HUD de contratos
        /// Se busca una sola vez o con reintentos espaciados
        /// </summary>
        private static Transform contractsContainer;
        private static bool hasAttemptedContainerSearch = false;
        private static float lastContainerSearchTime = 0f;
        private const float CONTAINER_SEARCH_RETRY_INTERVAL = 5f; // segundos

        public static void Initialize()
        {
            MelonLogger.Msg($"[SmartMarket] [OVERLAY] SmartMarketContractOverlay static manager initialized");
        }

        public static bool IsInitialized()
        {
            return true; // Static class is always "initialized"
        }

        /// <summary>
        /// Llamado desde Dealer.AddContract() patch cuando se añade un nuevo contrato
        /// </summary>
        public static void OnContractAdded(object contract, Customer customer)
        {
            try
            {
                if (contract == null)
                    return;

                // Si no tenemos customer, intentar extraerlo del Contract por reflexión
                if (customer == null)
                {
                    customer = ExtractCustomerFromContract(contract);
                    if (customer == null)
                        return;
                }

                string customerName = customer.gameObject.name;
                var memory = MemorySystem.GetMemory(customerName);

                if (memory == null)
                    return;

                // Verificar si hay especificaciones SmartMarket para este contrato
                // Usar listas nuevas; fallback al campo legacy
                bool hasEffects = (memory.PendingRequestedEffectNames != null && memory.PendingRequestedEffectNames.Count > 0)
                                  || !string.IsNullOrEmpty(memory.PendingRequestedEffectName);
                bool hasQuality = !string.IsNullOrEmpty(memory.PendingRequestedQuality);

                // Si no hay especificaciones, no crear overlay
                if (!hasEffects && !hasQuality)
                {
                    return;
                }

                // Intentar localizar el contenedor si no lo tenemos
                if (contractsContainer == null && !hasAttemptedContainerSearch)
                {
                    TryLocateContractContainer();
                }

                // Si aún no tenemos contenedor, no podemos crear el overlay
                if (contractsContainer == null)
                {
                    if (SmartMarketConfig.DebugEnabled)
                        MelonLogger.Msg($"[SmartMarket] Contract overlay skipped - container not found");
                    return;
                }

                // Crear el overlay
                CreateOverlayForContract(contract, customer, memory);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] Error in OnContractAdded: {ex.Message}");
            }
        }

        /// <summary>
        /// <summary>
        /// Extrae el Customer del objeto Contract usando reflexión
        /// </summary>
        private static Customer ExtractCustomerFromContract(object contract)
        {
            try
            {
                if (contract == null)
                    return null;

                var contractType = contract.GetType();
                
                // Buscar propiedad Customer o NPC o similar
                var customerProp = contractType.GetProperty("Customer") ?? 
                                   contractType.GetProperty("NPC") ??
                                   contractType.GetProperty("Employer") ??
                                   contractType.GetProperty("Dealer");

                if (customerProp != null)
                {
                    var customer = customerProp.GetValue(contract) as Customer;
                    if (customer != null)
                        return customer;
                }

                // Buscar campo Customer
                var customerField = contractType.GetField("Customer") ??
                                    contractType.GetField("NPC") ??
                                    contractType.GetField("m_Customer");

                if (customerField != null)
                {
                    var customer = customerField.GetValue(contract) as Customer;
                    if (customer != null)
                        return customer;
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Llamado desde Dealer.CustomerContractEnded() patch cuando termina un contrato
        /// </summary>
        public static void OnContractEnded(object contract)
        {
            try
            {
                if (contract == null)
                    return;

                if (contractOverlays.TryGetValue(contract, out GameObject overlay))
                {
                    contractOverlays.Remove(contract);
                    if (overlay != null)
                    {
                        UnityEngine.Object.Destroy(overlay);
                        if (SmartMarketConfig.DebugEnabled)
                            MelonLogger.Msg($"[SmartMarket] Contract overlay removed");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] Error in OnContractEnded: {ex.Message}");
            }
        }

        /// <summary>
        /// Intenta localizar el contenedor del HUD de contratos de forma segura
        /// Búsqueda limitada para no bloquear el juego
        /// </summary>
        private static void TryLocateContractContainer()
        {
            try
            {
                hasAttemptedContainerSearch = true;
                lastContainerSearchTime = Time.time;

                // Estrategia: Buscar por GameObject/Canvas name patterns
                // Nombres comunes: "HUD", "ContractList", "PhoneUI", "DeliveryUI", etc.
                
                Canvas[] canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
                
                foreach (Canvas canvas in canvases)
                {
                    if (canvas == null) continue;

                    // Buscar en la jerarquía del canvas
                    Transform t = canvas.transform;
                    string path = GetTransformPath(t);
                    
                    if (SmartMarketConfig.DebugEnabled)
                        MelonLogger.Msg($"[SmartMarket] Exploring canvas: {path}");

                    // Buscar por nombres que sugieran contrato/entrega
                    if (path.IndexOf("contract", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        path.IndexOf("delivery", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        path.IndexOf("quest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        path.IndexOf("task", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        contractsContainer = t;
                        MelonLogger.Msg($"[SmartMarket] Found contracts container: {path}");
                        return;
                    }

                    // Buscar en hijos
                    foreach (Transform child in GetAllChildren(t))
                    {
                        string childPath = GetTransformPath(child);
                        if (childPath.IndexOf("contract", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            childPath.IndexOf("delivery", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            childPath.IndexOf("active", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            contractsContainer = child;
                            MelonLogger.Msg($"[SmartMarket] Found contracts container: {childPath}");
                            return;
                        }
                    }
                }

                MelonLogger.Warning($"[SmartMarket] Could not locate contracts container in UI hierarchy");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] Error in TryLocateContractContainer: {ex.Message}");
            }
        }

        private static string GetTransformPath(Transform t)
        {
            if (t == null) return "";
            var path = t.name;
            var parent = t.parent;
            while (parent != null && path.Length < 100)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private static List<Transform> GetAllChildren(Transform parent, int maxDepth = 3, int currentDepth = 0)
        {
            var result = new List<Transform>();
            if (parent == null || currentDepth >= maxDepth) return result;

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                result.Add(child);
                result.AddRange(GetAllChildren(child, maxDepth, currentDepth + 1));
            }

            return result;
        }

        /// <summary>
        /// Crea un overlay visual para un contrato con especificaciones SmartMarket
        /// </summary>
        private static void CreateOverlayForContract(object contract, Customer customer, CustomerMemory memory)
        {
            try
            {
                // Protección contra duplicación: si ya existe un overlay para este contrato, no crear otro
                if (contractOverlays.TryGetValue(contract, out var existingOverlay))
                {
                    if (existingOverlay != null)
                        return; // Overlay válido ya existe, no hacer nada
                    
                    // Entrada existe pero GameObject fue destruido, limpiar la entrada
                    contractOverlays.Remove(contract);
                }

                // Construir texto del overlay
                string overlayText = BuildOverlayText(memory);

                if (string.IsNullOrEmpty(overlayText))
                    return;

                // Crear GameObject para el overlay
                GameObject overlayGo = new GameObject("SmartMarketOverlay_" + contract.GetHashCode());
                overlayGo.transform.SetParent(contractsContainer, false);

                // Añadir RectTransform
                RectTransform rectTrans = overlayGo.AddComponent<RectTransform>();
                rectTrans.anchoredPosition = Vector2.zero;
                rectTrans.sizeDelta = new Vector2(400, 30);

                // Añadir Text component (UI basic)
                Text textComponent = overlayGo.AddComponent<Text>();
                textComponent.text = overlayText;
                textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                textComponent.fontSize = 14;
                textComponent.fontStyle = FontStyle.Normal;
                textComponent.alignment = TextAnchor.UpperLeft;
                
                // Estilización coherente con SmartMarket
                textComponent.color = new Color(1f, 1f, 1f, 0.9f); // Blanco semi-transparente
                
                // Layout
                LayoutElement layout = overlayGo.AddComponent<LayoutElement>();
                layout.preferredWidth = 400;
                layout.preferredHeight = 30;

                contractOverlays[contract] = overlayGo;

                if (SmartMarketConfig.DebugEnabled)
                    MelonLogger.Msg($"[SmartMarket] Contract overlay created: {overlayText}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] Error creating contract overlay: {ex.Message}");
            }
        }

        /// <summary>
        /// Construye el texto del overlay basado en los datos de MemorySystem
        /// Ejemplo: "Requested: Sedating · Paranoia · High Quality"
        /// Usa las listas multi-efecto; cae back al campo legacy si las listas están vacías.
        /// </summary>
        private static string BuildOverlayText(CustomerMemory memory)
        {
            List<string> parts = new List<string>();

            // Recoger nombres de efectos: preferir la lista nueva, fallback al campo legacy
            if (memory.PendingRequestedEffectNames != null && memory.PendingRequestedEffectNames.Count > 0)
            {
                foreach (var name in memory.PendingRequestedEffectNames)
                    if (!string.IsNullOrEmpty(name))
                        parts.Add(name);
            }
            else if (!string.IsNullOrEmpty(memory.PendingRequestedEffectName))
            {
                parts.Add(memory.PendingRequestedEffectName);
            }

            // Añadir calidad siempre de forma independiente
            if (!string.IsNullOrEmpty(memory.PendingRequestedQuality))
                parts.Add(memory.PendingRequestedQuality);

            if (parts.Count == 0)
                return string.Empty;

            return "Requested: " + string.Join(" · ", parts);
        }
    }
}
