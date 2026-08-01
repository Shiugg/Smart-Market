using System;
using HarmonyLib;
using MelonLoader;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Product;
using SmartMarket.Core; // Assuming ProductClassifier or similar is here

namespace SmartMarket.Patches
{
    [HarmonyPatch(typeof(Customer), nameof(Customer.GetValueProposition))]
    public static class CustomerMarketPatch
    {
        // Multiplicador dinámico para contrarrestar la penalización por afinidad de mezclas
        private const float MIXTURE_BOOST_MULTIPLIER = 1.35f;
        private const float BASE_SCORE_OFFSET = 0.15f;

        [HarmonyPostfix]
        public static void Postfix(ProductDefinition product, float price, ref float __result)
        {
            try
            {
                // 1. Verificación defensiva contra nulos
                if (product == null)
                    return;

                // 2. Comprobar si el producto es una mezcla
                if (ProductClassifier.IsNamedMix(product))
                {
                    // 3. Compensar la penalización de CustomerAffinityData aplicando el bono
                    float originalValue = __result;
                    
                    if (originalValue > 0f)
                    {
                        __result = (originalValue * MIXTURE_BOOST_MULTIPLIER) + BASE_SCORE_OFFSET;
                    }
                    else
                    {
                        // Si la afinidad vanilla dejó el valor en cero o negativo, le asignamos un umbral base mínimo
                        __result = BASE_SCORE_OFFSET;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CustomerMarketPatch] Exception en GetValueProposition Postfix: {ex}");
            }
        }
    }
}