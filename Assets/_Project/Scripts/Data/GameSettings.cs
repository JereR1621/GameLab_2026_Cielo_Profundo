using UnityEngine;

// Ajustes del jugador. Persisten en PlayerPrefs, igual que el progreso de la ruta.
//
// Sobre el volumen: el general mueve el AudioListener y el de música lo lee
// AmbientMusic cada cuadro, así que esos dos se oyen. El de efectos todavía se guarda
// nada más, a la espera de que existan efectos a los que aplicárselo.
public static class GameSettings
{
    const string MasterKey = "cielo_profundo_vol_general";
    const string EffectsKey = "cielo_profundo_vol_efectos";
    const string MusicKey = "cielo_profundo_vol_musica";
    const string SensitivityKey = "cielo_profundo_sensibilidad";
    const string InvertKey = "cielo_profundo_invertir_y";
    const string FullscreenKey = "cielo_profundo_pantalla_completa";

    static bool applied;

    public static float MasterVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, 0.8f));
        set { PlayerPrefs.SetFloat(MasterKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); Apply(); }
    }

    public static float EffectsVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsKey, 0.8f));
        set { PlayerPrefs.SetFloat(EffectsKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }

    // Este sí se aplica de verdad: AmbientMusic lo lee cada cuadro, así que moverlo en
    // opciones se oye al instante mientras suena la música del menú.
    public static float MusicVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 0.7f));
        set { PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }

    public static float MouseSensitivity
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 3f), 0.5f, 10f);
        set { PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(value, 0.5f, 10f)); PlayerPrefs.Save(); }
    }

    public static bool InvertY
    {
        get => PlayerPrefs.GetInt(InvertKey, 0) == 1;
        set { PlayerPrefs.SetInt(InvertKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
        set { PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0); PlayerPrefs.Save(); Apply(); }
    }

    public static void Apply()
    {
        AudioListener.volume = MasterVolume;
        if (Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
        applied = true;
    }

    // Para las escenas a las que se puede entrar sin pasar por el menú, que en el
    // editor es todo el tiempo.
    public static void EnsureApplied()
    {
        if (!applied) Apply();
    }

    public static void ResetToDefaults()
    {
        PlayerPrefs.DeleteKey(MasterKey);
        PlayerPrefs.DeleteKey(EffectsKey);
        PlayerPrefs.DeleteKey(MusicKey);
        PlayerPrefs.DeleteKey(SensitivityKey);
        PlayerPrefs.DeleteKey(InvertKey);
        PlayerPrefs.DeleteKey(FullscreenKey);
        PlayerPrefs.Save();
        Apply();
    }
}
