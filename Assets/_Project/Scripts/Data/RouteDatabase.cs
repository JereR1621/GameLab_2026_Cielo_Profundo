using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BodyRecord
{
    public string id;
    public string nombre;
    public string clase;
    [Tooltip("Estrella, Planeta, Nebulosa, Sin clasificar. Define el carácter del cuerpo.")]
    public string tipo;
    public int tramo;
    public int semilla;

    [Tooltip("Si entra en la ruta jugable. Los cuerpos en false se quedan en el archivo con todo su lore, pero no aparecen en el selector.")]
    public bool enRuta;

    [Header("Atmósfera: fija por cuerpo, no sorteada")]
    public string cieloAlto;
    public string cieloHorizonte;
    public string colorEstrella;
    public string colorTerreno;
    [Tooltip("Las estrellas van alto (1.3 a 2.2) y los planetas bajo (0.2 a 0.7).")]
    public float luzIntensidad;
    [Tooltip("Al revés de lo que parece: cuanta más luz, más cerrada la bruma. En una estrella estás bañado en claridad y no ves nada.")]
    public float nieblaDensidad;

    public int itemsMin;
    public int itemsMax;
    public string transmision;
    public string nota;

    public Color ParseColor(string hex, Color fallback)
    {
        return ColorUtility.TryParseHtmlString(hex, out var color) ? color : fallback;
    }
}

[Serializable]
public class RouteFile
{
    public BodyRecord[] cuerpos;
}

// La ruta de Paralax: los cuerpos asignados, en orden. Cada uno fija su semilla,
// así un mismo cuerpo siempre es el mismo lugar cuando volvés.
public static class RouteDatabase
{
    const string ResourcePath = "ruta";
    const string ProgressKey = "paralax_indice_ruta";
    const string UnlockedKey = "paralax_cuerpos_abiertos";

    static BodyRecord[] cache;

    public static BodyRecord[] Bodies
    {
        get
        {
            if (cache != null) return cache;

            var text = Resources.Load<TextAsset>(ResourcePath);
            if (text == null)
            {
                Debug.LogError($"No se encontró Resources/{ResourcePath}.json");
                cache = Array.Empty<BodyRecord>();
                return cache;
            }

            var file = JsonUtility.FromJson<RouteFile>(text.text);
            var todos = file?.cuerpos ?? Array.Empty<BodyRecord>();

            // Solo los marcados entran en la ruta jugable. Los demás se quedan en el
            // archivo con su lore intacto: para una jam doce cuerpos son demasiados, pero
            // borrarlos sería tirar texto que después no se recupera. Cambiar un enRuta a
            // true los devuelve.
            var enRuta = new List<BodyRecord>();
            foreach (var body in todos)
                if (body.enRuta) enRuta.Add(body);

            if (enRuta.Count == 0)
            {
                Debug.LogWarning("Ningún cuerpo tiene enRuta en true: se usan los doce.");
                cache = todos;
                return cache;
            }

            cache = enRuta.ToArray();
            return cache;
        }
    }

    // Cuántos cuerpos hay disponibles. Empieza en uno y sube al despegar de cada cuerpo.
    //
    // Va aparte de CurrentIndex a propósito: antes ese índice hacía los dos trabajos —
    // a dónde vas Y hasta dónde llegaste— así que elegir un cuerpo lejano en la lista te
    // marcaba como si lo hubieras superado. Por eso todo estaba disponible desde el
    // principio y no se entendía que hubiera progresión.
    public static int UnlockedCount
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, 1), 1, Mathf.Max(1, Bodies.Length));
        set
        {
            PlayerPrefs.SetInt(UnlockedKey, Mathf.Clamp(value, 1, Mathf.Max(1, Bodies.Length)));
            PlayerPrefs.Save();
        }
    }

    public static bool IsUnlocked(int index) => index >= 0 && index < UnlockedCount;

    // Índice del cuerpo elegido ahora mismo. Solo dice a dónde vas: no marca progreso.
    public static int CurrentIndex
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(ProgressKey, 0), 0, Mathf.Max(0, Bodies.Length - 1));
        set
        {
            PlayerPrefs.SetInt(ProgressKey, Mathf.Clamp(value, 0, Mathf.Max(0, Bodies.Length - 1)));
            PlayerPrefs.Save();
        }
    }

    public static BodyRecord Current => Bodies.Length > 0 ? Bodies[CurrentIndex] : null;

    public static bool HasNext => CurrentIndex < Bodies.Length - 1;

    // Al despegar de un cuerpo se abre el siguiente. Se usa el máximo y no un incremento
    // ciego: volver a bajar a un cuerpo ya superado no debe cerrar los que vienen.
    public static void Advance()
    {
        UnlockedCount = Mathf.Max(UnlockedCount, CurrentIndex + 2);
        if (HasNext) CurrentIndex++;
    }

    public static void ResetProgress()
    {
        CurrentIndex = 0;
        UnlockedCount = 1;
    }
}
