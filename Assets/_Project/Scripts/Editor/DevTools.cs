using UnityEditor;
using UnityEngine;

// Atajos para probar. Nada de esto llega al juego: son cosas que en una partida normal
// se resuelven jugando, pero que probando la misma escena treinta veces al día hacen
// falta a cada rato.
public static class DevTools
{
    [MenuItem("Cielo Profundo/Pruebas: olvidar pantallas de inicio")]
    public static void OlvidarFichas()
    {
        Expedition.CreatureBriefSeen = false;
        Expedition.InductionSeen = false;
        Expedition.ControlsBriefSeen = false;
        Debug.Log("Fichas de criatura y controles marcadas como no vistas: vuelven a aparecer en el próximo descenso.");
    }

    [MenuItem("Cielo Profundo/Pruebas: reiniciar todo el progreso")]
    public static void ReiniciarProgreso()
    {
        Expedition.ResetAll();
        Debug.Log("Progreso borrado: catálogo vacío, ruta en el primer cuerpo, ficha sin ver.");
    }

    [MenuItem("Cielo Profundo/Pruebas: ver estado guardado")]
    public static void VerEstado()
    {
        Debug.Log($"Ruta: cuerpo {RouteDatabase.CurrentIndex + 1} de {RouteDatabase.Bodies.Length}\n" +
                  $"Catálogo: {Expedition.Recovered.Count} ficha(s)\n" +
                  $"Estudios fallidos: {Expedition.FailedSurveys}\n" +
                  $"Dificultad del descenso: ×{Expedition.DescentDifficulty:0.#}\n" +
                  $"Ficha de criatura vista: {(Expedition.CreatureBriefSeen ? "sí" : "no")}\n" +
                  $"Controles vistos: {(Expedition.ControlsBriefSeen ? "sí" : "no")}");
    }
}
