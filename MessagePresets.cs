using System;
using System.Collections.Generic;

namespace SmartMarket.Core
{
    /// <summary>
    /// Almacena todos los presets de mensajes según la situación del juego.
    /// Los mensajes NO son generados dinámicamente; se seleccionan aleatoriamente de estos presets.
    /// </summary>
    public static class MessagePresets
    {
        // Evento: Boca en boca (Word of Mouth)
        public static readonly string[] WordOfMouthTemplates = new[]
        {
            "Me hablaron de {producto}. Traeme {cantidad}.",
            "Dicen que {producto} pega fuerte. Quiero {cantidad}.",
            "Un amigo me pasó el dato. Quiero {cantidad} de {producto}.",
            "Escuché cosas buenas de {producto}. Traeme {cantidad}.",
            "Me recomendaron {producto}. ¿Tenés {cantidad}?",
            "Todos están hablando de {producto}. Necesito {cantidad}.",
            "Me dijeron que pruebe {producto}. Quiero {cantidad}.",
            "Andan diciendo que {producto} está bueno. Dame {cantidad}.",
            "Un conocido me consiguió tu contacto. Quiero {cantidad} de {producto}.",
            "Quiero ver si es tan bueno como dicen. Traeme {cantidad} de {producto}."
        };

        // Pedidos por efecto
        public static readonly string[] EffectDrivenTemplates = new[]
        {
            "Necesito {droga} {efecto}. Traeme {cantidad}.",
            "¿Tenés {cantidad} de {droga} {efecto}?",
            "Busco {droga} {efecto}.",
            "Quiero {droga} con efecto {efecto}.",
            "Dame {cantidad} de {droga} {efecto}.",
            "Estoy buscando {droga} {efecto}.",
            "Conseguime {droga} {efecto}.",
            "¿Todavía vendés {droga} {efecto}?",
            "Necesito algo {efecto}. Dame {cantidad} de {droga}.",
            "Quiero probar {droga} {efecto}."
        };

        // Pedidos por calidad
        public static readonly string[] QualityDrivenTemplates = new[]
        {
            "Quiero {droga} {calidad}. Traeme {cantidad}.",
            "Busco {cantidad} de {droga} {calidad}.",
            "¿Tenés {droga} calidad {calidad}?",
            "Necesito {droga} {calidad}.",
            "Dame {droga} {calidad}.",
            "Sólo quiero {calidad}. Traeme {droga}.",
            "Estoy buscando {droga} {calidad}.",
            "Conseguime {cantidad} de {droga} {calidad}.",
            "¿Todavía hacés {droga} {calidad}?",
            "Hoy quiero algo {calidad}. Dame {droga}."
        };

        // Cuando el jugador roba o mata al cliente - mensajes genéricos
        public static readonly string[] PenaltyTemplates = new[]
        {
            "Después de lo que hiciste no quiero hacer negocios con vos.",
            "Me dejaste tirado. Olvidate de mí por un tiempo.",
            "Perdiste mi confianza.",
            "No vuelvas a escribirme.",
            "No pienso comprarte nada por ahora.",
            "Buscate otro cliente.",
            "No quiero problemas con vos.",
            "Se terminó el negocio entre nosotros.",
            "No vuelvas a aparecer por acá.",
            "Ya sé cómo hacés negocios. Paso."
        };

        // Variantes según personalidad - Conservador
        public static readonly string[] PenaltyConservativeTemplates = new[]
        {
            "Ya tuve suficiente con vos.",
            "No vuelvo a confiar.",
            "Se terminó."
        };

        // Variantes según personalidad - Adicto
        public static readonly string[] PenaltyAddictTemplates = new[]
        {
            "Sos un hijo de puta... pero cuando se me pase capaz volvemos a hablar.",
            "Necesito consumir, pero ahora no quiero saber nada de vos.",
            "Me cagaste feo."
        };

        // Variantes según personalidad - Curioso
        public static readonly string[] PenaltyCuriousTemplates = new[]
        {
            "Pensé que eras distinto.",
            "No esperaba eso de vos.",
            "Mejor pruebo con otro."
        };

        // Variantes según personalidad - Agresivo
        public static readonly string[] PenaltyAggressiveTemplates = new[]
        {
            "La próxima no termina igual.",
            "Nos vamos a volver a cruzar.",
            "Esto no queda así."
        };

        /// <summary>
        /// Obtiene un preset aleatorio de acuerdo al tipo de mensaje solicitado
        /// </summary>
        public static string GetRandomPreset(string presetType)
        {
            string[] templates = presetType switch
            {
                "wordofmouth" => WordOfMouthTemplates,
                "effect" => EffectDrivenTemplates,
                "quality" => QualityDrivenTemplates,
                "penalty" => PenaltyTemplates,
                "penalty_conservative" => PenaltyConservativeTemplates,
                "penalty_addict" => PenaltyAddictTemplates,
                "penalty_curious" => PenaltyCuriousTemplates,
                "penalty_aggressive" => PenaltyAggressiveTemplates,
                _ => PenaltyTemplates, // fallback
            };

            if (templates == null || templates.Length == 0)
                return "Lo siento, no hay presets disponibles.";

            return templates[UnityEngine.Random.Range(0, templates.Length)];
        }

        /// <summary>
        /// Obtiene el preset de penalización según la personalidad del NPC
        /// </summary>
        public static string GetPenaltyPreset(ConsumerType consumerType)
        {
            return consumerType switch
            {
                ConsumerType.Classic => GetRandomPreset("penalty_conservative"),
                ConsumerType.Addict => GetRandomPreset("penalty_addict"),
                ConsumerType.Experimenter => GetRandomPreset("penalty_curious"),
                ConsumerType.Gourmet => GetRandomPreset("penalty_aggressive"), // Gourmet es agresivo
                _ => GetRandomPreset("penalty"),
            };
        }
    }
}
