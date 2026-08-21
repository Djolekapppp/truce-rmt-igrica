using Godot;

/// <summary>Doba u kome se rasa nalazi tokom jedne epohe.</summary>
public enum Age {
    Neutral,
    Golden,
    Dark,
}

/// <summary>
/// Raspored zlatnih i mracnih doba. Fiksan je i ponavlja se na svake tri
/// epohe, pa ga i klijent moze izracunati sam — sto je potrebno da bi se
/// iscrtala traka sa svih sest epoha, a ne samo tekuca.
///
/// Mora da ostane identican `common::golden_class` / `common::dark_class`
/// na serveru, jer server po istom pravilu suzava spilove.
/// </summary>
public static class Ages {
    public const int EpochCount = 6;

    /// <summary>Od ove epohe igraci dobijaju modifikatore.</summary>
    public const int ModifierEpoch = 4;

    private static readonly string[] GoldenByEpoch = {
        Factions.Elves, Factions.Humans, Factions.Dwarves,
    };

    private static readonly string[] DarkByEpoch = {
        Factions.Humans, Factions.Dwarves, Factions.Elves,
    };

    public static string GoldenClass(int epoch) =>
        epoch < 1 ? "" : GoldenByEpoch[(epoch - 1) % 3];

    public static string DarkClass(int epoch) =>
        epoch < 1 ? "" : DarkByEpoch[(epoch - 1) % 3];

    public static Age Of(string factionId, int epoch) {
        if (epoch < 1 || !Factions.IsValid(factionId)) {
            return Age.Neutral;
        }

        if (GoldenClass(epoch) == factionId) {
            return Age.Golden;
        }

        return DarkClass(epoch) == factionId ? Age.Dark : Age.Neutral;
    }

    public static string DisplayName(Age age) => age switch {
        Age.Golden => "Zlatno doba",
        Age.Dark => "Mračno doba",
        _ => "Mirno doba",
    };

    /// <summary>Kratka oznaka za traku epoha.</summary>
    public static string Badge(Age age) => age switch {
        Age.Golden => "▲",
        Age.Dark => "▼",
        _ => "·",
    };

    public static Color Tint(Age age) => age switch {
        Age.Golden => new Color(0.98f, 0.78f, 0.32f),
        Age.Dark => new Color(0.72f, 0.38f, 0.85f),
        _ => new Color(0.65f, 0.65f, 0.70f),
    };

    public static string Explain(Age age) => age switch {
        Age.Golden => "vuče iz jače polovine svog špila",
        Age.Dark => "vuče iz slabije polovine svog špila",
        _ => "vuče iz celog špila",
    };
}
