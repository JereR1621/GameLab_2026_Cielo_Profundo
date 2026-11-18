using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ItemRecord
{
    public string id;
    public string nombre;
    public string forma;
    public string color;
    public string serie;
    public int tramo;
    public string voz;
    public string texto;
}

[Serializable]
public class ItemRecordFile
{
    public ItemRecord[] items;
}

// Carga la pool de objetos desde Resources/items.json. Las fichas se arman en
// memoria: ya no hay assets sueltos, así el lore vive en un solo archivo editable.
public static class CatalogDatabase
{
    const string ResourcePath = "items";

    static ItemRecord[] cache;

    public static ItemRecord[] All
    {
        get
        {
            if (cache != null) return cache;

            var text = Resources.Load<TextAsset>(ResourcePath);
            if (text == null)
            {
                Debug.LogError($"No se encontró Resources/{ResourcePath}.json");
                cache = Array.Empty<ItemRecord>();
                return cache;
            }

            var file = JsonUtility.FromJson<ItemRecordFile>(text.text);
            cache = file?.items ?? Array.Empty<ItemRecord>();
            return cache;
        }
    }

    // Devuelve las fichas del tramo pedido. Si ese tramo está vacío se cae a la
    // pool entera, para que un dato mal cargado no deje el planeta sin objetos.
    public static CatalogEntry[] EntriesForTramo(int tramo)
    {
        var matching = new List<ItemRecord>();
        foreach (var record in All)
            if (record.tramo == tramo) matching.Add(record);

        if (matching.Count == 0)
        {
            Debug.LogWarning($"No hay objetos definidos para el tramo {tramo}; se usa la pool completa.");
            matching.AddRange(All);
        }

        var entries = new CatalogEntry[matching.Count];
        for (int i = 0; i < matching.Count; i++)
            entries[i] = BuildEntry(matching[i]);

        return entries;
    }

    // Las fichas que ya se transmitieron, en el orden del archivo, para el catálogo
    // de la nave. El id de cada ficha es su `name`: así lo deja BuildEntry.
    public static CatalogEntry[] RecoveredEntries()
    {
        var entries = new List<CatalogEntry>();
        foreach (var record in All)
            if (Expedition.HasRecovered(record.id)) entries.Add(BuildEntry(record));

        return entries.ToArray();
    }

    static CatalogEntry BuildEntry(ItemRecord record)
    {
        var entry = ScriptableObject.CreateInstance<CatalogEntry>();
        entry.name = record.id;
        entry.entryTitle = record.nombre;
        entry.description = record.texto;
        entry.serial = record.serie;
        entry.voice = record.voz;
        entry.tramo = Mathf.Clamp(record.tramo, 1, 3);

        if (!Enum.TryParse(record.forma, out ItemShape shape))
        {
            Debug.LogWarning($"Forma desconocida '{record.forma}' en el objeto '{record.id}'; se usa Lampara.");
            shape = ItemShape.Lampara;
        }
        entry.shape = shape;

        entry.accentColor = ColorUtility.TryParseHtmlString(record.color, out var color)
            ? color
            : new Color(0.435f, 0.788f, 0.831f);

        return entry;
    }
}
