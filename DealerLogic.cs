// Estado actual: Define la lógica del dealer para elegir qué vender en base a inventario local o tendencias globales.
// Riesgos si se modifica: Cambiar la selección del dealer puede impactar la economía local y romper expectativas de comportamiento.
// Integración propuesta: Mantener el comportamiento existente; añadir protecciones contra llamadas privadas y usar el DemandEngine para scoring en el futuro.
using Il2CppSystem.Collections.Generic;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Economy;
using MelonLoader;
using MelonLogger = SmartMarket.SmartMarketLogger;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using System;

namespace SmartMarket.Core
{
    public static class DealerLogic
    {
        public static ProductDefinition ApplyPersonality(Customer customer, Dealer dealer)
        {
            float roll = UnityEngine.Random.Range(1f, 100f);

            // Obtenemos el inventario local del dealer
            List<ProductDefinition> localStock = null;
            try
            {
                localStock = customer.GetOrderableProducts(dealer);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] Error llamando a GetOrderableProducts(dealer): {ex.Message}");
                try
                {
                    var method = AccessTools.Method(typeof(Customer), "GetOrderableProducts", new Type[] { typeof(Dealer) });
                    if (method != null)
                    {
                        var res = method.Invoke(customer, new object[] { dealer });
                        localStock = res as List<ProductDefinition>;
                    }
                }
                catch (Exception rex)
                {
                    MelonLogger.Warning($"[SmartMarket] Fallback reflection GetOrderableProducts(dealer) falló: {rex.Message}");
                }
            }
            
            // Obtenemos el inventario global (tendencias del jugador/mercado)
            List<ProductDefinition> globalStock = null;
            try
            {
                globalStock = customer.GetOrderableProducts(null);
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
                        globalStock = res as List<ProductDefinition>;
                    }
                }
                catch (Exception rex)
                {
                    MelonLogger.Warning($"[SmartMarket] Fallback reflection GetOrderableProducts(null) falló: {rex.Message}");
                }
            }

            if (localStock == null) localStock = new List<ProductDefinition>();
            if (globalStock == null) globalStock = new List<ProductDefinition>();

            if (localStock == null || localStock.Count == 0)
            {
                // Si el dealer no tiene nada, intenta usar el global, o devuelve null
                return MarketEngine.RunWeightedLottery(globalStock);
            }

            // Modo Liquidador (70%): Intenta vender lo que tiene en mayor cantidad
            if (roll <= 70f)
            {
                // En un escenario real buscaríamos el stock exacto. 
                // Por ahora usamos la ruleta ponderada sobre su inventario local.
                return MarketEngine.RunWeightedLottery(localStock);
            }
            // Modo Variedad (20%): Vende otra cosa al azar
            else if (roll <= 90f)
            {
                // Selecciona puramente al azar, ignorando pesos/cantidades
                int randomIndex = UnityEngine.Random.Range(0, localStock.Count);
                return localStock[randomIndex];
            }
            // Modo Tendencia (10%): Sigue la demanda global
            else
            {
                if (globalStock != null && globalStock.Count > 0)
                {
                    return MarketEngine.RunWeightedLottery(globalStock);
                }
                return MarketEngine.RunWeightedLottery(localStock); // Fallback
            }
        }
    }
}
