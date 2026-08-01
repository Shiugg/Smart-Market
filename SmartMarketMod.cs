using MelonLoader;
using MelonLogger = SmartMarket.SmartMarketLogger;
using UnityEngine;

[assembly: MelonInfo(typeof(SmartMarket.SmartMarketMod), "Smart Market", "1.0.0", "Shiugg & AntiGravity")]

namespace SmartMarket
{
    public class SmartMarketMod : MelonMod
    {
        private static bool configVisible = false;
        private static Rect configWindowRect = new Rect(10, 50, 900, 700);
        private static Vector2 scrollPosition = Vector2.zero;
        private static int activeTab = 0; // 0=General, 1=Events
        
        // Text field state
        // State for collapse/expand each event panel
        private static bool showRumorDetails = false;
        private static bool showViralDetails = false;
        private static bool showFestivalDetails = false;
        private static bool showOperativoDetails = false;
        private static bool showEstacionalDetails = false;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("BOOT", "=================================================");
            MelonLogger.Msg("BOOT", "SMART MARKET CORE V1.0 - INICIANDO SISTEMAS");
            MelonLogger.Msg("BOOT", "=================================================");

            // PASO 1: Inicializar patches Harmony PRIMERO (antes que cualquier otro sistema)
            try
            {
                MelonLogger.Msg("BOOT", "[HARMONY] Inicializando sistema de patches...");
                Patches.HarmonyPatcher.Initialize();
                MelonLogger.Msg("BOOT", "[HARMONY] ✓ Patches Harmony inicializados");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning("BOOT", $"Error inicializando patches Harmony: {ex.Message}");
            }

            try
            {
                Core.MemorySystem.Init();
                MelonLogger.Msg("BOOT", "Sistema de memoria inicializado correctamente.");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning("BOOT", $"No se pudo inicializar el sistema de memoria: {ex.Message}");
            }

            try
            {
                Core.SeasonalChangeManager.Init();
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] No se pudo inicializar SeasonalChangeManager: {ex.Message}");
            }

