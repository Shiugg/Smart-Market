using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using Il2CppScheduleOne.Economy;

namespace SmartMarket.Patches
{
    /// <summary>
    /// Diagnostic utility to inspect Harmony patches on Dealer.AddContract()
    /// This is a TEMPORARY diagnostic file - will be removed after investigation
    /// </summary>
    public static class HarmonyDiagnostic
    {
        public static void DiagnoseAddContractPatch()
        {
            MelonLogger.Msg("[HARMONY-DIAG] ========== INICIANDO DIAGNÓSTICO ==========");
            
            try
            {
                // Get the exact method we're trying to patch
                Type dealerType = typeof(Dealer);
                MethodInfo addContractMethod = dealerType.GetMethod("AddContract", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                    null,
                    new Type[] { typeof(object) }, // Contract is passed as object in patches
                    null
                );

                if (addContractMethod == null)
                {
                    // Try with exact Contract type
                    MelonLogger.Msg("[HARMONY-DIAG] AddContract(object) not found, trying with Contract type...");
                    
                    // Attempt to find Contract type
                    Type contractType = Type.GetType("Il2CppScheduleOne.Economy.Contract, Assembly-CSharp");
                    if (contractType != null)
                    {
                        addContractMethod = dealerType.GetMethod("AddContract",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                            null,
                            new Type[] { contractType },
                            null
                        );
                        MelonLogger.Msg($"[HARMONY-DIAG] Found with Contract type: {addContractMethod != null}");
                    }
                }

                if (addContractMethod == null)
                {
                    // Last attempt: get all AddContract methods
                    MelonLogger.Msg("[HARMONY-DIAG] No AddContract found with object parameter. Listing all AddContract methods:");
                    MethodInfo[] allMethods = dealerType.GetMethods(
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                    );
                    
                    foreach (var method in allMethods)
                    {
                        if (method.Name == "AddContract")
                        {
                            var parameters = method.GetParameters();
                            string paramStr = string.Join(", ", Array.ConvertAll(parameters, p => $"{p.ParameterType.Name} {p.Name}"));
                            MelonLogger.Msg($"[HARMONY-DIAG] Found method: {method.ReturnType.Name} AddContract({paramStr})");
                        }
                    }
                    
                    MelonLogger.Msg("[HARMONY-DIAG] FATAL: Could not locate AddContract method");
                    return;
                }

                // Now check if Harmony has patches on this method
                MelonLogger.Msg($"[HARMONY-DIAG] Located method: {addContractMethod.ReturnType.Name} AddContract(...)");
                MelonLogger.Msg($"[HARMONY-DIAG] Full signature: {addContractMethod.DeclaringType?.FullName}.{addContractMethod.Name}");

                // Get patch info using HarmonyLib
                var patchInfo = HarmonyLib.Harmony.GetPatchInfo(addContractMethod);
                
                if (patchInfo == null)
                {
                    MelonLogger.Msg("[HARMONY-DIAG] GetPatchInfo returned NULL - method has NO patches applied");
                    return;
                }

                MelonLogger.Msg($"[HARMONY-DIAG] GetPatchInfo found patches:");
                MelonLogger.Msg($"[HARMONY-DIAG]   Prefix patches: {patchInfo.Prefixes.Count}");
                MelonLogger.Msg($"[HARMONY-DIAG]   Postfix patches: {patchInfo.Postfixes.Count}");
                MelonLogger.Msg($"[HARMONY-DIAG]   Transpiler patches: {patchInfo.Transpilers.Count}");
                MelonLogger.Msg($"[HARMONY-DIAG]   Finalizer patches: {patchInfo.Finalizers.Count}");

                // List prefixes
                if (patchInfo.Prefixes.Count > 0)
                {
                    MelonLogger.Msg($"[HARMONY-DIAG] Prefixes:");
                    foreach (var prefix in patchInfo.Prefixes)
                    {
                        MelonLogger.Msg($"[HARMONY-DIAG]   - Owner: {prefix.owner}");
                        MelonLogger.Msg($"[HARMONY-DIAG]     Method: {prefix.PatchMethod?.DeclaringType?.FullName}.{prefix.PatchMethod?.Name}");
                        MelonLogger.Msg($"[HARMONY-DIAG]     Priority: {prefix.priority}");
                    }
                }

                // List postfixes
                if (patchInfo.Postfixes.Count > 0)
                {
                    MelonLogger.Msg($"[HARMONY-DIAG] Postfixes:");
                    foreach (var postfix in patchInfo.Postfixes)
                    {
                        MelonLogger.Msg($"[HARMONY-DIAG]   - Owner: {postfix.owner}");
                        MelonLogger.Msg($"[HARMONY-DIAG]     Method: {postfix.PatchMethod?.DeclaringType?.FullName}.{postfix.PatchMethod?.Name}");
                        MelonLogger.Msg($"[HARMONY-DIAG]     Priority: {postfix.priority}");
                        
                        // Check if it's our patch
                        if (postfix.PatchMethod?.DeclaringType?.Name == "Dealer_AddContract_Overlay_Patch")
                        {
                            MelonLogger.Msg($"[HARMONY-DIAG]     ✓✓✓ THIS IS OUR OVERLAY PATCH ✓✓✓");
                        }
                    }
                }

                MelonLogger.Msg("[HARMONY-DIAG] ========== DIAGNÓSTICO COMPLETADO ==========");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[HARMONY-DIAG] Error during diagnosis: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
