using SmartMarket.Core;
using System.Collections.Generic;
using UnityEngine;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Economy;

namespace SmartMarket.Core
{
    public static class MessageGenerator
    {
        // Diccionario de estilos según el barrio
        private static readonly Dictionary<Neighborhood, string[]> PrefixStyles = new Dictionary<Neighborhood, string[]>
        {
            { Neighborhood.Downtown, new[] { "Dame", "Necesito", "Traeme", "Rápido," } }, // Directos/Groseros
            { Neighborhood.Docks, new[] { "Eh loco,", "Pasame", "Preciso", "Che," } },    // Callejeros
            { Neighborhood.Westville, new[] { "Hola,", "¿Tienes", "Ando buscando", "Necesito" } }, // Neutrales
            { Neighborhood.Northtown, new[] { "Qué onda,", "Buscando", "A ver si tienes", "Dame" } }, // Relajados
            { Neighborhood.Suburbia, new[] { "Hola, ¿podrías traerme", "Disculpa,", "Estoy buscando", "Necesitaría" } }, // Amables
            { Neighborhood.Uptown, new[] { "Requiero", "Tráeme", "Prepara", "Exijo" } } // Snobs/Fríos
        };

        public static string GenerateMessage(ConsumerProfile profile, string productName, string effectGained, bool isWordOfMouth)
        {
            var context = new MessageContext
            {
                Profile = profile ?? new ConsumerProfile("Desconocido", ConsumerType.Classic, Neighborhood.Westville),
                RequestedProductName = productName,
                RequestedEffect = effectGained,
                IsWordOfMouth = isWordOfMouth,
                IsUrgent = isWordOfMouth || (profile != null && profile.Type == ConsumerType.Addict),
                HasGoodQualityFocus = profile != null && profile.Type == ConsumerType.Gourmet,
                Neighborhood = profile != null ? profile.HomeNeighborhood : Neighborhood.Westville,
                NeighborhoodStandard = profile != null ? ProfileManager.GetNeighborhoodStandard(profile.HomeNeighborhood) : NeighborhoodStandard.Normal
            };

            return GenerateMessage(context);
        }

        public static string GeneratePenaltyMessage(ConsumerProfile profile = null)
        {
            var effectiveProfile = profile ?? new ConsumerProfile("Desconocido", ConsumerType.Classic, Neighborhood.Westville);
            return MessagePresets.GetPenaltyPreset(effectiveProfile.Type);
        }
 
