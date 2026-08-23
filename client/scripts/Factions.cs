using System.Collections.Generic;
using Godot;

/// <summary>
/// Tri rase. Id-jevi su isti stringovi koje server ocekuje u Ready poruci
/// i koje nosi polje "class" u cards.json.
/// </summary>
public static class Factions {
    public const string Elves = "elves";
    public const string Dwarves = "dwarves";
    public const string Humans = "humans";

    public static readonly string[] All = { Elves, Dwarves, Humans };

    public static string DisplayName(string id) => id switch {
        Elves => "Vilenjaci",
        Dwarves => "Patuljci",
        Humans => "Ljudi",
        _ => "—",
    };

    /// <summary>Resurs (zadovoljstvo gradjana) koji ova rasa vodi.</summary>
    public static string ResourceName(string id) => id switch {
        Elves => "Priroda",
        Dwarves => "Nauka",
        Humans => "Vera",
        _ => "—",
    };

    public static string Motto(string id) => id switch {
        Elves => "Cuvari suma i zivog sveta",
        Dwarves => "Kovaci, graditelji, izumitelji",
        Humans => "Hramovi, zakoni i zavet",
        _ => "",
    };

    public static Color Tint(string id) => id switch {
        Elves => new Color(0.35f, 0.78f, 0.45f),
        Dwarves => new Color(0.85f, 0.42f, 0.32f),
        Humans => new Color(0.42f, 0.60f, 0.92f),
        _ => new Color(0.65f, 0.65f, 0.70f),
    };

    public static bool IsValid(string id) =>
        id == Elves || id == Dwarves || id == Humans;

    /// <summary>
    /// Ikonice su crtane crno, pa se uvek moraju bojiti. Bele se najbolje
    /// vide na tamnoj pozadini, a boja rase ionako stoji na tekstu pored.
    /// </summary>
    public static readonly Color IconColor = new(1f, 1f, 1f);

    private static readonly Dictionary<string, Texture2D> IconCache = new();

    /// <summary>
    /// Ikonica rase: vilenjaci luk, patuljci sekira, ljudi mac.
    ///
    /// Trazi se i .svg i .png pod istim imenom, pa se nacrtane ikonice mogu
    /// zameniti svojima bez diranja koda. Ako fajla nema, vraca null i
    /// TextureRect jednostavno ne iscrta nista.
    /// </summary>
    public static Texture2D Icon(string id) {
        if (IconCache.TryGetValue(id, out var cached)) {
            return cached;
        }

        Texture2D texture = null;

        foreach (var extension in new[] { ".svg", ".png" }) {
            string path = $"res://assets/icons/{id}{extension}";

            if (ResourceLoader.Exists(path)) {
                texture = ResourceLoader.Load<Texture2D>(path);
                break;
            }
        }

        IconCache[id] = texture;
        return texture;
    }

    /// <summary>Ikonica spremna da stane ispred imena rase.</summary>
    public static TextureRect IconRect(string id, int size) => new() {
        Texture = Icon(id),
        CustomMinimumSize = new Vector2(size, size),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        SelfModulate = IconColor,
        SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
    };
}