            try
            {
                Core.CustomerWatcher.Init();
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] No se pudo inicializar CustomerWatcher: {ex.Message}");
            }

            try
            {
                MelonLogger.Msg("BOOT", "[OVERLAY] Inicializando SmartMarketContractOverlay...");
                Core.SmartMarketContractOverlay.Initialize();
                MelonLogger.Msg("BOOT", "[OVERLAY] SmartMarket Contract Overlay initialized successfully.");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] [OVERLAY] No se pudo inicializar SmartMarketContractOverlay: {ex.Message}\n{ex.StackTrace}");
            }

            // TEMPORARY: Diagnose Harmony patch status
            try
            {
                MelonLogger.Msg("BOOT", "[HARMONY-DIAG] Running Harmony diagnostic...");
                Patches.HarmonyDiagnostic.DiagnoseAddContractPatch();
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] Harmony diagnostic failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public override void OnUpdate()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.F7))
            {
                configVisible = !configVisible;
                MelonLogger.Msg("CONTEXT", $"Config menu {(configVisible ? "abierto" : "cerrado")}. Presiona F7 para alternar.");
            }

            // update seasonal manager
            try
            {
                Core.SeasonalChangeManager.Update();
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] SeasonalChangeManager.Update failed: {ex.Message}");
            }

            // update messaging monitor (pending offers timeout)
            try
            {
                Core.MessagingMonitor.Update();
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] MessagingMonitor.Update failed: {ex.Message}");
            }

            // update customer watcher to detect killed/robbed customers
            try
            {
                Core.CustomerWatcher.Update();
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[SmartMarket] CustomerWatcher.Update failed: {ex.Message}");
            }
        }

        public override void OnGUI()
        {
            // draw seasonal overlay even if config not visible
            try
            {
                Core.SeasonalChangeManager.DrawOverlay();
            }
            catch { }

            if (!configVisible) return;

            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.Box(new Rect(configWindowRect.x - 8, configWindowRect.y - 28, configWindowRect.width + 16, configWindowRect.height + 36), string.Empty);
            GUI.color = Color.white;

            GUILayout.BeginArea(configWindowRect, "SmartMarket Config (F7 cerrar)", GUI.skin.window);
            
            // Tab buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(activeTab == 0, "General", GUILayout.Width(80)))
                activeTab = 0;
            if (GUILayout.Toggle(activeTab == 1, "Eventos", GUILayout.Width(80)))
                activeTab = 1;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            if (activeTab == 0)
                DrawGeneralTab();
            else if (activeTab == 1)
                DrawEventsTab();

            GUILayout.EndScrollView();

            // Footer buttons (always visible)
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Guardar todo"))
            {
                Core.SmartMarketConfig.Save();
                Core.SmartMarketConfig.SaveEvents();
                MelonLogger.Msg("CONTEXT", "Toda la configuración guardada.");
            }
            if (GUILayout.Button("Cerrar (F7)"))
            {
                configVisible = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void DrawGeneralTab()
        {
            GUILayout.Label("=== CONFIGURACIÓN GENERAL ===", GUI.skin.box);
            GUILayout.Space(4);

            GUILayout.Label($"Recomendación boca a boca: {Core.SmartMarketConfig.NeighborhoodRecommendationAcceptanceScore:0.0} / 10");
            float acceptance = GUILayout.HorizontalSlider(Core.SmartMarketConfig.NeighborhoodRecommendationAcceptanceScore, 0.0f, 10.0f);
            if (!Mathf.Approximately(acceptance, Core.SmartMarketConfig.NeighborhoodRecommendationAcceptanceScore))
            {
                Core.SmartMarketConfig.NeighborhoodRecommendationAcceptanceScore = acceptance;
            }

            GUILayout.Label($"Probabilidad de llegar al cliente: {Core.SmartMarketConfig.NeighborhoodRecommendationReachScore:0.0} / 10");
            float reach = GUILayout.HorizontalSlider(Core.SmartMarketConfig.NeighborhoodRecommendationReachScore, 0.0f, 10.0f);
            if (!Mathf.Approximately(reach, Core.SmartMarketConfig.NeighborhoodRecommendationReachScore))
            {
                Core.SmartMarketConfig.NeighborhoodRecommendationReachScore = reach;
            }

            GUILayout.Label($"Máx eventos virales diarios: {Core.SmartMarketConfig.MaxDailyViralEvents}");
            int maxDaily = Mathf.RoundToInt(GUILayout.HorizontalSlider(Core.SmartMarketConfig.MaxDailyViralEvents, 0, 20));
            if (maxDaily != Core.SmartMarketConfig.MaxDailyViralEvents)
            {
                Core.SmartMarketConfig.MaxDailyViralEvents = maxDaily;
            }

            bool dbg = GUILayout.Toggle(Core.SmartMarketConfig.DebugEnabled, "Depuración activada");
            if (dbg != Core.SmartMarketConfig.DebugEnabled)
            {
                Core.SmartMarketConfig.DebugEnabled = dbg;
            }

            GUILayout.Space(8);
            GUILayout.Label("=== CATÁLOGO DE DATOS ===", GUI.skin.box);
            if (GUILayout.Button("Actualizar catálogo (Extraer productos/zonas)"))
            {
                try
                {
                    Core.EventDataExtractor.ExtractAndSave();
                    MelonLogger.Msg("[SmartMarket] Catálogo actualizado desde menú F7.");
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[SmartMarket] Extraction error: {ex}");
                }
            }

            GUILayout.Space(12);

            GUILayout.Label("=== PESOS DE VALORACIÓN DE PRODUCTOS ===", GUI.skin.box);
            GUILayout.Label($"Peso - efecto preferido: {Core.SmartMarketConfig.EffectMatchWeight:0.00}");
            float effectMatch = GUILayout.HorizontalSlider(Core.SmartMarketConfig.EffectMatchWeight, 0.0f, 5.0f);
            if (!Mathf.Approximately(effectMatch, Core.SmartMarketConfig.EffectMatchWeight)) Core.SmartMarketConfig.EffectMatchWeight = Mathf.Round(effectMatch * 100f) / 100f;
            GUILayout.Label("Aumenta la preferencia por productos que contengan efectos que el cliente prefiere.");

            GUILayout.Label($"Peso - efecto rechazado: {Core.SmartMarketConfig.EffectMismatchWeight:0.00}");
            float effectMismatch = GUILayout.HorizontalSlider(Core.SmartMarketConfig.EffectMismatchWeight, 0.0f, 2.0f);
            if (!Mathf.Approximately(effectMismatch, Core.SmartMarketConfig.EffectMismatchWeight)) Core.SmartMarketConfig.EffectMismatchWeight = Mathf.Round(effectMismatch * 100f) / 100f;
            GUILayout.Label("Penaliza productos que tengan efectos que el cliente rechaza.");

            GUILayout.Label($"Peso - calidad aceptada: {Core.SmartMarketConfig.QualityMatchWeight:0.00}");
            float qualityMatch = GUILayout.HorizontalSlider(Core.SmartMarketConfig.QualityMatchWeight, 0.0f, 5.0f);
            if (!Mathf.Approximately(qualityMatch, Core.SmartMarketConfig.QualityMatchWeight)) Core.SmartMarketConfig.QualityMatchWeight = Mathf.Round(qualityMatch * 100f) / 100f;
            GUILayout.Label("Bonus si la calidad del producto cumple o supera el mínimo aceptado por el cliente.");

            GUILayout.Label($"Peso - penalización por baja calidad: {Core.SmartMarketConfig.QualityMismatchWeight:0.00}");
            float qualityMismatch = GUILayout.HorizontalSlider(Core.SmartMarketConfig.QualityMismatchWeight, 0.0f, 2.0f);
            if (!Mathf.Approximately(qualityMismatch, Core.SmartMarketConfig.QualityMismatchWeight)) Core.SmartMarketConfig.QualityMismatchWeight = Mathf.Round(qualityMismatch * 100f) / 100f;
            GUILayout.Label("Penaliza si la calidad del producto está por debajo del mínimo aceptado (más fuerte si el cliente no confía).");

            GUILayout.Label($"Peso - adicción: {Core.SmartMarketConfig.AddictionWeight:0.00}");
            float addictionWeight = GUILayout.HorizontalSlider(Core.SmartMarketConfig.AddictionWeight, 0.0f, 3.0f);
            if (!Mathf.Approximately(addictionWeight, Core.SmartMarketConfig.AddictionWeight)) Core.SmartMarketConfig.AddictionWeight = Mathf.Round(addictionWeight * 100f) / 100f;
            GUILayout.Label("Aumenta la propensión a comprar de clientes con niveles más altos de adicción.");

            GUILayout.Label($"Peso - confianza: {Core.SmartMarketConfig.TrustWeight:0.00}");
            float trustWeight = GUILayout.HorizontalSlider(Core.SmartMarketConfig.TrustWeight, 0.0f, 3.0f);
            if (!Mathf.Approximately(trustWeight, Core.SmartMarketConfig.TrustWeight)) Core.SmartMarketConfig.TrustWeight = Mathf.Round(trustWeight * 100f) / 100f;
            GUILayout.Label("Aumenta la influencia de la confianza histórica del cliente en la valoración.");

            GUILayout.Label($"Peso - satisfacción histórica: {Core.SmartMarketConfig.SatisfactionWeight:0.00}");
            float satisfactionWeight = GUILayout.HorizontalSlider(Core.SmartMarketConfig.SatisfactionWeight, 0.0f, 3.0f);
            if (!Mathf.Approximately(satisfactionWeight, Core.SmartMarketConfig.SatisfactionWeight)) Core.SmartMarketConfig.SatisfactionWeight = Mathf.Round(satisfactionWeight * 100f) / 100f;
            GUILayout.Label("Aumenta la influencia de la satisfacción histórica (clientes fieles dan ventaja).\n");

            GUILayout.Label($"Peso - penalización por entregar menos cantidad (UnderSupply): {Core.SmartMarketConfig.UnderSupplyWeight:0.00}");
            float underSupply = GUILayout.HorizontalSlider(Core.SmartMarketConfig.UnderSupplyWeight, 0.0f, 3.0f);
            if (!Mathf.Approximately(underSupply, Core.SmartMarketConfig.UnderSupplyWeight)) Core.SmartMarketConfig.UnderSupplyWeight = Mathf.Round(underSupply * 100f) / 100f;
            GUILayout.Label("Penaliza el score si la cantidad ofrecida es menor a la solicitada por el cliente. Reduce la probabilidad de aceptar.");

            GUILayout.Label($"Peso - penalización por producto equivocado: {Core.SmartMarketConfig.WrongProductWeight:0.00}");
            float wrongProd = GUILayout.HorizontalSlider(Core.SmartMarketConfig.WrongProductWeight, 0.0f, 3.0f);
            if (!Mathf.Approximately(wrongProd, Core.SmartMarketConfig.WrongProductWeight)) Core.SmartMarketConfig.WrongProductWeight = Mathf.Round(wrongProd * 100f) / 100f;
            GUILayout.Label("Penaliza el score si el producto ofrecido no coincide con lo que el cliente pidió (nombre/ID).\n");

            GUILayout.Space(12);
        }

        private void DrawEventsTab()
        {
            GUILayout.Label("=== CONFIGURACIÓN DE EVENTOS ===", GUI.skin.box);
            GUILayout.Label("Haz clic en ► para expandir/contraer cada evento");
            GUILayout.Space(4);

            DrawEventPanel("RUMOR DE ESCASEZ", Core.SmartMarketConfig.Events.rumorEscasez, ref showRumorDetails);
            GUILayout.Space(4);

            DrawEventPanel("PRODUCTO VIRAL", Core.SmartMarketConfig.Events.productoViral, ref showViralDetails);
            GUILayout.Space(4);

            DrawEventPanel("FESTIVAL DE BARRIO", Core.SmartMarketConfig.Events.festivalBarrio, ref showFestivalDetails);
            GUILayout.Space(4);

            DrawEventPanel("OPERATIVO POLICIAL", Core.SmartMarketConfig.Events.operativoPolicial, ref showOperativoDetails);
            GUILayout.Space(4);

            DrawEventPanel("CAMBIO ESTACIONAL", Core.SmartMarketConfig.Events.cambioEstacional, ref showEstacionalDetails);
        }

        private void DrawEventPanel(string title, Core.RumorEscasezConfig config, ref bool showDetails)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            
            // Header with toggle for enable + expand button
            GUILayout.BeginHorizontal();
            config.enabled = GUILayout.Toggle(config.enabled, title, GUILayout.Width(150));
            showDetails = GUILayout.Toggle(showDetails, showDetails ? "▼" : "►", GUILayout.Width(30));
            GUILayout.EndHorizontal();
            
            // Show details only if expanded
            if (showDetails)
            {
                GUILayout.Label($"Probabilidad: {config.probability:0.0}% / día");
                config.probability = GUILayout.HorizontalSlider(config.probability, 0.0f, 100.0f);

                GUILayout.Label($"Intensidad: {config.intensity:0.00} / 10");
                config.intensity = Mathf.Round(GUILayout.HorizontalSlider(config.intensity, 0.0f, 10.0f) * 4f) / 4f;

                GUILayout.Label($"Duración: {config.durationMin}-{config.durationMax} días");
                config.durationMin = Mathf.RoundToInt(GUILayout.HorizontalSlider(config.durationMin, 1, 10));
                config.durationMax = Mathf.Max(config.durationMin, Mathf.RoundToInt(GUILayout.HorizontalSlider(config.durationMax, config.durationMin, 10)));

                GUILayout.Label($"Cooldown: {config.cooldownDays} días");
                config.cooldownDays = Mathf.RoundToInt(GUILayout.HorizontalSlider(config.cooldownDays, 1, 30));

                GUILayout.Label($"Target producto: {(string.IsNullOrEmpty(config.targetProductId) ? "random" : config.targetProductId)}");
            }
            
            GUILayout.EndVertical();
        }

        private void DrawEventPanel(string title, Core.ProductoViralConfig config, ref bool showDetails)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            
            GUILayout.BeginHorizontal();
            config.enabled = GUILayout.Toggle(config.enabled, title, GUILayout.Width(150));
            showDetails = GUILayout.Toggle(showDetails, showDetails ? "▼" : "►", GUILayout.Width(30));
            GUILayout.EndHorizontal();
            
            if (showDetails)
            {
                GUILayout.Label($"Probabilidad: {config.probability:0.0}% / día");
                config.probability = GUILayout.HorizontalSlider(config.probability, 0.0f, 100.0f);

                GUILayout.Label($"Intensidad: {config.intensity:0.00} / 10");
                config.intensity = Mathf.Round(GUILayout.HorizontalSlider(config.intensity, 0.0f, 10.0f) * 4f) / 4f;

                GUILayout.Label($"Duración: {config.durationMin}-{config.durationMax} días");
                config.durationMin = Mathf.RoundToInt(GUILayout.HorizontalSlider(config.durationMin, 1, 10));
                config.durationMax = Mathf.Max(config.durationMin, Mathf.RoundToInt(GUILayout.HorizontalSlider(config.durationMax, config.durationMin, 10)));

                GUILayout.Label($"Cooldown: {config.cooldownDays} días");
                config.cooldownDays = Mathf.RoundToInt(GUILayout.HorizontalSlider(config.cooldownDays, 1, 30));

                GUILayout.Label($"Target producto: {(string.IsNullOrEmpty(config.targetProductId) ? "random" : config.targetProductId)}");
            }
            
            GUILayout.EndVertical();
        }

        private void DrawEventPanel(string title, Core.FestivalBarrioConfig config, ref bool showDetails)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            
            GUILayout.BeginHorizontal();
            config.enabled = GUILayout.Toggle(config.enabled, title, GUILayout.Width(150));
            showDetails = GUILayout.Toggle(showDetails, showDetails ? "▼" : "►", GUILayout.Width(30));
            GUILayout.EndHorizontal();
            
            if (showDetails)
            {
                GUILayout.Label($"Probabilidad: {config.probability:0.0}% / día por zona");
                config.probability = GUILayout.HorizontalSlider(config.probability, 0.0f, 100.0f);

                GUILayout.Label($"Intensidad: {config.intensity:0.00} / 10");
                config.intensity = Mathf.Round(GUILayout.HorizontalSlider(config.intensity, 0.0f, 10.0f) * 4f) / 4f;

                GUILayout.Label($"Duración: {config.durationMin}-{config.durationMax} días");
                config.durationMin = Mathf.RoundToInt(GUILayout.HorizontalSlider(config.durationMin, 1, 10));
                config.durationMax = Mathf.Max(config.durationMin, Mathf.RoundToInt(GUILayout.HorizontalSlider(config.durationMax, config.durationMin, 10)));

                GUILayout.Label($"Cooldown: {config.cooldownDays} días");
                config.cooldownDays = Mathf.RoundToInt(GUILayout.HorizontalSlider(config.cooldownDays, 1, 30));

                GUILayout.Label($"Zonas: {(config.targetZoneIds.Count == 0 ? "todas" : string.Join(", ", config.targetZoneIds))}");
            }
            
            GUILayout.EndVertical();
        }

        private void DrawEventPanel(string title, Core.OperativoPolicialConfig config, ref bool showDetails)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            
            GUILayout.BeginHorizontal();
            config.enabled = GUILayout.Toggle(config.enabled, title, GUILayout.Width(150));
            showDetails = GUILayout.Toggle(showDetails, showDetails ? "▼" : "►", GUILayout.Width(30));
            GUILayout.EndHorizontal();
            
            if (showDetails)
            {
                GUILayout.Label($"Prob. base: {config.baseProbability:0.0}% | Máx: {config.maxProbability:0.0}%");
                config.baseProbability = GUILayout.HorizontalSlider(config.baseProbability, 0.0f, 100.0f);
                config.maxProbability = Mathf.Max(config.baseProbability, GUILayout.HorizontalSlider(config.maxProbability, config.baseProbability, 100.0f));

                GUILayout.Label($"Lookback: {config.lookbackDays} días | Intensidad: {config.intensity:0.00} / 10");
                config.lookbackDays = Mathf.RoundToInt(GUILayout.HorizontalSlider(config.lookbackDays, 1, 14));
                config.intensity = Mathf.Round(GUILayout.HorizontalSlider(config.intensity, 0.0f, 10.0f) * 4f) / 4f;

                GUILayout.Label($"Duración: {config.durationMin}-{config.durationMax} días");
                config.durationMin = Mathf.RoundToInt(GUILayout.HorizontalSlider(config.durationMin, 1, 10));
                config.durationMax = Mathf.Max(config.durationMin, Mathf.RoundToInt(GUILayout.HorizontalSlider(config.durationMax, config.durationMin, 10)));

                GUILayout.Label($"Cooldown: {config.cooldownDays} días | Umbral exclusión: {config.exclusionThreshold:0.0}%");
                config.cooldownDays = Mathf.RoundToInt(GUILayout.HorizontalSlider(config.cooldownDays, 1, 30));
                config.exclusionThreshold = GUILayout.HorizontalSlider(config.exclusionThreshold, 0.0f, 100.0f);

                GUILayout.Label($"Zonas: {(config.targetZoneIds.Count == 0 ? "dinámico" : string.Join(", ", config.targetZoneIds))}");
            }
            
            GUILayout.EndVertical();
        }

        private void DrawEventPanel(string title, Core.CambioEstacionalConfig config, ref bool showDetails)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            
            GUILayout.BeginHorizontal();
            config.enabled = GUILayout.Toggle(config.enabled, title, GUILayout.Width(150));
            showDetails = GUILayout.Toggle(showDetails, showDetails ? "▼" : "►", GUILayout.Width(30));
            GUILayout.EndHorizontal();
            
            if (showDetails)
            {
                GUILayout.Label($"Intensidad: {config.intensity:0.00} / 10");
                config.intensity = Mathf.Round(GUILayout.HorizontalSlider(config.intensity, 0.0f, 10.0f) * 4f) / 4f;

                GUILayout.Label("(Trigger calendar-based)");

                // Debug force button
                if (GUILayout.Button("Forzar cambio estacional (DEBUG)"))
                {
                    Core.SeasonalChangeManager.ForceSeasonalChangeDebug();
                }
            }
            
            GUILayout.EndVertical();
        }
    }
}
