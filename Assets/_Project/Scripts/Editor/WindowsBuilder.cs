using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Compilación del ejecutable de Windows para la entrega.
//
// Se hace por script y no a mano por el mismo motivo que las escenas: así la entrega es
// repetible y no depende de que alguien recuerde qué casillas marcar en el diálogo.
//
// Las escenas se toman de la lista del proyecto y solo las habilitadas, para que la de
// pruebas —que está desmarcada— no se cuele en el juego entregado.
public static class WindowsBuilder
{
    const string Carpeta = "Build/CieloProfundo";
    const string Ejecutable = "Cielo Profundo.exe";

    [MenuItem("Cielo Profundo/Compilar para Windows")]
    public static void Build()
    {
        // Identidad del juego. Además del nombre de la ventana y del ejecutable, decide
        // dónde se guardan los ajustes y el progreso, así que fijarlo antes de entregar
        // evita que la partida del jurado se mezcle con la de las pruebas.
        PlayerSettings.companyName = "Paralax";
        PlayerSettings.productName = "Cielo Profundo";
        PlayerSettings.bundleVersion = "1.0";

        // Ventana sin bordes a resolución de escritorio: es lo más amable para quien lo
        // abre una sola vez. Pantalla completa exclusiva da problemas al cambiar de
        // aplicación, y en ventana pequeña la interfaz se lee mal.
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultIsNativeResolution = true;
        PlayerSettings.runInBackground = false;

        // Sin la pantalla de configuración al arrancar: el juego ya tiene su menú de
        // opciones y un diálogo previo solo confunde.
        PlayerSettings.SetPropertyInt("ScriptingBackend", (int)ScriptingImplementation.Mono2x,
                                      BuildTargetGroup.Standalone);

        var escenas = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (escenas.Length == 0)
        {
            Debug.LogError("No hay escenas habilitadas en el build. Nada que compilar.");
            return;
        }

        Debug.Log($"Compilando {escenas.Length} escenas: {string.Join(", ", escenas)}");

        Directory.CreateDirectory(Carpeta);

        var opciones = new BuildPlayerOptions
        {
            scenes = escenas,
            locationPathName = Path.Combine(Carpeta, Ejecutable),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        BuildReport informe = BuildPipeline.BuildPlayer(opciones);

        if (informe.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"BUILD OK -> {Path.GetFullPath(Path.Combine(Carpeta, Ejecutable))} " +
                      $"({informe.summary.totalSize / (1024 * 1024)} MB, " +
                      $"{informe.summary.totalTime.TotalSeconds:0} s)");
        }
        else
        {
            Debug.LogError($"BUILD FALLIDO: {informe.summary.result}, " +
                           $"{informe.summary.totalErrors} errores.");
        }
    }
}
