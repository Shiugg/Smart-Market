using System;
using HarmonyLib;
using Il2CppInterop.Runtime;
using MelonLoader;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework; // Namespace para IItemSlotOwner / ItemSlot
using Il2CppScheduleOne.Quests; // Namespace para ContractInfo
using SmartMarket.Core;

namespace SmartMarket.Patches
{
    [HarmonyPatch(typeof(Dealer), nameof(Dealer.ShouldAcceptContract))]
    public static class DealerAutonomyPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Dealer __instance, ContractInfo info, Customer customer, ref bool __result)
        {
            try
            {
                // 1. Verificación de nulos explícita
                if (__instance == null || info == null)
                {
                    return true; // Permitir que ejecute el método original
                }

                // 2. Cast seguro vía Il2CppInterop .TryCast<T>()
                var slotOwner = __instance.TryCast<IItemSlotOwner>();
                if (slotOwner == null)
                {
                    return true;
                }

                // 3. Inspeccionar inventario local/slots del Dealer
                var itemSlots = slotOwner.ItemSlots;
                if (itemSlots == null)
                {
                    return true;
                }

                bool hasLocalStock = false;
                
                // Extraer el ID del producto y cantidad solicitada usando la misma lógica que ContractPatches
                string reqProductId = "";
                int reqQuantity = 0;
                
                if (info != null)
                {
                    var productsProp = info.GetType().GetProperty("Products");
                    if (productsProp != null)
                    {
                        var productsVal = productsProp.GetValue(info);
                        if (productsVal != null)
                        {
                            var entriesField = productsVal.GetType().GetField("entries");
                            if (entriesField != null)
                            {
                                var entriesVal = entriesField.GetValue(productsVal) as System.Collections.IList;
                                if (entriesVal != null && entriesVal.Count > 0)
                                {
                                    var entry = entriesVal[0];
                                    var idProp = entry.GetType().GetProperty("ProductID");
                                    var qtyProp = entry.GetType().GetProperty("Quantity");
                                    
                                    if (idProp != null) reqProductId = idProp.GetValue(entry) as string;
                                    if (qtyProp != null) reqQuantity = (int)qtyProp.GetValue(entry);
                                }
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(reqProductId)) return true;

                // Recorremos los slots buscando coincidencia de producto e id/cantidad
                foreach (var slot in itemSlots)
                {
                    if (slot != null)
                    {
                        try 
                        {
                            // Reflection for ItemSlot properties
                            var slotType = slot.GetType();
                            var isEmptyProp = slotType.GetProperty("IsEmpty") ?? slotType.GetProperty("isEmpty");
                            bool isEmpty = false;
                            if (isEmptyProp != null) isEmpty = (bool)isEmptyProp.GetValue(slot);
                            
                            if (!isEmpty)
                            {
                                var itemProp = slotType.GetProperty("Item") ?? slotType.GetProperty("StoredInstance");
                                if (itemProp != null)
                                {
                                    var itemInstance = itemProp.GetValue(slot);
                                    if (itemInstance != null)
                                    {
                                        var defProp = itemInstance.GetType().GetProperty("Definition");
                                        if (defProp != null)
                                        {
                                            var def = defProp.GetValue(itemInstance);
                                            if (def != null)
                                            {
                                                var idProp = def.GetType().GetProperty("ProductID") ?? def.GetType().GetProperty("ID");
                                                if (idProp != null)
                                                {
                                                    string defId = idProp.GetValue(def)?.ToString();
                                                    
                                                    var qtyProp = slotType.GetProperty("Quantity") ?? slotType.GetProperty("Amount");
                                                    int slotQty = 0;
                                                    if (qtyProp != null) slotQty = (int)qtyProp.GetValue(slot);
                                                    
                                                    if (defId == reqProductId && slotQty >= reqQuantity)
                                                    {
                                                        hasLocalStock = true;
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }

                // 4. Si tiene el stock físico localmente, se intercepta la validación global de ProductManager
                if (hasLocalStock)
                {
                    __result = true;
                    return false; // Salta la ejecución del método Vanilla
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[DealerAutonomyPatch] Exception en ShouldAcceptContract Prefix: {ex}");
            }

            // 5. En caso de no tener stock o si ocurre algún fallo, se deja continuar el comportamiento normal
            return true;
        }
    }
}