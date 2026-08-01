using System.Collections.Generic;
using Il2CppScheduleOne.Economy;
using UnityEngine;
using MelonLogger = SmartMarket.SmartMarketLogger;
using MelonLoader;

namespace SmartMarket.Core
{
    public enum ConsumerType
    {
        Classic,      // 40% - Busca lo seguro; prefiere marihuana con efectos tranquilos o mushrooms suaves.
        Experimenter, // 30% - Curioso: empieza con mezclas nuevas, prueba marihuanas y mushrooms distintas, luego busca cosas más duras.
        Addict,       // 20% - Mensajes secos y urgentes; acepta lo que sea, no quiere contraofertas ni demoras.
        Gourmet       // 10% - Prioriza la mejor calidad y potencia; puede querer solo marihuana o solo cocaína según su mundo.
    }

    public enum Neighborhood
    {
        Northtown,
        Westville,
        Downtown,
        Docks,
        Suburbia,
        Uptown
    }

    public enum ProductCategory
    {
        Any,
        Marijuana,
        Mushrooms,
        Cocaine,
        Other
    }

    public class ConsumerProfile
    {
        public string ID { get; private set; }
        public ConsumerType Type { get; private set; }
        public Neighborhood HomeNeighborhood { get; private set; }

        // Behavioral properties to drive simulation (added without changing external API)
        public ProductCategory PreferredCategory { get; internal set; } = ProductCategory.Any;
        public float AggressionLevel { get; internal set; } = 0.0f; // 0-1
        public bool WillAttackOnDenied { get; internal set; } = false;
        public bool RejectsCounterOffers { get; internal set; } = false;
        public float NoveltyTolerance { get; internal set; } = 0.5f; // tendency to try new things

        public ConsumerProfile(string id, ConsumerType type, Neighborhood neighborhood)
        {
            ID = id;
            Type = type;
            HomeNeighborhood = neighborhood;
        }
    }

    public static class ProfileManager
    {
        // Caché en memoria para mantener los perfiles persistentes durante la sesión
        private static Dictionary<string, ConsumerProfile> _profiles = new Dictionary<string, ConsumerProfile>();

        public static ConsumerProfile GetOrCreateProfile(Customer customer)
        {
            if (customer == null || customer.gameObject == null) return null;

            string id = customer.gameObject.name;

            if (_profiles.ContainsKey(id))
            {
                return _profiles[id];
            }

            // Generación determinista basada en el nombre (Hash) para que siempre sea igual en una partida
            int seed = id.GetHashCode();
            System.Random rand = new System.Random(seed);

            // Determinar Tipo (40% Classic, 30% Exp, 20% Addict, 10% Gourmet)
            int typeRoll = rand.Next(1, 101);
            ConsumerType type = ConsumerType.Classic;
            if (typeRoll > 40 && typeRoll <= 70) type = ConsumerType.Experimenter;
            else if (typeRoll > 70 && typeRoll <= 90) type = ConsumerType.Addict;
            else if (typeRoll > 90) type = ConsumerType.Gourmet;

            // Determinar Barrio (Distribuido equitativamente entre los 6 barrios reales)
            Neighborhood neighborhood = (Neighborhood)rand.Next(0, 6);

            ConsumerProfile newProfile = new ConsumerProfile(id, type, neighborhood);

            // Set behavioral defaults according to type
            switch (type)
            {
                case ConsumerType.Classic:
                    // Busca lo seguro: marihuana y mushrooms suaves preferidas
                    newProfile.PreferredCategory = ProductCategory.Marijuana;
                    newProfile.NoveltyTolerance = 0.2f;
                    newProfile.AggressionLevel = 0.1f;
                    newProfile.WillAttackOnDenied = false;
                    newProfile.RejectsCounterOffers = false;
                    break;
                case ConsumerType.Experimenter:
                    // Curioso: prueba mezclas, alta tolerancia a novedad
                    newProfile.PreferredCategory = ProductCategory.Any;
                    newProfile.NoveltyTolerance = 0.8f;
                    newProfile.AggressionLevel = 0.2f;
                    newProfile.WillAttackOnDenied = false;
                    newProfile.RejectsCounterOffers = false;
                    break;
                case ConsumerType.Addict:
                    // Urgente/directo: acepta casi cualquier cosa, no tolera contraofertas, puede reaccionar con violencia
                    newProfile.PreferredCategory = ProductCategory.Any;
                    newProfile.NoveltyTolerance = 0.1f;
                    newProfile.AggressionLevel = 0.9f;
                    newProfile.WillAttackOnDenied = true;
                    newProfile.RejectsCounterOffers = true;
                    break;
                case ConsumerType.Gourmet:
                    // Prioriza calidad; algunos gourmets son específicos de marihuana o cocaína
                    // Determinismo por semilla para mantener consistencia en la partida
                    int pick = rand.Next(0, 100);
                    newProfile.PreferredCategory = (pick < 60) ? ProductCategory.Marijuana : ProductCategory.Cocaine; // 60% marihuana, 40% cocaína
                    newProfile.NoveltyTolerance = 0.3f;
                    newProfile.AggressionLevel = 0.25f;
                    newProfile.WillAttackOnDenied = false;
                    newProfile.RejectsCounterOffers = false;
                    break;
                default:
                    newProfile.PreferredCategory = ProductCategory.Any;
                    newProfile.NoveltyTolerance = 0.5f;
                    newProfile.AggressionLevel = 0.2f;
                    newProfile.WillAttackOnDenied = false;
                    newProfile.RejectsCounterOffers = false;
                    break;
            }

            // Adjust by neighborhood standards (NPCs from higher-standard neighborhoods expect higher quality)
            var std = GetNeighborhoodStandard(neighborhood);
            if (std == NeighborhoodStandard.High)
            {
                // Slightly favor quality-oriented preferences
                newProfile.NoveltyTolerance = Mathf.Clamp01(newProfile.NoveltyTolerance - 0.1f);
            }
            else if (std == NeighborhoodStandard.Marginal)
            {
                // More tolerant to substitutes and novelty slightly lower
                newProfile.NoveltyTolerance = Mathf.Clamp01(newProfile.NoveltyTolerance + 0.05f);
            }

            _profiles.Add(id, newProfile);

            MelonLoader.MelonLogger.Msg($"[SmartMarket] Nuevo perfil generado para {id}: {type} de {neighborhood} (Pref:{newProfile.PreferredCategory} Agg:{newProfile.AggressionLevel} Novelty:{newProfile.NoveltyTolerance})");
            return newProfile;
        }

