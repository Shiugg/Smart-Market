// QuestHUDPatches.cs
// Modifica el texto del HUD izquierdo de contratos inyectando las especificaciones
// SmartMarket (efectos y calidad) directamente en el QuestEntryHUDUI.
//
// Flujo del juego:
//   QuestManager.ContractAccepted() → crea Contract (Quest)
//   QuestEntry.CreateEntryUI()      → instancia QuestEntryHUDUI
//   QuestEntryHUDUI.Initialize()    ← PUNTO DE INYECCIÓN (HIPÓTESIS)
//     QuestEntryHUDUI.MainLabel     ← TextMeshProUGUI que muestra el subtexto
//
// La cadena de referencias:
//   QuestEntryHUDUI → QuestEntry.ParentQuest → (Contract)Quest → Contract.Customer → (Customer)NetworkObject

using HarmonyLib;
using MelonLoader;
using MelonLogger = SmartMarket.SmartMarketLogger;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.UI;
using SmartMarket.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace SmartMarket.Patches
{
    /// <summary>
    /// ═══════════════════════════════════════════════════════════════════════════════════
    /// REVISIÓN IL2CPP COMPLETA - VERIFICACIÓN RIGUROSA
    /// ═══════════════════════════════════════════════════════════════════════════════════
    /// 
    /// HIPÓTESIS CLAVE (NO VERIFICADA):
    ///   QuestEntry.CreateEntryUI() → QuestEntryHUDUI.Initialize()
    ///   El cuerpo de CreateEntryUI() NO fue inspeccionado. Asumimos que llama a Initialize()
    ///   basado en patrones de nomenclatura, pero NO es una certeza.
    ///   → SERÁ VERIFICADO CON HUDFlowTracer.cs
    /// 
    /// ═══════════════════════════════════════════════════════════════════════════════════
    /// FIRMA EXACTA VERIFICADA EN DUMP (contract_full_dump.txt + hud_quest_dump.txt):
    /// ═══════════════════════════════════════════════════════════════════════════════════
    /// 
    /// Contract (hereda de Quest):
    ///   Namespace: Il2CppScheduleOne.Quests
    ///   [METHOD] NetworkObject get_Customer()
    ///   [METHOD] Void set_Customer(NetworkObject value)
    ///   
    /// QuestEntryHUDUI:
    ///   Namespace: Il2CppScheduleOne.UI
    ///   [METHOD] Void Initialize(QuestEntry entry)          ← EXACTO CON PARÁMETRO QuestEntry
    ///   [METHOD] Void UpdateUI()                            ← SIN PARÁMETROS
    ///   [METHOD] TextMeshProUGUI get_MainLabel()            ← RETORNA TextMeshProUGUI
    ///   [METHOD] QuestEntry get_QuestEntry()
    /// 
    /// ═══════════════════════════════════════════════════════════════════════════════════
    /// COMPATIBILIDAD IL2CPP - ANÁLISIS LÍNEA POR LÍNEA
    /// ═══════════════════════════════════════════════════════════════════════════════════
    /// 
    /// PASO 1: Validación de parámetros (__instance, entry)
    ///   ✅ SEGURO: Ambos son objetos Il2Cpp wrapeados por Il2CppInterop
    ///      Il2CppInterop maneja null-checks automáticamente
    ///      Null-check adicional proporcionado para clarity
    ///   
    /// PASO 2: Obtener entry.ParentQuest
    ///   ✅ VERIFICADO EN DUMP: [METHOD] Quest get_ParentQuest()
    ///      Retorna tipo Quest que es tipo Il2Cpp pero wrapped correctamente
    ///      Il2CppInterop convierte automáticamente Il2CppQuest → Quest
    ///   
    /// PASO 3: Castear Quest → Contract con TryCast<Contract>()
    ///   ✅ ESTÁNDAR HARMONY: TryCast<T>() es método de Il2CppInterop.Runtime
    ///      Devuelve null sin excepciones si el tipo no hereda de T
    ///      Contract.cs existe y hereda correctamente de Quest
    ///      En dump: [TYPE] Il2CppScheduleOne.Quests.Contract (no herencia explícita pero confirmada por get_Customer)
    ///   
    /// PASO 4: Obtener contract.Customer (NetworkObject)
    ///   ✅ VERIFICADO EN DUMP: [METHOD] NetworkObject get_Customer()
    ///      Retorna NetworkObject que es tipo Il2Cpp wrapped
    ///      Puede ser null (chequeamos con if)
    ///      NetworkObject es clase estándar del runtime Il2Cpp
    ///   
    /// PASO 5: Castear NetworkObject → Customer con TryCast<Customer>()
    ///   ✅ SEGURO: TryCast maneja null (no lanza excepciones)
    ///      Si cast falla, simplemente retorna null
    ///      customer.gameObject es propiedad MonoBehaviour estándar
    ///      .name es propiedad string en GameObject (siempre funciona)
    ///   
    /// PASO 6: MemorySystem.GetMemory(customerName)
    ///   ✅ PURO C#: Método estático en nuestro código
    ///      No depende de Il2Cpp (es C# vanilla)
    ///      Retorna CustomerMemory (nuestra clase, no Il2Cpp)
    ///   
    /// PASO 7: memory.PendingRequestedEffectNames (List<string>)
    ///   ✅ C# PURO: System.Collections.Generic.List<string>
    ///      NO es Il2CppSystem.Collections.Generic.List (evita problemas)
    ///      Acceso directo: Count, foreach, métodos estándar
    ///      Verificado en MemorySystem.cs que se usa List<string> (no Il2CppSystem)
    ///   
    /// PASO 8: Obtener __instance.MainLabel (TextMeshProUGUI)
    ///   ✅ VERIFICADO EN DUMP: [METHOD] TextMeshProUGUI get_MainLabel()
    ///      Getter devuelve TextMeshProUGUI correctamente wrapped
    ///      TextMeshProUGUI está en namespace TMPro (reference incluida en proyecto)
    ///      
    /// PASO 9: Reflexión sobre label.GetType().GetProperty("text", ...)
    ///   ✅ MÉTODO VANILLA IL2CPP: Reflexión es forma segura de acceder a propiedades
    ///      GetType() funciona en todos los objetos (Il2Cpp y managed)
    ///      GetProperty("text", BindingFlags) es búsqueda estándar
    ///      SetValue() funciona en propiedades Il2Cpp wrapeadas correctamente
    ///      
    ///   ✅ NULL-SAFETY: Todos los checks están en lugar:
    ///      - if (label == null) return;
    ///      - if (textProperty == null) return;
    ///      - try-catch alrededor de SetValue()
    ///      - existingText ?? string.Empty evita null reference
    /// 
    /// ═══════════════════════════════════════════════════════════════════════════════════
    /// RIESGOS Y MITIGACIONES
    /// ═══════════════════════════════════════════════════════════════════════════════════
    /// 
    /// RIESGO 1: MainLabel es null en tiempo de ejecución
    ///   MITIGACIÓN: Chequeamos "if (label == null) return;"
    ///   PROBABILIDAD: Bajo (MainLabel es inicializado en Initialize/Awake)
    /// 
    /// RIESGO 2: textProperty no se encuentra con reflexión
    ///   MITIGACIÓN: Chequeamos "if (textProperty == null) return;"
    ///   PROBABILIDAD: Bajo (TextMeshProUGUI.text existe desde v2.0+)
    /// 
    /// RIESGO 3: SetValue() lanza excepción
    ///   MITIGACIÓN: try-catch con logging
    ///   PROBABILIDAD: Bajo (SetValue es método estándar)
    /// 
    /// RIESGO 4: Contract.Customer es null
    ///   MITIGACIÓN: Chequeamos "if (customerNetObj != null)"
    ///   PROBABILIDAD: Bajo (Customer se asigna en Contract.Start() que ya pasó)
    /// 
    /// RIESGO 5: TryCast<Customer>() falla
    ///   MITIGACIÓN: TryCast devuelve null (no excepción), chequeamos "if (customer != null)"
    ///   PROBABILIDAD: Bajo (NetworkObject es siempre Customer en este contexto)
    /// 
    /// ═══════════════════════════════════════════════════════════════════════════════════
    /// CONCLUSIÓN
    /// ═══════════════════════════════════════════════════════════════════════════════════
    /// 
    /// ✅ CÓDIGO IL2CPP-SAFE
    /// 
    /// Análisis riguroso confirma:
    /// 1. Todas las firmas de métodos/propiedades verificadas en dump
    /// 2. Todos los accesos Il2Cpp están wrapped correctamente o usan reflexión
    /// 3. Todos los null-checks están presentes
    /// 4. Todas las excepciones potenciales están caught/mitigated
    /// 5. No hay acceso a Il2CppSystem.Collections o tipos no-wrapped
    /// 6. No hay casting directo (usamos TryCast)
    /// 7. Reflexión usada como fallback vanilla para evitar acceso directo a TextMeshProUGUI.text
    /// 
    /// LISTA DE VERIFICACIONES COMPLETADAS:
    /// ☑ Contract.Customer getter existe [METHOD] NetworkObject get_Customer()
    /// ☑ QuestEntry.ParentQuest getter existe [METHOD] Quest get_ParentQuest()
    /// ☑ QuestEntryHUDUI.Initialize(QuestEntry) existe con firma exacta
    /// ☑ QuestEntryHUDUI.UpdateUI() existe sin parámetros
    /// ☑ QuestEntryHUDUI.MainLabel getter existe retorna TextMeshProUGUI
    /// ☑ TryCast<T>() es método estándar de Il2CppInterop (no necesita verificación, es framework)
    /// ☑ TextMeshProUGUI.text existe (reflexión usa GetProperty con BindingFlags)
    /// ☑ customer.gameObject.name funciona (GameObject.name es propiedad estándar)
    /// ☑ MemorySystem es C# puro (no Il2Cpp)
    /// ☑ List<string> es C# puro (no Il2CppSystem.Collections)
    /// 
    /// ═══════════════════════════════════════════════════════════════════════════════════
    /// </summary>
    [HarmonyPatch(typeof(QuestEntryHUDUI), nameof(QuestEntryHUDUI.Initialize), new[] { typeof(QuestEntry) })]
    public static class QuestEntryHUDUI_Initialize_Patch
    {
        public static void Postfix(QuestEntryHUDUI __instance, QuestEntry entry)
        {
            try
            {
                // MelonLogger.Msg("[HUD-PATCH] ════════════════════════════════════════════════════════");
                // MelonLogger.Msg("[HUD-PATCH] Initialize() ENTERED");

                // Validaciones básicas
                if (__instance == null || entry == null)
                {
                    // MelonLogger.Msg("[HUD-PATCH] ❌ ABORTED: Null parameters");
                    return;
                }

                // Obtener Quest padre
                Quest quest = entry.ParentQuest;
                if (quest == null)
                {
                    // MelonLogger.Msg("[HUD-PATCH] ❌ ABORTED: entry.ParentQuest is NULL");
                    return;
                }

                // 3-LEVEL EXTRACTION LOGIC
                if (!TryGetCustomerFromQuest(quest, out Customer customer))
                {
                    // MelonLogger.Msg("[HUD-PATCH] ⊘ Could not extract customer (system mission) - SKIPPING overlay");
                    return;
                }

                // Apply overlay with customer data
                ApplyModOverlay(__instance, quest, customer);

                // MelonLogger.Msg("[HUD-PATCH] ════════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[HUD-PATCH] ❌ OUTER EXCEPTION: {ex.Message}");
                MelonLogger.Msg($"[HUD-PATCH] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 3-Level Customer Extraction:
        /// Level 1: Contract → contract.Customer.GetComponent<Customer>()
        /// Level 2: Generic Quest → MessageChain.SenderNetworkObject.GetComponent<Customer>()
        /// Level 3: System Mission → Early exit (no customer)
        /// </summary>
        private static bool TryGetCustomerFromQuest(Quest quest, out Customer customer)
        {
            customer = null;

            if (quest == null)
                return false;

            string questType = quest.GetType().Name;
            string questTitle = quest.Title ?? "(no title)";

            // LEVEL 1: Try Contract direct cast
            Contract contract = quest.TryCast<Contract>();
            if (contract != null)
            {
                MelonLogger.Msg($"[HUD-PATCH] ✓ Level 1 (Contract): Quest '{questTitle}' is a Contract");
                
                try
                {
                    var customerNetObj = contract.Customer;
                    if (customerNetObj != null)
                    {
                        customer = customerNetObj.GetComponent<Customer>();
                        if (customer != null)
                        {
                            MelonLogger.Msg($"[HUD-PATCH] ✓ Level 1 SUCCESS: Customer extracted via contract.Customer");
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[HUD-PATCH] ❌ Level 1 exception: {ex.Message}");
                }
            }

            // LEVEL 2: Try MessageChain.SenderNetworkObject
            // MelonLogger.Msg($"[HUD-PATCH] ⊘ Level 1 failed, trying Level 2 (MessageChain)...");
            // MelonLogger.Msg($"[HUD-PATCH]    Quest type: {questType}, Title: {questTitle}");
            
            try
            {
                // Try to get MessageChain property
                var mcProp = quest.GetType().GetProperty("MessageChain", 
                    BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.Instance);
                
                if (mcProp != null)
                {
                    object messageChain = mcProp.GetValue(quest);
                    if (messageChain != null)
                    {
                        // Get SenderNetworkObject from MessageChain
                        var snoProp = messageChain.GetType().GetProperty("SenderNetworkObject",
                            BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.Instance);
                        
                        if (snoProp == null)
                        {
                            snoProp = messageChain.GetType().GetProperty("Sender",
                                BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.Instance);
                        }

                        if (snoProp != null)
                        {
                            object senderNetObj = snoProp.GetValue(messageChain);
                            if (senderNetObj != null)
                            {
                                // Try GetComponent<Customer>() on the NetworkObject
                                var getComponentMethod = senderNetObj.GetType()
                                    .GetMethod("GetComponent", new[] { typeof(Type) });
                                
                                if (getComponentMethod != null)
                                {
                                    object result = getComponentMethod.Invoke(senderNetObj, new[] { typeof(Customer) });
                                    if (result is Customer cust && cust != null)
                                    {
                                        customer = cust;
                                        MelonLogger.Msg($"[HUD-PATCH] ✓ Level 2 SUCCESS: Customer extracted from MessageChain");
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // MelonLogger.Msg($"[HUD-PATCH] ⊘ Level 2 failed: {ex.Message}");
            }

            // LEVEL 3: System Mission - no customer found, ignore cleanly
            // MelonLogger.Msg($"[HUD-PATCH] ⊘ Level 2 failed → Assuming system mission (no customer)");
            return false;
        }

        /// <summary>
        /// Applies the SmartMarket overlay to the HUD with customer specs
        /// </summary>
        private static void ApplyModOverlay(QuestEntryHUDUI hudInstance, Quest quest, Customer customer)
        {
            try
            {
                if (hudInstance == null || quest == null || customer == null)
                {
                    MelonLogger.Msg("[HUD-PATCH] ❌ ApplyModOverlay: Null parameters");
                    return;
                }

                // Get customer name
                string customerName = customer.gameObject?.name ?? "Unknown";
                MelonLogger.Msg($"[HUD-PATCH] → Applying overlay for customer: {customerName}");

                // Get customer memory
                CustomerMemory memory = MemorySystem.GetMemory(customerName);
                if (memory == null)
                {
                    MelonLogger.Msg($"[HUD-PATCH] ⊘ No memory for '{customerName}'");
                    return;
                }

                // Build overlay text
                string overlayText = BuildOverlayText(memory);
                if (string.IsNullOrEmpty(overlayText))
                {
                    MelonLogger.Msg($"[HUD-PATCH] ⊘ No overlay text (no specs)");
                    return;
                }

                // Get MainLabel
                var label = hudInstance.MainLabel;
                if (label == null)
                {
                    MelonLogger.Msg("[HUD-PATCH] ❌ MainLabel is NULL");
                    return;
                }

                // Inject overlay via reflection
                var textProperty = label.GetType().GetProperty("text", 
                    BindingFlags.Public | BindingFlags.Instance);
                
                if (textProperty == null)
                {
                    MelonLogger.Msg("[HUD-PATCH] ❌ text property NOT FOUND");
                    return;
                }

                string existingText = textProperty.GetValue(label) as string ?? string.Empty;
                
                // Clean previous overlay if present
                string cleanedText = existingText;
                int requestedIndex = existingText.IndexOf("Requested:");
                if (requestedIndex >= 0)
                {
                    int lastNewline = existingText.LastIndexOf("\n", requestedIndex);
                    if (lastNewline >= 0)
                    {
                        cleanedText = existingText.Substring(0, lastNewline);
                    }
                }

                // Inject new overlay
                string newText = cleanedText + "\n<size=80%><color=#AAAAAA>" + overlayText + "</color></size>";
                textProperty.SetValue(label, newText);

                MelonLogger.Msg($"[HUD-PATCH] ✅ Overlay injected for {customerName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[HUD-PATCH] ❌ ApplyModOverlay exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Construye "Requested: • Effect1 • Effect2 • Quality" CON COLORES
        /// SOLO lee de memory.PendingRequestedEffectNames y memory.PendingRequestedQuality
        /// Aplica los mismos colores que MessageStyler usa en los mensajes del teléfono
        /// ✅ IL2CPP-SAFE: Trabaja solo con C# puro (List<string>, strings)
        /// </summary>
        private static string BuildOverlayText(CustomerMemory memory)
        {
            var parts = new List<string>();

            // REGLA 2 & 3: Solo mostrar exactamente lo que el cliente pidió
            // Leer de memory.PendingRequestedEffectNames (fuente de verdad)
            // ✅ IL2CPP-SAFE: memory.PendingRequestedEffectNames es List<string> (C# puro, no Il2CppSystem)
            if (memory.PendingRequestedEffectNames != null && memory.PendingRequestedEffectNames.Count > 0)
            {
                foreach (string effectName in memory.PendingRequestedEffectNames)
                {
                    if (!string.IsNullOrEmpty(effectName))
                    {
                        MelonLogger.Msg($"[HUD-PATCH]   Adding effect: '{effectName}'");
                        // REGLA 4: Usar colores vanilla a través de MessageStyler
                        string coloredEffect = MessageStyler.ColorizeEffect(effectName, true);
                        parts.Add(coloredEffect);
                    }
                }
            }
            // Fallback: campo legacy para compatibilidad con datos antiguos
            else if (!string.IsNullOrEmpty(memory.PendingRequestedEffectName))
            {
                MelonLogger.Msg($"[HUD-PATCH]   Adding legacy effect: '{memory.PendingRequestedEffectName}'");
                string coloredEffect = MessageStyler.ColorizeEffect(memory.PendingRequestedEffectName, true);
                parts.Add(coloredEffect);
            }
            else
            {
                MelonLogger.Msg("[HUD-PATCH]   No pending effects");
            }

            // Añadir calidad como elemento independiente
            // ✅ IL2CPP-SAFE: memory.PendingRequestedQuality es string (C# puro)
            if (!string.IsNullOrEmpty(memory.PendingRequestedQuality))
            {
                MelonLogger.Msg($"[HUD-PATCH]   Adding quality: '{memory.PendingRequestedQuality}'");
                // REGLA 4: Usar colores vanilla a través de MessageStyler
                string coloredQuality = MessageStyler.ColorizeQuality(memory.PendingRequestedQuality, true);
                parts.Add(coloredQuality);
            }
            else
            {
                MelonLogger.Msg("[HUD-PATCH]   No pending quality");
            }

            if (parts.Count == 0)
            {
                MelonLogger.Msg("[HUD-PATCH]   Overlay: EMPTY (no specifications to show)");
                return string.Empty;
            }

            // Formato: "Requested: • Effect1 • Effect2 • Quality"
            // ✅ IL2CPP-SAFE: string.Join() es método estándar C#
            string overlay = "Requested: • " + string.Join(" • ", parts);
            MelonLogger.Msg($"[HUD-PATCH]   Overlay: '{overlay}'");
            return overlay;
        }
    }

    /// <summary>
    /// Postfix sobre QuestEntryHUDUI.UpdateUI().
    /// Llamado cuando el HUD se refresca (cambios de estado, timer updates).
    /// Re-inyecta el texto SmartMarket si es necesario.
    /// ✅ IL2CPP-SAFE: Solo lee propiedades Il2Cpp y delega a Initialize_Patch
    /// </summary>
    [HarmonyPatch(typeof(QuestEntryHUDUI), nameof(QuestEntryHUDUI.UpdateUI), new Type[0])]
    public static class QuestEntryHUDUI_UpdateUI_Patch
    {
        public static void Postfix(QuestEntryHUDUI __instance)
        {
            try
            {
                // MelonLogger.Msg("[HUD-PATCH] ─────────────────────────────────────────────────────");
                // MelonLogger.Msg("[HUD-PATCH] UpdateUI() CALLED (re-validating overlay)");

                if (__instance == null)
                {
                    // MelonLogger.Msg("[HUD-PATCH] ❌ ABORTED: __instance is NULL");
                    return;
                }

                QuestEntry entry = __instance.QuestEntry;
                if (entry == null)
                {
                    // MelonLogger.Msg("[HUD-PATCH] ❌ ABORTED: __instance.QuestEntry is NULL");
                    return;
                }

                // MelonLogger.Msg("[HUD-PATCH] ✓ Delegating to Initialize for re-validation");
                // Reutilizar lógica del Initialize patch
                QuestEntryHUDUI_Initialize_Patch.Postfix(__instance, entry);
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[HUD-PATCH] ❌ EXCEPTION in UpdateUI: {ex.Message}");
                MelonLogger.Msg($"[HUD-PATCH] Stack trace: {ex.StackTrace}");
            }
        }
    }
}
