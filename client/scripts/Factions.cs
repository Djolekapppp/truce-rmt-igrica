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
}
