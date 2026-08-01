// HarmonyPatcher.cs
// REGISTRO INDIVIDUAL DE PATCHES - DIAGNÓSTICO DETALLADO
// Registra cada patch uno por uno para identificar exactamente cuál falla

using HarmonyLib;
using MelonLoader;
using System;
using System.Reflection;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.Quests;

namespace SmartMarket.Patches
{
    /// <summary>
    /// Registro individual de patches con diagnóstico completo.
    /// </summary>
    public static class HarmonyPatcher
    {
        private static HarmonyLib.Harmony _harmonyInstance = null;
        private const string HarmonyId = "com.smartmarket.patches";

        /// <summary>
        /// Llamado automáticamente por MelonLoader durante inicialización
        /// </summary>
        public static void Initialize()
        {
            try
            {
                MelonLogger.Msg("[HARMONY-INIT] ════════════════════════════════════════════════════════");
                MelonLogger.Msg("[HARMONY-INIT] Inicializando sistema de patches Harmony...");
                MelonLogger.Msg("[HARMONY-INIT] Estrategia: Registro individual de cada patch");

                // Crear instancia de Harmony
                if (_harmonyInstance == null)
                {
                    _harmonyInstance = new HarmonyLib.Harmony(HarmonyId);
                    MelonLogger.Msg($"[HARMONY-INIT] ✓ Instancia Harmony creada: {HarmonyId}");
                }

                // VERIFICACIÓN PREVIA: Confirmar que los métodos objetivo existen
                VerifyTargetMethodsExist();

                // REGISTRO INDIVIDUAL DE CADA PATCH
                MelonLogger.Msg("[HARMONY-INIT] ────────────────────────────────────────────────────────");
                MelonLogger.Msg("[HARMONY-INIT] Registrando patches individualmente...");

                // Patch 0: ContractPatches + HandoverPatches - Auto-register via [HarmonyPatch] attributes
                MelonLogger.Msg("[HARMONY-INIT] ▶ Registrando: Customer patches (ContractPatches) + SMS interception (HandoverPatches)");
                _harmonyInstance.PatchAll(typeof(Customer_NotifyPlayer_Patch).Assembly);
                MelonLogger.Msg($"[HARMONY-INIT] ✓ PatchAll completado - Debería incluir ContractPatches + HandoverPatches");

                // Patch 1: QuestEntryHUDUI.Initialize(QuestEntry)
                RegisterPatch_QuestEntryHUDUI_Initialize();

                // Patch 2: QuestEntryHUDUI.UpdateUI()
                RegisterPatch_QuestEntryHUDUI_UpdateUI();

                MelonLogger.Msg("[HARMONY-INIT] ────────────────────────────────────────────────────────");
                MelonLogger.Msg("[HARMONY-INIT] ✓ Todos los patches registrados exitosamente");

                // POST-VERIFICATION
                VerifyPatchesApplied();

                MelonLogger.Msg("[HARMONY-INIT] ════════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[HARMONY-INIT] ════════════════════════════════════════════════════════");
                MelonLogger.Error($"[HARMONY-INIT] ❌ ERROR CRÍTICO durante inicialización");
                MelonLogger.Error($"[HARMONY-INIT] Type: {ex.GetType().FullName}");
                MelonLogger.Error($"[HARMONY-INIT] Message: {ex.Message}");
                MelonLogger.Error($"[HARMONY-INIT] Stack trace: {ex.StackTrace}");
                
                // Capturar excepción interna si existe
                Exception current = ex;
                int level = 1;
                while (current.InnerException != null)
                {
                    current = current.InnerException;
                    MelonLogger.Error($"[HARMONY-INIT] ┌─ InnerException (Nivel {level}):");
                    MelonLogger.Error($"[HARMONY-INIT] ├─ Type: {current.GetType().FullName}");
                    MelonLogger.Error($"[HARMONY-INIT] ├─ Message: {current.Message}");
                    MelonLogger.Error($"[HARMONY-INIT] └─ Stack: {current.StackTrace}");
                    level++;
                }
                
                MelonLogger.Error("[HARMONY-INIT] ════════════════════════════════════════════════════════");
            }
        }

