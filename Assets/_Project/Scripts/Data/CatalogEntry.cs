using UnityEngine;

public enum ItemShape
{
    Lampara,
    Lente,
    Antena,
    Tablilla,
    Frasco,
    Carrete,
    Giroscopio,
    Baliza,
    Casco,
    Credencial,
}

[CreateAssetMenu(fileName = "NuevaFicha", menuName = "Cielo Profundo/Ficha de catálogo")]
public class CatalogEntry : ScriptableObject
{
    public string entryTitle = "Objeto sin identificar";

    [Tooltip("Número de serie de Paralax. Es la pista silenciosa: el mismo aparece en cuerpos distintos.")]
    public string serial = "";

    [Tooltip("Quién habla en la ficha: la cuadrilla anterior (a mano) o Paralax (formulario).")]
    public string voice = "cuadrilla";

    [Tooltip("De dónde vino: quién lo dejó, para qué servía, qué pasó con quien lo usaba.")]
    [TextArea(4, 10)] public string description = "";

    [Tooltip("Qué objeto es. Define la forma que se construye en el mundo.")]
    public ItemShape shape = ItemShape.Lampara;

    [Tooltip("Color de la parte que emite luz o del detalle grabado.")]
    public Color accentColor = new Color(0.435f, 0.788f, 0.831f);

    // Tramo de la ruta donde aparece, de 1 a 3.
    //
    // Hace de nivel de rareza en el catálogo, y no es una etiqueta puesta encima: en
    // este juego el tramo YA significa lo que la rareza significa en otros. Las fichas
    // del tramo 1 son material de trabajo normal; las del 3 son la lámpara que sigue
    // encendida sin combustible y el casco cerrado por dentro. Colorear por tramo pone
    // a la vista una escala que el lore ya tenía.
    public int tramo = 1;
}
