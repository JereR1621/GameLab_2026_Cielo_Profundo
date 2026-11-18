using System.Collections.Generic;
using UnityEngine;

// Lo que sobrevive a un cambio de escena. Todo lo demás muere al cargar el planeta
// y vuelve a nacer al volver a la nave.
//
// Va en PlayerPrefs por el mismo motivo que RouteDatabase.CurrentIndex: es lo único
// que persiste sin tener que arrastrar un objeto con DontDestroyOnLoad entre escenas
// que se construyen por separado desde el editor.
public static class Expedition
{
    const string RecoveredKey = "cielo_profundo_recuperadas";
    const string DifficultyKey = "cielo_profundo_dificultad";
    const string AttemptsKey = "cielo_profundo_intentos_fallidos";

    // La ficha de la criatura se muestra una sola vez por partida. La clave vive acá y no
    // en la propia ficha para que empezar una ruta nueva la vuelva a mostrar: si no,
    // quien la vio una vez no la recupera nunca, ni empezando de cero.
    const string CreatureBriefKey = "cielo_profundo_ficha_arania";
    const string ControlsBriefKey = "cielo_profundo_ficha_controles";
    const string RouteDoneKey = "cielo_profundo_ruta_completa";
    const string InductionKey = "cielo_profundo_induccion";

    // ── Economía ────────────────────────────────────────────────────────────────
    //
    // El dinero sale de transmitir fichas al despegar, que es literalmente lo que la
    // empresa te pide en cada comunicado. Eso convierte a los coleccionables en el motor
    // del juego en vez de decorado narrativo, y de paso crea la tensión que le faltaba:
    // quedarte a juntar una más es más ruido, más arañas y más riesgo, contra despegar
    // ahora con lo que llevas.
    const string CreditsKey = "cielo_profundo_creditos";
    const string FlaresKey = "cielo_profundo_bengalas";
    const string MaxHeartsKey = "cielo_profundo_corazones";

    // Preparación: se compra en la nave y se gasta en el próximo descenso.
    const string QuietStepKey = "cielo_profundo_prep_suela";
    const string StaminaPrepKey = "cielo_profundo_prep_aguante";

    public static int Credits
    {
        get => Mathf.Max(0, PlayerPrefs.GetInt(CreditsKey, 0));
        set { PlayerPrefs.SetInt(CreditsKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    // Las bengalas ya no se regeneran al entrar al mapa: son un recurso de la ruta
    // entera. Empiezas con cinco y las que gastes hay que reponerlas comprando.
    public static int Flares
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(FlaresKey, 5), 0, 99);
        set { PlayerPrefs.SetInt(FlaresKey, Mathf.Clamp(value, 0, 99)); PlayerPrefs.Save(); }
    }

    // Capacidad, no vida actual: siempre bajas con el traje entero. Comprar corazones
    // sube el techo, no te cura.
    public static int MaxHearts
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(MaxHeartsKey, 3), 1, 5);
        set { PlayerPrefs.SetInt(MaxHeartsKey, Mathf.Clamp(value, 1, 5)); PlayerPrefs.Save(); }
    }

    public static bool QuietStepReady
    {
        get => PlayerPrefs.GetInt(QuietStepKey, 0) == 1;
        set { PlayerPrefs.SetInt(QuietStepKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool StaminaPrepReady
    {
        get => PlayerPrefs.GetInt(StaminaPrepKey, 0) == 1;
        set { PlayerPrefs.SetInt(StaminaPrepKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // Se gastan al aterrizar: valen para un descenso y no para la partida.
    public static void ConsumePreparations()
    {
        QuietStepReady = false;
        StaminaPrepReady = false;
    }

    public static bool CreatureBriefSeen
    {
        get => PlayerPrefs.GetInt(CreatureBriefKey, 0) == 1;
        set { PlayerPrefs.SetInt(CreatureBriefKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool ControlsBriefSeen
    {
        get => PlayerPrefs.GetInt(ControlsBriefKey, 0) == 1;
        set { PlayerPrefs.SetInt(ControlsBriefKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // La ruta llegó a su final y el jugador vio el cierre. Solo sirve para que el menú
    // principal no ofrezca «continuar ruta» hacia un cuerpo que ya está terminado.
    public static bool RouteCompleted
    {
        get => PlayerPrefs.GetInt(RouteDoneKey, 0) == 1;
        set { PlayerPrefs.SetInt(RouteDoneKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // La bienvenida de la empresa, que se ve una vez por ruta.
    public static bool InductionSeen
    {
        get => PlayerPrefs.GetInt(InductionKey, 0) == 1;
        set { PlayerPrefs.SetInt(InductionKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    static HashSet<string> recoveredCache;

    // Fichas recuperadas en toda la ruta, por id de catálogo. El contador del HUD
    // cuenta las de la jornada; esto es el catálogo completo, el que se revisa con C.
    public static IReadOnlyCollection<string> Recovered => Load();

    public static bool HasRecovered(string id) => !string.IsNullOrEmpty(id) && Load().Contains(id);

    // Se llama al despegar, no al recoger: si mueres en la superficie pierdes lo de
    // esa jornada. Es lo único que hace que bajar tenga consecuencia.
    public static void Commit(IEnumerable<string> ids)
    {
        var set = Load();
        foreach (var id in ids)
            if (!string.IsNullOrEmpty(id)) set.Add(id);

        PlayerPrefs.SetString(RecoveredKey, string.Join(",", set));
        PlayerPrefs.Save();
    }

    // Multiplicador de dificultad del próximo descenso. Lo fija el estudio previo:
    // identificar bien el cuerpo lo deja en 1, bajar a ciegas lo sube.
    public static float DescentDifficulty
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(DifficultyKey, 1f), 1f, 4f);
        set
        {
            PlayerPrefs.SetFloat(DifficultyKey, Mathf.Clamp(value, 1f, 4f));
            PlayerPrefs.Save();
        }
    }

    // Intentos fallidos del estudio previo en el cuerpo actual. Se reinicia al
    // cambiar de destino.
    public static int FailedSurveys
    {
        get => PlayerPrefs.GetInt(AttemptsKey, 0);
        set
        {
            PlayerPrefs.SetInt(AttemptsKey, Mathf.Max(0, value));
            PlayerPrefs.Save();
        }
    }

    public static void ResetSurvey()
    {
        FailedSurveys = 0;
        DescentDifficulty = 1f;
    }

    public static void ResetAll()
    {
        recoveredCache = null;
        PlayerPrefs.DeleteKey(RecoveredKey);
        PlayerPrefs.DeleteKey(CreatureBriefKey);
        PlayerPrefs.DeleteKey(ControlsBriefKey);
        PlayerPrefs.DeleteKey(RouteDoneKey);
        PlayerPrefs.DeleteKey(InductionKey);
        PlayerPrefs.DeleteKey(CreditsKey);
        PlayerPrefs.DeleteKey(FlaresKey);
        PlayerPrefs.DeleteKey(MaxHeartsKey);
        PlayerPrefs.DeleteKey(QuietStepKey);
        PlayerPrefs.DeleteKey(StaminaPrepKey);
        ResetSurvey();
        RouteDatabase.ResetProgress();
    }

    static HashSet<string> Load()
    {
        if (recoveredCache != null) return recoveredCache;

        recoveredCache = new HashSet<string>();
        string raw = PlayerPrefs.GetString(RecoveredKey, "");
        if (string.IsNullOrEmpty(raw)) return recoveredCache;

        foreach (var id in raw.Split(','))
            if (!string.IsNullOrEmpty(id)) recoveredCache.Add(id);

        return recoveredCache;
    }
}
