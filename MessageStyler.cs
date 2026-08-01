using System;
using System.Collections.Generic;

namespace SmartMarket.Core
{
    // Centralized helper to apply consistent coloring/styling to dynamic placeholders in generated messages.
    // Colors are defined in SmartMarketConfig and serialized to the config file so they can be tuned.
    public static class MessageStyler
    {
        private static string WrapColor(string hexColor, string text, bool applyColor)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            if (!applyColor || string.IsNullOrEmpty(hexColor)) return text;
            // Use Unity/TextMeshPro rich text color tag (both TMPro and legacy Text support this form).
            return $"<color={hexColor}>{text}</color>";
        }

        // Effect-specific color mapping (based on vanilla game UI)
        private static readonly Dictionary<string, string> EffectColors = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            // Primary effects - blues/cyans
            { "Athletic", "#5DADE2" },              // Light blue
            { "Anti-Gravity", "#5DADE2" },          // Light blue
            { "Electrifying", "#00CED1" },          // Cyan
            { "Focused", "#00BCD4" },               // Light cyan
            { "Sedating", "#3F51B5" },              // Dark blue
            { "Slippery", "#00BCD4" },              // Light cyan
            { "Schizophrenic", "#3F51B5" },         // Dark blue

            // Oranges/yellows/warm tones
            { "Balding", "#F39C12" },               // Orange
            { "Calming", "#FF9800" },               // Light orange
            { "Cyclopsean", "#FF7F50" },            // Coral/orange
            { "Disorienting", "#FF6B6B" },          // Red-orange
            { "Gingeritis", "#FF7F50" },            // Coral orange
            { "Long-Faced", "#FFC107" },            // Gold/yellow
            { "Laxative", "#FF7F50" },              // Coral orange
            { "Tropic Thunder", "#FF9800" },        // Dark orange
            { "Euphoric", "#FFC107" },              // Gold
            { "Explosive", "#FF4444" },             // Bright red-orange

            // Greens/limes
            { "Energizing", "#CDDC39" },            // Lime green
            { "Glowing", "#CDDC39" },               // Lime green
            { "Refreshing", "#4CAF50" },            // Green
            { "Smelly", "#4CAF50" },                // Green
            { "Toxic", "#4CAF50" },                 // Green
            { "Zombifying", "#CDDC39" },            // Lime green
            { "Shrinking", "#00CED1" },             // Cyan

            // Pinks/magentas
            { "Calorie-Dense", "#E91E63" },         // Magenta
            { "Jennerising", "#FF1493" },           // Deep pink
            { "Munchies", "#FF6B6B" },              // Red
            { "Paranoia", "#FF69B4" },              // Hot pink
            { "Spicy", "#FF6B6B" },                 // Red
            { "Thought-Provoking", "#FF69B4" },     // Hot pink

            // Grays/neutrals
            { "Foggy", "#9E9E9E" },                 // Gray
            { "Sneaky", "#808080" },                // Dark gray

            // Seizure-Inducing (yellow)
            { "Seizure-Inducing", "#FFEB3B" },      // Bright yellow

            // Fallback for unknown effects
            { "default", "#8e44ad" },               // Purple (original default)
        };

        // Note: applyColor defaults to false for SMS messages, preventing HTML tags in the phone UI
        public static string ColorizeProduct(string text, bool applyColor = false) => WrapColor(SmartMarketConfig.ProductColorHex, text, applyColor);
        
        /// <summary>
        /// Colorize an effect with its specific color from the vanilla game
        /// </summary>
        public static string ColorizeEffect(string effectName, bool applyColor = false)
        {
            if (string.IsNullOrEmpty(effectName))
                return effectName ?? string.Empty;

            string color;
            if (EffectColors.TryGetValue(effectName, out color))
            {
                return WrapColor(color, effectName, applyColor);
            }

            // Fallback to default effect color if not found
            return WrapColor(EffectColors["default"], effectName, applyColor);
        }

        public static string ColorizeQuality(string text, bool applyColor = false) => WrapColor(SmartMarketConfig.QualityColorHex, text, applyColor);
        public static string ColorizeQuantity(string text, bool applyColor = false) => WrapColor(SmartMarketConfig.QuantityColorHex, text, applyColor);
        public static string ColorizePrice(string text, bool applyColor = false) => WrapColor(SmartMarketConfig.PriceColorHex, text, applyColor);

        // Generic entrypoint for future placeholder types
        public static string Colorize(string placeholderType, string text, bool applyColor = false)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            switch ((placeholderType ?? string.Empty).ToLowerInvariant())
            {
                case "product": return ColorizeProduct(text, applyColor);
                case "effect": return ColorizeEffect(text, applyColor);
                case "quality": return ColorizeQuality(text, applyColor);
                case "quantity": return ColorizeQuantity(text, applyColor);
                case "price": return ColorizePrice(text, applyColor);
                default: return text; // unknown placeholder: leave unchanged
            }
        }
    }
}
