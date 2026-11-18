using UnityEngine;

// Sonidos de interfaz. Dos nada más: confirmar y moverse.
//
// Se arma solo la primera vez que alguien pide un sonido, y los clips salen de
// Resources, igual que items.json y ruta.json. Así ningún panel necesita que le cableen
// un AudioSource desde el constructor de escena — son siete pantallas y serían siete
// cables que se pueden olvidar.
//
// Al cambiar de escena el objeto muere y la referencia queda en null, así que la
// siguiente llamada lo vuelve a crear. No hace falta DontDestroyOnLoad ni limpieza.
public static class UISound
{
    static AudioSource source;
    static AudioClip clickClip;
    static AudioClip moveClip;
    static bool buscado;

    // Confirmar, abrir, cerrar: cualquier cosa que sea una decisión.
    public static void Click() => Reproducir(clickClip);

    // Cambiar de fila, subir o bajar un valor: cualquier cosa que sea recorrer.
    public static void Move() => Reproducir(moveClip, 0.8f);

    static void Reproducir(AudioClip clip, float escala = 1f)
    {
        Preparar();
        if (source == null || clip == null) return;

        float volumen = GameSettings.EffectsVolume * escala;
        if (volumen <= 0.001f) return;

        // PlayOneShot y no Play: dos sonidos seguidos se superponen en vez de cortarse,
        // que es lo que pasa al recorrer una lista rápido.
        source.PlayOneShot(clip, volumen);
    }

    static void Preparar()
    {
        if (!buscado)
        {
            clickClip = Resources.Load<AudioClip>("Audio/click");
            moveClip = Resources.Load<AudioClip>("Audio/mover");
            buscado = true;

            if (clickClip == null || moveClip == null)
                Debug.LogWarning("UISound: faltan clips en Resources/Audio (click.wav, mover.wav).");
        }

        if (source != null) return;

        var go = new GameObject("UISound") { hideFlags = HideFlags.HideAndDontSave };
        source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // 2D: la interfaz no está en ningún lugar del mundo
    }
}