        public static bool IsConservative(ConsumerProfile profile)
        {
            return profile != null && profile.Type == ConsumerType.Classic;
        }

        public static bool IsCurious(ConsumerProfile profile)
        {
            return profile != null && profile.Type == ConsumerType.Experimenter;
        }

        public static bool IsAddicted(ConsumerProfile profile)
        {
            return profile != null && profile.Type == ConsumerType.Addict;
        }

        public static bool IsGourmet(ConsumerProfile profile)
        {
            return profile != null && profile.Type == ConsumerType.Gourmet;
        }

        public static string GetPersonalityDescription(ConsumerProfile profile)
        {
            if (profile == null)
                return "Cliente sin perfil definido.";

            switch (profile.Type)
            {
                case ConsumerType.Classic:
                    return "Busca lo seguro; prefiere marihuana con efectos tranquilos o mushrooms suaves.";
                case ConsumerType.Experimenter:
                    return "Empieza por cosas nuevas y mezcla variedades; puede subir a drogas más duras.";
                case ConsumerType.Addict:
                    return "Urgente y directo; acepta casi cualquier cosa, pero no tolera contraofertas.";
                case ConsumerType.Gourmet:
                    return "Prioriza lo mejor y más fuerte; algunos solo quieren marihuana, otros solo cocaína.";
                default:
                    return "Perfil de consumidor genérico.";
            }
        }

        public static NeighborhoodStandard GetNeighborhoodStandard(Neighborhood neighborhood)
        {
            switch (neighborhood)
            {
                case Neighborhood.Downtown:
                case Neighborhood.Docks:
                    return NeighborhoodStandard.Marginal;
                case Neighborhood.Northtown:
                case Neighborhood.Westville:
                    return NeighborhoodStandard.Normal;
                case Neighborhood.Suburbia:
                case Neighborhood.Uptown:
                    return NeighborhoodStandard.High;
                default:
                    return NeighborhoodStandard.Normal;
            }
        }

        public static string GetNeighborhoodStandardLabel(Neighborhood neighborhood)
        {
            switch (GetNeighborhoodStandard(neighborhood))
            {
                case NeighborhoodStandard.Marginal:
                    return "marginal";
                case NeighborhoodStandard.High:
                    return "de alta exigencia";
                default:
                    return "de estándares normales";
            }
        }
    }
}
