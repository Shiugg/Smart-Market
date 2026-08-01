using System;
using System.Collections.Generic;

namespace SmartMarket.Core
{
    [Serializable]
    public class EventsConfig
    {
        public RumorEscasezConfig rumorEscasez = new RumorEscasezConfig();
        public ProductoViralConfig productoViral = new ProductoViralConfig();
        public FestivalBarrioConfig festivalBarrio = new FestivalBarrioConfig();
        public OperativoPolicialConfig operativoPolicial = new OperativoPolicialConfig();
        public CambioEstacionalConfig cambioEstacional = new CambioEstacionalConfig();
    }

    [Serializable]
    public class RumorEscasezConfig
    {
        public bool enabled = true;
        public float probability = 8.0f; // % por día
        public int durationMin = 2;
        public int durationMax = 3;
        public float intensity = 4.0f; // 0-10 scale
        public string targetProductId = ""; // empty = random
        public int cooldownDays = 7;
    }

    [Serializable]
    public class ProductoViralConfig
    {
        public bool enabled = true;
        public float probability = 5.0f; // % por día
        public int durationMin = 2;
        public int durationMax = 3;
        public float intensity = 7.5f; // 0-10 scale
        public string targetProductId = ""; // empty = random
        public int cooldownDays = 14;
    }

    [Serializable]
    public class FestivalBarrioConfig
    {
        public bool enabled = true;
        public float probability = 1.0f; // % por día por zona
        public int durationMin = 1;
        public int durationMax = 1;
        public float intensity = 6.0f; // 0-10 scale
        public List<string> targetZoneIds = new List<string>(); // empty = all zones
        public int cooldownDays = 21;
    }

    [Serializable]
    public class OperativoPolicialConfig
    {
        public bool enabled = true;
        public float baseProbability = 1.0f; // % por día por zona
        public float maxProbability = 10.0f; // % máximo según ventas
        public int lookbackDays = 3; // días para evaluar ventas
        public float intensity = 5.0f; // 0-10 scale
        public int durationMin = 1;
        public int durationMax = 2;
        public List<string> targetZoneIds = new List<string>(); // empty = dynamic by sales
        public int cooldownDays = 14;
        public float exclusionThreshold = 8.0f; // % para romper mutual exclusion
    }

    [Serializable]
    public class CambioEstacionalConfig
    {
        public bool enabled = false; // disabled by default unless seasons exist
        public float intensity = 2.5f; // 0-10 scale
        // trigger is calendar-based, not probability-based
        public int exclusionWindowDays = 3; // standard mutual exclusion window

        // Multipliers applied during seasonal change: calming products multiplied, stimulants multiplied
        public float calmingMultiplier = 1.5f; // >1 favors calming products
        public float stimulantMultiplier = 0.5f; // <1 penalizes stimulants
    }
}
