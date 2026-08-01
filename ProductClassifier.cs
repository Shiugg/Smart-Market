// ProductClassifier.cs
// Distingue entre productos base (que pueden tener especificaciones) y mezclas con nombre propio (que no)
// BASADO 100% EN LÓGICA VANILLA: usa ProductManager.DefaultKnownProducts como fuente de verdad
// NO CONTIENE LISTAS HARDCODEADAS

using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Product;

namespace SmartMarket.Core
{
    public static class ProductClassifier
    {
        /// <summary>
        /// Determina si un ProductDefinition es un producto base (vanilla) o una mezcla creada por el jugador
        /// 
        /// LÓGICA VANILLA:
        /// - Productos base: están en ProductManager.DefaultKnownProducts
        /// - Mezclas con nombre propio: NO están en esa lista (fueron creadas por FinishAndNameMix())
        /// 
        /// VALIDACIÓN POR ID INTERNO: Comparamos por ProductID (inmutable) en lugar de DisplayName (que el usuario puede cambiar)
        /// Esto evita falsos positivos si alguien renombra un producto base.
        /// </summary>
        public static bool IsBaseProduct(Il2CppScheduleOne.Product.ProductDefinition product)
        {
            if (product == null)
                return false;

            try
            {
                // Obtener la lista vanilla de productos conocidos del juego
                var productManager = Il2CppScheduleOne.Product.ProductManager.Instance;
                if (productManager == null)
                {
                    SmartMarketConfig.LogDebug("[ProductClassifier] ProductManager.Instance es NULL");
                    return false;
                }

                var defaultProducts = productManager.DefaultKnownProducts;
                if (defaultProducts == null || defaultProducts.Count == 0)
                {
                    SmartMarketConfig.LogDebug("[ProductClassifier] ProductManager.DefaultKnownProducts es NULL o vacío");
                    return false;
                }

                // ESTRATEGIA: Comparar por ProductID (ID interno inmutable) en lugar de nombres que pueden cambiar
                // Obtener ProductID del producto a verificar
                int? productID = GetProductID(product);
                if (!productID.HasValue)
                {
                    SmartMarketConfig.LogDebug($"[ProductClassifier] No se pudo obtener ProductID de {product.name ?? "unknown"}");
                    return false;
                }

                // DEBUGGING: Log lista de productos en DefaultKnownProducts (solo primera vez)
                if (_debuggedDefaultProducts == false)
                {
                    _debuggedDefaultProducts = true;
                    SmartMarketConfig.LogDebug($"[ProductClassifier] ════════════════════════════════════════════");
                    SmartMarketConfig.LogDebug($"[ProductClassifier] CONTENIDO DE DefaultKnownProducts: {defaultProducts.Count} productos");
                    foreach (var prod in defaultProducts)
                    {
                        if (prod != null)
                        {
                            int? baseID = GetProductID(prod);
                            string displayName = prod.name ?? "???";
                            string prodIdStr = baseID.HasValue ? baseID.ToString() : "NO_ID";
                            SmartMarketConfig.LogDebug($"[ProductClassifier]   - ID:{prodIdStr} Name:{displayName}");
                        }
                    }
                    SmartMarketConfig.LogDebug($"[ProductClassifier] ════════════════════════════════════════════");
                }

                // Buscar en la lista vanilla comparando por ProductID
                foreach (var baseProduct in defaultProducts)
                {
                    if (baseProduct == null)
                        continue;

                    int? baseProductID = GetProductID(baseProduct);
                    if (baseProductID.HasValue && baseProductID.Value == productID.Value)
                    {
                        // ENCONTRADO: Este producto está en la lista vanilla de productos base
                        SmartMarketConfig.LogDebug($"[ProductClassifier] ProductID={productID} = PRODUCTO BASE (en DefaultKnownProducts)");
                        return true;
                    }
                }

                // NO ENCONTRADO: Este producto NO está en DefaultKnownProducts, entonces es una mezcla
                SmartMarketConfig.LogDebug($"[ProductClassifier] ProductID={productID} = MEZCLA (NO en DefaultKnownProducts)");
                return false;
            }
            catch (Exception ex)
            {
                SmartMarketConfig.LogDebug($"[ProductClassifier] Error al verificar IsBaseProduct: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Obtiene el ProductID de un ProductDefinition de forma segura
        /// Intenta múltiples estrategias para extraer el ID interno
        /// </summary>
        private static int? GetProductID(ProductDefinition product)
        {
            if (product == null)
                return null;

            try
            {
                // Estrategia 1: Propiedad ProductID directa (más probable)
                var productIDProp = product.GetType().GetProperty("ProductID", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (productIDProp != null)
                {
                    object value = productIDProp.GetValue(product);
                    if (value is int intValue)
                        return intValue;
                }

                // Estrategia 2: Propiedad ID
                var idProp = product.GetType().GetProperty("ID",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (idProp != null)
                {
                    object value = idProp.GetValue(product);
                    if (value is int intValue)
                        return intValue;
                }

                // Estrategia 3: Campo SaveFileName puede contener un hash/ID (fallback)
                // pero preferimos no usarlo para evitar falsos positivos
            }
            catch (Exception ex)
            {
                SmartMarketConfig.LogDebug($"[ProductClassifier] Error en GetProductID: {ex.Message}");
            }

            return null;
        }

        // Flag para loguear DefaultKnownProducts solo una vez
        private static bool _debuggedDefaultProducts = false;

        /// <summary>
        /// Determina si un ProductDefinition es una mezcla con nombre propio (creada por el jugador)
        /// </summary>
        public static bool IsNamedMix(Il2CppScheduleOne.Product.ProductDefinition product)
        {
            return product != null && !IsBaseProduct(product);
        }

        /// <summary>
        /// LEGACY: Versión que recibe nombre de producto como string (para uso donde aún no tenemos ProductDefinition)
        /// NOTA: Esta versión es fallback y menos confiable que IsBaseProduct(ProductDefinition).
        /// Intenta buscar por nombre pero también por ProductID si es posible.
        /// </summary>
        public static bool IsBaseProductByName(string productName)
        {
            if (string.IsNullOrEmpty(productName))
                return false;

            try
            {
                var productManager = Il2CppScheduleOne.Product.ProductManager.Instance;
                if (productManager == null || productManager.DefaultKnownProducts == null)
                    return false;

                // Búsqueda por nombre (fallback si no se encuentra por ID)
                foreach (var baseProduct in productManager.DefaultKnownProducts)
                {
                    if (baseProduct == null)
                        continue;

                    // Intenta coincidir por nombre exacto (case-insensitive)
                    if (string.Equals(baseProduct.name, productName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    // Intenta coincidir por SaveFileName
                    if (!string.IsNullOrEmpty(baseProduct.SaveFileName) &&
                        string.Equals(baseProduct.SaveFileName, productName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                SmartMarketConfig.LogDebug($"[ProductClassifier] Error en IsBaseProductByName: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Valida si es apropiado que un contrato tenga especificaciones (efectos/calidades)
        /// Solo productos base pueden tener RequestedEffect o RequestedQuality no vacío
        /// </summary>
        public static bool ShouldHaveSpecifications(Il2CppScheduleOne.Product.ProductDefinition product, string requestedEffect, string requestedQuality)
        {
            // Si es una mezcla con nombre propio, NO debe tener especificaciones
            if (IsNamedMix(product))
            {
                // Si tiene especificaciones extra, esto es incorrecto
                if (!string.IsNullOrEmpty(requestedEffect) || !string.IsNullOrEmpty(requestedQuality))
                    return false;

                // Una mezcla sin especificaciones extra es válida
                return true;
            }

            // Los productos base siempre pueden tener especificaciones (aunque estén vacías)
            return true;
        }

        /// <summary>
        /// Filtra especificaciones para mezclas con nombre propio
        /// Si es una mezcla, devuelve null/empty para efecto y calidad
        /// Si es base, devuelve los valores originales
        /// </summary>
        public static (string effect, string quality) FilterSpecifications(Il2CppScheduleOne.Product.ProductDefinition product, string requestedEffect, string requestedQuality)
        {
            if (IsNamedMix(product))
            {
                // Las mezclas no deben mostrar especificaciones adicionales
                return (null, null);
            }

            // Los productos base muestran sus especificaciones normalmente
            return (requestedEffect, requestedQuality);
        }
    }
}