        public static string GenerateMessage(MessageContext context)
        {
            if (context == null)
            {
                return "Necesito lo de siempre.";
            }

            var profile = context.Profile ?? new ConsumerProfile("Desconocido", ConsumerType.Classic, Neighborhood.Westville);
            var prefixes = PrefixStyles.ContainsKey(context.Neighborhood) ? PrefixStyles[context.Neighborhood] : PrefixStyles[Neighborhood.Westville];
            string prefix = prefixes[UnityEngine.Random.Range(0, prefixes.Length)];
            string productName = context.RequestedProductName;
            string effectName = context.RequestedEffect;
            bool isWordOfMouth = context.IsWordOfMouth;
            bool isEffectDriven = context.IsEffectDriven;
            bool isAddict = profile.Type == ConsumerType.Addict;
            bool isClassic = profile.Type == ConsumerType.Classic;
            bool isExperimenter = profile.Type == ConsumerType.Experimenter;
            bool isGourmet = profile.Type == ConsumerType.Gourmet;
            bool highStandard = context.NeighborhoodStandard == NeighborhoodStandard.High;
            bool marginalStandard = context.NeighborhoodStandard == NeighborhoodStandard.Marginal;

            // Debug: log context decisions so we can trace why a preset branch was (or was not) chosen.
            try
            {
                var dbg = $"GenerateMessage: Customer={(profile != null ? profile.ID : "Desconocido")} Product='{productName}' Effect='{effectName}' RequestedQuality='{context.RequestedQuality}' IsWOM={isWordOfMouth} IsEffect={isEffectDriven} IsRepeat={context.IsRepeatRequest} HasGoodQualityFocus={context.HasGoodQualityFocus} Preferences=[Novelty:{context.Preferences.PreferenceNovelty:0.00},SubTol:{context.Preferences.SubstitutionTolerance:0.00},Urgency:{context.Preferences.Urgency:0.00},QualityBias:{context.Preferences.QualityBias:0.00}] NeighborhoodAcceptance={SmartMarketConfig.NeighborhoodRecommendationAcceptanceScore} NeighborhoodReach={SmartMarketConfig.NeighborhoodRecommendationReachScore}";
                SmartMarketConfig.LogDebug(dbg);
            }
            catch { }

            if (isWordOfMouth && !string.IsNullOrEmpty(productName))
            {
                string quantityText = context.RequestedQuantity > 0 ? context.RequestedQuantity.ToString() : "algo";
                string template = MessagePresets.GetRandomPreset("wordofmouth");
                
                // Reemplazar placeholders
                string message = template
                    .Replace("{producto}", MessageStyler.ColorizeProduct(productName))
                    .Replace("{cantidad}", MessageStyler.ColorizeQuantity(quantityText));
                
                SmartMarketConfig.LogDebug($"GenerateMessage -> WordOfMouth preset: '{message}'");
                return message;
            }

            if (!string.IsNullOrEmpty(productName) && !string.IsNullOrEmpty(context.RequestedQuality))
            {
                string quantityText = context.RequestedQuantity > 0 ? $"{context.RequestedQuantity} g" : "algo";
                string template = MessagePresets.GetRandomPreset("quality");
                
                // Reemplazar placeholders
                string message = template
                    .Replace("{droga}", MessageStyler.ColorizeProduct(productName))
                    .Replace("{calidad}", MessageStyler.ColorizeQuality(context.RequestedQuality))
                    .Replace("{cantidad}", MessageStyler.ColorizeQuantity(quantityText));
                
                SmartMarketConfig.LogDebug($"GenerateMessage -> Quality preset: '{message}'");
                return message;
            }
 
            if (!string.IsNullOrEmpty(productName) && isEffectDriven && !string.IsNullOrEmpty(effectName))
            {
                string quantityText = context.RequestedQuantity > 0 ? $"{context.RequestedQuantity} g" : "algo";
                string template = MessagePresets.GetRandomPreset("effect");
                
                // Reemplazar placeholders
                string message = template
                    .Replace("{droga}", MessageStyler.ColorizeProduct(productName))
                    .Replace("{efecto}", MessageStyler.ColorizeEffect(effectName))
                    .Replace("{cantidad}", MessageStyler.ColorizeQuantity(quantityText));
                
                SmartMarketConfig.LogDebug($"GenerateMessage -> Effect preset: '{message}'");
                return message;
            }

            if (!string.IsNullOrEmpty(productName))
            {
                            string quantityText = context.RequestedQuantity > 0 ? $"{context.RequestedQuantity} g" : "algo";
                            string chosenMsg = null;
                            if (isClassic)
                            {
                                if (productName.ToLower().Contains("maria") || productName.ToLower().Contains("weed") || productName.ToLower().Contains("mushroom"))
                                {
                                    chosenMsg = $"{prefix} quiero {MessageStyler.ColorizeQuantity(quantityText)} de {MessageStyler.ColorizeProduct(productName)}. Algo suave para relajarme.";
                                }
                                else
                                {
                                    chosenMsg = $"{prefix} necesito {MessageStyler.ColorizeQuantity(quantityText)} de {MessageStyler.ColorizeProduct(productName)}, algo seguro y confiable. Nada muy loco.";
                                }
                            }
                            else if (isExperimenter)
                            {
                                if (marginalStandard)
                                    chosenMsg = $"{prefix} quiero probar {MessageStyler.ColorizeQuantity(quantityText)} si tenés algo nuevo o una mezcla rara.";
                                else
                                    chosenMsg = $"{prefix} si tenés {MessageStyler.ColorizeQuantity(quantityText)} de {MessageStyler.ColorizeProduct(productName)} o algo parecido, quiero algo distinto. Estoy probando mezclas nuevas.";
                            }
                            else if (isAddict)
                            {
                                chosenMsg = $"{prefix} dame {MessageStyler.ColorizeQuantity(quantityText)} de {MessageStyler.ColorizeProduct(productName)} ahora mismo. No quiero contraofertas, trae lo que tengas y ya.";
                            }
                            else if (isGourmet)
                            {
                                var want = productName.ToLower().Contains("coca") ? "algo de alta calidad" : "algo bien premium"; // want remains plain text (no color)
                                chosenMsg = $"{prefix} quiero {MessageStyler.ColorizeQuantity(quantityText)} de {MessageStyler.ColorizeProduct(productName)} de la mejor calidad. Si es potente y refinado, mejor aún.";
                            }
                            else
                            {
                                // Add sample quality-demand templates (user examples)
                                string[] qualityDemandTemplates = new[]
                                {
                                    $"{prefix} nada por debajo de Premium. Si no, olvídalo. (Quiero {MessageStyler.ColorizeQuantity(quantityText)})",
                                    $"{prefix} solo Premium. No quiero estándar o barato. Dame {MessageStyler.ColorizeQuantity(quantityText)}.",
                                    $"{prefix} necesitamos calidad: Premium o mejor, nada menos. Requiero {MessageStyler.ColorizeQuantity(quantityText)}."
                                };

                                // Occasionally return an explicit quality demand instead of generic request
                                if (context.HasGoodQualityFocus && UnityEngine.Random.Range(0f, 1f) < 0.35f)
                                    chosenMsg = qualityDemandTemplates[UnityEngine.Random.Range(0, qualityDemandTemplates.Length)];
                                else
                                    chosenMsg = $"{prefix} {MessageStyler.ColorizeProduct(productName)}, {MessageStyler.ColorizeQuantity(quantityText)}, lo mejor que tengas.";
                            }

                SmartMarketConfig.LogDebug($"GenerateMessage -> Product-specific chosen msg for {(profile!=null?profile.ID:"Desconocido")} : '{chosenMsg}'");
                return chosenMsg;
            }

            if (!string.IsNullOrEmpty(effectName))
            {
                string em = null;
                if (isClassic)
                    em = $"{prefix} quiero algo que me deje {effectName}, pero no muy fuerte.";
                else if (isExperimenter)
                    em = $"{prefix} algo con efecto {effectName}, quiero ver qué tal me pega.";
                else if (isAddict)
                    em = $"{prefix} necesito algo que me deje {effectName} ya mismo.";
                else if (isGourmet)
                    em = $"{prefix} quiero una experiencia {effectName} de primera calidad.";

                if (!string.IsNullOrEmpty(em))
                {
                    SmartMarketConfig.LogDebug($"GenerateMessage -> Effect fallback chosen for {(profile!=null?profile.ID:"Desconocido")} : '{em}'");
                    return em;
                }
            }

            if (isAddict)
            {
                var am = $"{prefix} traé lo que tengas, y rápido. No me hagas esperar.";
                SmartMarketConfig.LogDebug($"GenerateMessage -> Addict default: '{am}'");
                return am;
            }

            if (highStandard)
            {
                var hm = $"{prefix} algo de calidad, por favor. No quiero cualquier cosa.";
                SmartMarketConfig.LogDebug($"GenerateMessage -> HighStandard default: '{hm}'");
                return hm;
            }

            if (marginalStandard)
            {
                var mm = $"{prefix} pasame algo que pegue fuerte, sin vueltas.";
                SmartMarketConfig.LogDebug($"GenerateMessage -> MarginalStandard default: '{mm}'");
                return mm;
            }

            var df = $"{prefix} lo de siempre.";
            SmartMarketConfig.LogDebug($"GenerateMessage -> Fallback default: '{df}'");
            return df;
        }
    }
}