        /// <summary>
        /// Verifica que los métodos objetivo existan ANTES de intentar patchearlos
        /// </summary>
        private static void VerifyTargetMethodsExist()
        {
            try
            {
                MelonLogger.Msg("[HARMONY-VERIFY] ════════════════════════════════════════════════════════");
                MelonLogger.Msg("[HARMONY-VERIFY] VERIFICACIÓN PREVIA DE MÉTODOS OBJETIVO");

                // Verificar QuestEntryHUDUI.Initialize(QuestEntry)
                var questEntryHUDUIType = typeof(QuestEntryHUDUI);
                MelonLogger.Msg($"[HARMONY-VERIFY] Buscando tipo: {questEntryHUDUIType.FullName}");

                var initializeMethod = AccessTools.Method(typeof(QuestEntryHUDUI), "Initialize", new[] { typeof(QuestEntry) });
                if (initializeMethod == null)
                {
                    MelonLogger.Error("[HARMONY-VERIFY] ❌ QuestEntryHUDUI.Initialize(QuestEntry) NO ENCONTRADO");
                    MelonLogger.Error("[HARMONY-VERIFY] Métodos disponibles en QuestEntryHUDUI:");
                    
                    var allMethods = questEntryHUDUIType.GetMethods(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static
                    );
                    
                    foreach (var method in allMethods)
                    {
                        var parameters = method.GetParameters();
                        string paramStr = string.Join(", ", Array.ConvertAll(parameters, p => $"{p.ParameterType.Name}"));
                        MelonLogger.Msg($"[HARMONY-VERIFY]   - {method.ReturnType.Name} {method.Name}({paramStr})");
                    }
                }
                else
                {
                    MelonLogger.Msg("[HARMONY-VERIFY] ✓ QuestEntryHUDUI.Initialize(QuestEntry) ENCONTRADO");
                }

                // Verificar QuestEntryHUDUI.UpdateUI()
                var updateUIMethod = AccessTools.Method(typeof(QuestEntryHUDUI), "UpdateUI", new Type[0]);
                if (updateUIMethod == null)
                {
                    MelonLogger.Error("[HARMONY-VERIFY] ❌ QuestEntryHUDUI.UpdateUI() NO ENCONTRADO");
                }
                else
                {
                    MelonLogger.Msg("[HARMONY-VERIFY] ✓ QuestEntryHUDUI.UpdateUI() ENCONTRADO");
                }

                MelonLogger.Msg("[HARMONY-VERIFY] ════════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[HARMONY-VERIFY] Error durante verificación previa: {ex.Message}");
                MelonLogger.Error($"[HARMONY-VERIFY] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Registra el patch de QuestEntryHUDUI.Initialize(QuestEntry) individualmente
        /// </summary>
        private static void RegisterPatch_QuestEntryHUDUI_Initialize()
        {
            try
            {
                MelonLogger.Msg("[HARMONY-INIT] ▶ Registrando: QuestEntryHUDUI.Initialize(QuestEntry)");

                // PASO 1: Buscar el método objetivo
                var targetMethod = AccessTools.Method(typeof(QuestEntryHUDUI), "Initialize", new[] { typeof(QuestEntry) });
                if (targetMethod == null)
                {
                    MelonLogger.Error("[HARMONY-INIT]   ❌ PASO 1 FALLÓ: Método no encontrado");
                    MelonLogger.Error("[HARMONY-INIT]   Intenté buscar: void QuestEntryHUDUI.Initialize(QuestEntry)");
                    return;
                }
                MelonLogger.Msg("[HARMONY-INIT]   ✓ PASO 1: Método objetivo encontrado");

                // PASO 2: Buscar el método Postfix en la clase patch
                var patchType = typeof(QuestEntryHUDUI_Initialize_Patch);
                var postfixMethod = AccessTools.Method(patchType, "Postfix");

                if (postfixMethod == null)
                {
                    MelonLogger.Error("[HARMONY-INIT]   ❌ PASO 2 FALLÓ: Postfix method no encontrado");
                    MelonLogger.Error($"[HARMONY-INIT]   Clase patch: {patchType.FullName}");
                    MelonLogger.Error("[HARMONY-INIT]   Métodos disponibles en clase patch:");
                    foreach (var method in patchType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        var parameters = method.GetParameters();
                        string paramStr = string.Join(", ", Array.ConvertAll(parameters, p => $"{p.ParameterType.Name}"));
                        MelonLogger.Error($"[HARMONY-INIT]     - {method.ReturnType.Name} {method.Name}({paramStr})");
                    }
                    return;
                }
                MelonLogger.Msg("[HARMONY-INIT]   ✓ PASO 2: Postfix method encontrado");

                // PASO 3: Registrar el patch mediante CreateProcessor
                var harmony = _harmonyInstance;
                var patchProcessor = harmony.CreateProcessor(targetMethod);
                patchProcessor.AddPostfix(postfixMethod);
                
                MelonLogger.Msg("[HARMONY-INIT]   ▶ PASO 3: Aplicando patch mediante Harmony.Patch()...");
                patchProcessor.Patch();

                MelonLogger.Msg("[HARMONY-INIT] ✅ QuestEntryHUDUI.Initialize(QuestEntry) registrado exitosamente");
            }
            catch (HarmonyException hex)
            {
                MelonLogger.Error("[HARMONY-INIT] ❌ HarmonyException registrando Initialize:");
                MelonLogger.Error($"[HARMONY-INIT]    Type: {hex.GetType().FullName}");
                MelonLogger.Error($"[HARMONY-INIT]    Message: {hex.Message}");
                MelonLogger.Error($"[HARMONY-INIT]    Stack: {hex.StackTrace}");
                
                // Capturar información detallada de InnerException
                if (hex.InnerException != null)
                {
                    MelonLogger.Error($"[HARMONY-INIT]    ┌─ InnerException (Nivel 1):");
                    MelonLogger.Error($"[HARMONY-INIT]    ├─ Type: {hex.InnerException.GetType().FullName}");
                    MelonLogger.Error($"[HARMONY-INIT]    ├─ Message: {hex.InnerException.Message}");
                    MelonLogger.Error($"[HARMONY-INIT]    └─ Stack: {hex.InnerException.StackTrace}");
                    
                    // Capturar excepción anidada si existe
                    if (hex.InnerException.InnerException != null)
                    {
                        MelonLogger.Error($"[HARMONY-INIT]    ┌─ InnerException (Nivel 2):");
                        MelonLogger.Error($"[HARMONY-INIT]    ├─ Type: {hex.InnerException.InnerException.GetType().FullName}");
                        MelonLogger.Error($"[HARMONY-INIT]    ├─ Message: {hex.InnerException.InnerException.Message}");
                        MelonLogger.Error($"[HARMONY-INIT]    └─ Stack: {hex.InnerException.InnerException.StackTrace}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[HARMONY-INIT] ❌ Exception registrando Initialize:");
                MelonLogger.Error($"[HARMONY-INIT]    Type: {ex.GetType().FullName}");
                MelonLogger.Error($"[HARMONY-INIT]    Message: {ex.Message}");
                MelonLogger.Error($"[HARMONY-INIT]    Stack: {ex.StackTrace}");
                
                // Capturar información detallada de InnerException
                if (ex.InnerException != null)
                {
                    MelonLogger.Error($"[HARMONY-INIT]    ┌─ InnerException:");
                    MelonLogger.Error($"[HARMONY-INIT]    ├─ Type: {ex.InnerException.GetType().FullName}");
                    MelonLogger.Error($"[HARMONY-INIT]    ├─ Message: {ex.InnerException.Message}");
                    MelonLogger.Error($"[HARMONY-INIT]    └─ Stack: {ex.InnerException.StackTrace}");
                }
            }
        }

        /// <summary>
        /// Registra el patch de QuestEntryHUDUI.UpdateUI() individualmente
        /// </summary>
        private static void RegisterPatch_QuestEntryHUDUI_UpdateUI()
        {
            try
            {
                MelonLogger.Msg("[HARMONY-INIT] ▶ Registrando: QuestEntryHUDUI.UpdateUI()");

                // PASO 1: Buscar el método objetivo
                var targetMethod = AccessTools.Method(typeof(QuestEntryHUDUI), "UpdateUI", new Type[0]);
                if (targetMethod == null)
                {
                    MelonLogger.Error("[HARMONY-INIT]   ❌ PASO 1 FALLÓ: Método no encontrado");
                    MelonLogger.Error("[HARMONY-INIT]   Intenté buscar: void QuestEntryHUDUI.UpdateUI()");
                    return;
                }
                MelonLogger.Msg("[HARMONY-INIT]   ✓ PASO 1: Método objetivo encontrado");

                // PASO 2: Buscar el método Postfix en la clase patch
                var patchType = typeof(QuestEntryHUDUI_UpdateUI_Patch);
                var postfixMethod = AccessTools.Method(patchType, "Postfix");

                if (postfixMethod == null)
                {
                    MelonLogger.Error("[HARMONY-INIT]   ❌ PASO 2 FALLÓ: Postfix method no encontrado");
                    MelonLogger.Error($"[HARMONY-INIT]   Clase patch: {patchType.FullName}");
                    MelonLogger.Error("[HARMONY-INIT]   Métodos disponibles en clase patch:");
                    foreach (var method in patchType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        var parameters = method.GetParameters();
                        string paramStr = string.Join(", ", Array.ConvertAll(parameters, p => $"{p.ParameterType.Name}"));
                        MelonLogger.Error($"[HARMONY-INIT]     - {method.ReturnType.Name} {method.Name}({paramStr})");
                    }
                    return;
                }
                MelonLogger.Msg("[HARMONY-INIT]   ✓ PASO 2: Postfix method encontrado");

                // PASO 3: Registrar el patch mediante CreateProcessor
                var harmony = _harmonyInstance;
                var patchProcessor = harmony.CreateProcessor(targetMethod);
                patchProcessor.AddPostfix(postfixMethod);
                
                MelonLogger.Msg("[HARMONY-INIT]   ▶ PASO 3: Aplicando patch mediante Harmony.Patch()...");
                patchProcessor.Patch();

                MelonLogger.Msg("[HARMONY-INIT] ✅ QuestEntryHUDUI.UpdateUI() registrado exitosamente");
            }
            catch (HarmonyException hex)
            {
                MelonLogger.Error("[HARMONY-INIT] ❌ HarmonyException registrando UpdateUI:");
                MelonLogger.Error($"[HARMONY-INIT]    Type: {hex.GetType().FullName}");
                MelonLogger.Error($"[HARMONY-INIT]    Message: {hex.Message}");
                MelonLogger.Error($"[HARMONY-INIT]    Stack: {hex.StackTrace}");
                
                // Capturar información detallada de InnerException
                if (hex.InnerException != null)
                {
                    MelonLogger.Error($"[HARMONY-INIT]    ┌─ InnerException (Nivel 1):");
                    MelonLogger.Error($"[HARMONY-INIT]    ├─ Type: {hex.InnerException.GetType().FullName}");
                    MelonLogger.Error($"[HARMONY-INIT]    ├─ Message: {hex.InnerException.Message}");
                    MelonLogger.Error($"[HARMONY-INIT]    └─ Stack: {hex.InnerException.StackTrace}");
                    
                    // Capturar excepción anidada si existe
                    if (hex.InnerException.InnerException != null)
                    {
                        MelonLogger.Error($"[HARMONY-INIT]    ┌─ InnerException (Nivel 2):");
                        MelonLogger.Error($"[HARMONY-INIT]    ├─ Type: {hex.InnerException.InnerException.GetType().FullName}");
                        MelonLogger.Error($"[HARMONY-INIT]    ├─ Message: {hex.InnerException.InnerException.Message}");
                        MelonLogger.Error($"[HARMONY-INIT]    └─ Stack: {hex.InnerException.InnerException.StackTrace}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[HARMONY-INIT] ❌ Exception registrando UpdateUI:");
                MelonLogger.Error($"[HARMONY-INIT]    Type: {ex.GetType().FullName}");
                MelonLogger.Error($"[HARMONY-INIT]    Message: {ex.Message}");
                MelonLogger.Error($"[HARMONY-INIT]    Stack: {ex.StackTrace}");
                
                // Capturar información detallada de InnerException
                if (ex.InnerException != null)
                {
                    MelonLogger.Error($"[HARMONY-INIT]    ┌─ InnerException:");
                    MelonLogger.Error($"[HARMONY-INIT]    ├─ Type: {ex.InnerException.GetType().FullName}");
                    MelonLogger.Error($"[HARMONY-INIT]    ├─ Message: {ex.InnerException.Message}");
                    MelonLogger.Error($"[HARMONY-INIT]    └─ Stack: {ex.InnerException.StackTrace}");
                }
            }
        }

        /// <summary>
        /// VERIFICACIÓN PREVIA: Confirma que los métodos objetivo existen antes de aplicar patches
        /// </summary>
        private static void VerifyTargetMethods()
        {
            try
            {
                MelonLogger.Msg("[HARMONY-VERIFY] ===== VERIFICANDO MÉTODOS OBJETIVO =====");

                // Verificar QuestEntryHUDUI.Initialize(QuestEntry)
                var questEntryHUDUIType = typeof(QuestEntryHUDUI);
                MelonLogger.Msg($"[HARMONY-VERIFY] Tipo encontrado: {questEntryHUDUIType.FullName}");
                MelonLogger.Msg($"[HARMONY-VERIFY] Assembly: {questEntryHUDUIType.Assembly.GetName().Name}");

                // Usar AccessTools para obtener el método exacto
                var initializeMethod = AccessTools.Method(typeof(QuestEntryHUDUI), "Initialize", new[] { typeof(QuestEntry) });
                
                if (initializeMethod == null)
                {
                    MelonLogger.Error("[HARMONY-VERIFY] ❌ QuestEntryHUDUI.Initialize(QuestEntry) NO ENCONTRADO");
                    MelonLogger.Error("[HARMONY-VERIFY] Listando TODOS los métodos de QuestEntryHUDUI:");
                    
                    var allMethods = questEntryHUDUIType.GetMethods(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static
                    );
                    
                    foreach (var method in allMethods)
                    {
                        var parameters = method.GetParameters();
                        string paramStr = string.Join(", ", Array.ConvertAll(parameters, p => $"{p.ParameterType.Name}"));
                        MelonLogger.Msg($"[HARMONY-VERIFY]   - {method.ReturnType.Name} {method.Name}({paramStr})");
                    }
                }
                else
                {
                    MelonLogger.Msg($"[HARMONY-VERIFY] ✓ QuestEntryHUDUI.Initialize(QuestEntry) ENCONTRADO");
                    MelonLogger.Msg($"[HARMONY-VERIFY]   ReturnType: {initializeMethod.ReturnType.Name}");
                    MelonLogger.Msg($"[HARMONY-VERIFY]   Parameters: {string.Join(", ", Array.ConvertAll(initializeMethod.GetParameters(), p => p.ParameterType.Name))}");
                    MelonLogger.Msg($"[HARMONY-VERIFY]   Declarado en: {initializeMethod.DeclaringType?.FullName}");
                }

                // Verificar QuestEntryHUDUI.UpdateUI()
                var updateUIMethod = AccessTools.Method(typeof(QuestEntryHUDUI), "UpdateUI");
                
                if (updateUIMethod == null)
                {
                    MelonLogger.Error("[HARMONY-VERIFY] ❌ QuestEntryHUDUI.UpdateUI() NO ENCONTRADO");
                }
                else
                {
                    MelonLogger.Msg($"[HARMONY-VERIFY] ✓ QuestEntryHUDUI.UpdateUI() ENCONTRADO");
                }

                MelonLogger.Msg("[HARMONY-VERIFY] ===== FIN VERIFICACIÓN PREVIA =====");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[HARMONY-VERIFY] Error durante verificación previa: {ex.Message}");
                MelonLogger.Error($"[HARMONY-VERIFY] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// POST-VERIFICATION: Confirma que los patches fueron aplicados correctamente
        /// </summary>
        private static void VerifyPatchesApplied()
        {
            try
            {
                MelonLogger.Msg("[HARMONY-VERIFY] ════════════════════════════════════════════════════════");
                MelonLogger.Msg("[HARMONY-VERIFY] VERIFICACIÓN POST - Patches aplicados");

                // Verificar Initialize patch
                var initializeMethod = AccessTools.Method(typeof(QuestEntryHUDUI), "Initialize", new[] { typeof(QuestEntry) });
                if (initializeMethod != null)
                {
                    var patches = HarmonyLib.Harmony.GetPatchInfo(initializeMethod);
                    if (patches != null && patches.Postfixes != null && patches.Postfixes.Count > 0)
                    {
                        MelonLogger.Msg("[HARMONY-VERIFY] ✓ QuestEntryHUDUI.Initialize tiene postfixes aplicados");
                        foreach (var postfix in patches.Postfixes)
                        {
                            if (postfix.owner == HarmonyId)
                                MelonLogger.Msg($"[HARMONY-VERIFY]   ✓ Nuestro patch: {postfix.PatchMethod?.Name}");
                        }
                    }
                }

                // Verificar UpdateUI patch
                var updateUIMethod = AccessTools.Method(typeof(QuestEntryHUDUI), "UpdateUI", new Type[0]);
                if (updateUIMethod != null)
                {
                    var patches = HarmonyLib.Harmony.GetPatchInfo(updateUIMethod);
                    if (patches != null && patches.Postfixes != null && patches.Postfixes.Count > 0)
                    {
                        MelonLogger.Msg("[HARMONY-VERIFY] ✓ QuestEntryHUDUI.UpdateUI tiene postfixes aplicados");
                        foreach (var postfix in patches.Postfixes)
                        {
                            if (postfix.owner == HarmonyId)
                                MelonLogger.Msg($"[HARMONY-VERIFY]   ✓ Nuestro patch: {postfix.PatchMethod?.Name}");
                        }
                    }
                }

                MelonLogger.Msg("[HARMONY-VERIFY] ════════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[HARMONY-VERIFY] Error durante verificación post: {ex.Message}");
                MelonLogger.Error($"[HARMONY-VERIFY] Stack trace: {ex.StackTrace}");
            }
        }
    }
}
