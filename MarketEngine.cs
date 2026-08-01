// Estado actual: Contiene la lógica principal para elegir productos entre una lista de candidatos y la ruleta ponderada actual.
// Riesgos si se modifica: Cambiar pesos o el flujo de selección puede alterar comportamiento existente de NPCs. No eliminar la lista de candidatos ni filtrar la publicación.
// Integración propuesta: Añadir una capa de scoring (DemandEngine) que se use para ponderar pero que preserve la lista original.
using Il2CppSystem.Collections.Generic;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Economy;
using MelonLoader;
using MelonLogger = SmartMarket.SmartMarketLogger;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using System;
using System.Collections.Generic;


namespace SmartMarket.Core
{
    public static class MarketEngine
    {
        /// <summary>
        /// Selección principal de producto con pesos dinámicos.
        /// </summary>
        public static ProductDefinition PickProduct(Il2CppSystem.Collections.Generic.List<ProductDefinition> orderableProducts, Customer customer, Dealer dealer)
        {
            if (orderableProducts == null || orderableProducts.Count == 0)
            {
                // FALLBACK: Si no hay productos ordenables, usar catálogo del jugador (ListedProducts)
                try
                {
                    var pm = ProductManager.Instance;
                    if (pm != null)
                    {
                        var pmType = pm.GetType();
                        var listedProp = pmType.GetProperty("ListedProducts");
                        
                        if (listedProp != null)
                        {
                            var listedObj = listedProp.GetValue(pm);
                            if (listedObj is System.Collections.IEnumerable listedList)
                            {
                                var fallbackList = new Il2CppSystem.Collections.Generic.List<ProductDefinition>();
                                
                                // Intentar obtener todos los productos y filtrar por los que están en ListedProducts
                                if (pm.AllProducts != null)
                                {
                                    var listedIds = new HashSet<string>();
                                    foreach (var item in listedList)
                                    {
                                        if (item is string id)
                                            listedIds.Add(id);
                                    }
                                    
                                    foreach (var product in pm.AllProducts)
                                    {
                                        if (product != null)
                                        {
                                            string prodId = product.SaveFileName ?? product.name ?? "";
                                            if (listedIds.Contains(prodId))
                                            {
                                                fallbackList.Add(product);
                                            }
                                        }
                                    }
                                }
                                
                                if (fallbackList.Count > 0)
                                {
                                    MelonLogger.Msg($"[SmartMarket] PickProduct fallback: Using {fallbackList.Count} products from PlayerCatalog");
                                    return RunWeightedLottery(fallbackList, customer);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SmartMarket] Error in PickProduct fallback: {ex.Message}");
                }
                
                return null;
            }

            if (dealer != null)
            {
                // Es un Dealer NPC, aplicamos personalidad
                return DealerLogic.ApplyPersonality(customer, dealer);
            }

            // Mercado global (Jugador)
            Il2CppSystem.Collections.Generic.List<ProductDefinition> globalList = null;
            try
            {
                globalList = customer.GetOrderableProducts(null);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] Error llamando a GetOrderableProducts(null): {ex.Message}");
                try
                {
                    var method = AccessTools.Method(typeof(Customer), "GetOrderableProducts", new Type[] { typeof(Dealer) });
                    if (method != null)
                    {
                        var res = method.Invoke(customer, new object[] { null });
                        globalList = res as Il2CppSystem.Collections.Generic.List<ProductDefinition>;
                    }
                }
                catch (Exception rex)
                {
                    MelonLogger.Warning($"[SmartMarket] Fallback reflection GetOrderableProducts falló: {rex.Message}");
                }
            }

            if (globalList == null) globalList = new Il2CppSystem.Collections.Generic.List<ProductDefinition>();
            return RunWeightedLottery(globalList, customer);
        }

        public static ProductDefinition RunWeightedLottery(Il2CppSystem.Collections.Generic.List<ProductDefinition> products, Customer customer = null)
        {
            if (products == null || products.Count == 0) return null;

            if (SmartMarketConfig.DebugEnabled)
                MelonLogger.Debug("SCORE", $"Starting weighted lottery for {products.Count} products for customer {customer?.gameObject?.name ?? "unknown"}");
 
            float totalWeight = 0f;
            System.Collections.Generic.List<float> weights = new System.Collections.Generic.List<float>();

            foreach (var product in products)
            {
                // PHASE 1: Filter products not for sale in ProductManager
                // Only products marked as accepting orders should participate in the lottery
                float weight = CalculateProductWeight(product, customer);
                
                // Apply ProductManager filter: if product is not accepting orders, weight = 0
                if (!IsProductAcceptingOrders(product))
                {
                    weight = 0f;
                    if (SmartMarketConfig.DebugEnabled)
                        MelonLogger.Debug("SCORE", $"Product '{product?.name ?? "unknown"}' filtered out (not accepting orders)");
                }
                
                weights.Add(weight);
                totalWeight += weight;
            }

            float randomRoll = UnityEngine.Random.Range(0f, totalWeight);
            float currentSum = 0f;

            for (int i = 0; i < products.Count; i++)
            {
                currentSum += weights[i];
                if (randomRoll <= currentSum)
                {
                    return products[i];
                }
            }

            return products[0];
        }

        private static float CalculateProductWeight(ProductDefinition product, Customer customer)
        {
            float weight = 10f; // Peso base

            if (product == null) return weight;

            try
            {
                // 1. Base weights
                float addictiveness = product.GetAddictiveness();
                float price = product.Price;
                EDrugType drugType = product.DrugType;

                weight += addictiveness * 5f;
                weight += price * 0.1f;

                // 2. Cruzar con Perfil de Consumidor (Fase 2)
                if (customer != null)
                {
                    ConsumerProfile profile = ProfileManager.GetOrCreateProfile(customer);
                    if (profile != null)
                    {
                        // Modificador por TIPO DE CONSUMIDOR
                        switch (profile.Type)
                        {
                            case ConsumerType.Classic:
                                // Prefiere drogas simples/puras o baratas. Castiga precios altos y mezclas extremas.
                                if (price > 100f) weight *= 0.5f;
                                break;
                            case ConsumerType.Experimenter:
                                // Premia combinaciones caras y de alta adicción (mezclas complejas)
                                if (price > 100f || addictiveness > 5f) weight *= 2.5f;
                                break;
                            case ConsumerType.Addict:
                                // Solo le importa la adicción.
                                weight += addictiveness * 20f;
                                break;
                            case ConsumerType.Gourmet:
                                // Premia el precio (calidad percibida) e ignora la adicción bruta
                                weight += price * 0.5f;
                                break;
                        }

                        string drugName = drugType.ToString();
                        switch (profile.HomeNeighborhood)
                        {
                            case Neighborhood.Northtown:
                                // Northtown (Munchies, Energizing, Euphoric, Refreshing)
                                if (drugName.Contains("Weed") || drugName.Contains("Amphetamine")) weight *= 1.3f;
                                break;
                            case Neighborhood.Westville:
                                // Westville (Thought-Provoking)
                                if (drugName.Contains("LSD") || drugName.Contains("Shroom")) weight *= 1.4f;
                                break;
                            case Neighborhood.Downtown:
                                // Downtown (Toxic, Shrinking, Sedating)
                                if (drugName.Contains("Cocaine") || drugName.Contains("Heroin")) weight *= 1.5f;
                                break;
                            case Neighborhood.Docks:
                                // Docks (Anti-Gravity, Laxative, Schizophrenic, Refreshing)
                                if (drugName.Contains("Meth") || drugName.Contains("PCP")) weight *= 1.5f;
                                break;
                            case Neighborhood.Suburbia:
                                // Suburbia (Sneaky, Athletic, None)
                                if (drugName.Contains("Weed") || drugName.Contains("Cocaine")) weight *= 1.2f;
                                break;
                            case Neighborhood.Uptown:
                                // Uptown (Schizophrenic, Explosive, Calming)
                                if (drugName.Contains("Heroin") || drugName.Contains("LSD")) weight *= 1.3f;
                                break;
                        }
                    }
                }

                // 3. Ajustes temporales por eventos (Cambio estacional)
                try
                {
                    if (SeasonalChangeManager.IsSeasonalActive)
                    {
                        // Use heuristics to decide si producto es calmante o estimulante
                        string dtype = drugType.ToString().ToLowerInvariant();
                        string pname = "";
                        try { pname = (string)product.GetType().GetProperty("Name")?.GetValue(product) ?? ""; } catch { }
                        pname = pname.ToLowerInvariant();

                        float calmingMult = Core.SmartMarketConfig.Events.cambioEstacional.calmingMultiplier;
                        float stimulantMult = Core.SmartMarketConfig.Events.cambioEstacional.stimulantMultiplier;

                        bool isCalming = dtype.Contains("weed") || dtype.Contains("mushroom") || dtype.Contains("heroin") || pname.Contains("weed") || pname.Contains("mushroom") || pname.Contains("heroin") || pname.Contains("calm") || pname.Contains("sedat");
                        bool isStimulant = dtype.Contains("cocaine") || dtype.Contains("meth") || dtype.Contains("amphetamine") || pname.Contains("cocaine") || pname.Contains("meth") || pname.Contains("speed") || pname.Contains("amphetamine");

                        if (isCalming)
                        {
                            weight *= calmingMult;
                        }
                        else if (isStimulant)
                        {
                            weight *= stimulantMult;
                        }
                    }
                }
                catch { }

            }
            catch
            {
                // Fallback
            }

            return Mathf.Max(weight, 1f);
        }

        /// <summary>
        /// PHASE 1: Check if a product is listed for sale in ProductManager
        /// This ensures NPCs only request products that are marked for sale by the player
        /// </summary>
        private static bool IsProductAcceptingOrders(ProductDefinition product)
        {
            if (product == null)
                return false;

            try
            {
                var productManager = ProductManager.Instance;
                if (productManager == null)
                {
                    MelonLogger.Warning("[SmartMarket] ProductManager.Instance is NULL in IsProductAcceptingOrders");
                    return true; // Fallback: allow if PM unavailable
                }

                // Get the product's SaveFileName (unique identifier)
                string productId = product.SaveFileName ?? product.name ?? "";
                if (string.IsNullOrEmpty(productId))
                    return false;

                // Use reflection to get ListedProducts from ProductManager
                var pmType = productManager.GetType();
                var listedProp = pmType.GetProperty("ListedProducts");
                
                if (listedProp != null)
                {
                    var listedObj = listedProp.GetValue(productManager);
                    if (listedObj is System.Collections.IEnumerable listedList)
                    {
                        foreach (var item in listedList)
                        {
                            if (item is string listedProductId && string.Equals(productId, listedProductId, StringComparison.OrdinalIgnoreCase))
                            {
                                return true; // Product is listed for sale
                            }
                        }
                    }
                }

                // Product not found in ListedProducts
                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] Error checking IsProductAcceptingOrders: {ex.Message}");
                return true; // Fallback: allow on error to not block commerce
            }
        }
    }
}
