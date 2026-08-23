using System;
using Godot;

/// <summary>
/// Modifikatori koje igraci dobijaju od 4. epohe. Server dodeljuje po jedan
/// svakom igracu i salje mu ga privatno (`Modifier` poruka).
///
/// `Apply` mora da racuna isto sto i `GameState::modify_value` na serveru —
/// ovde se koristi samo za prikaz, ali ako se razidje, igrac vidi jedan
/// broj a partija dobije drugi.
/// </summary>
public static class Modifiers {
    public const string Agitated = "agitated";
    public const string Skeptical = "skeptical";
    public const string Hyped = "hyped";

    public static string DisplayName(string id) => id switch {
        Agitated => "Uznemireni",
        Skeptical => "Skeptični",
        Hyped => "Ushićeni",
        _ => "—",
    };

    public static string Explain(string id, float value) {
        int percent = (int)MathF.Round(MathF.Abs(value - 1f) * 100f);

        return id switch {
            Agitated => $"Negativni efekti su ti {percent}% jači. Pozitivni ostaju isti.",
            Skeptical => $"Pozitivni efekti su ti {percent}% slabiji. Negativni ostaju isti.",
            Hyped => $"Svi efekti su ti {percent}% jači, i dobri i loši.",
            _ => "",
        };
    }

    public static Color Tint(string id) => id switch {
        Agitated => new Color(0.95f, 0.55f, 0.35f),
        Skeptical => new Color(0.55f, 0.68f, 0.95f),
        Hyped => new Color(0.95f, 0.42f, 0.62f),
        _ => new Color(0.65f, 0.65f, 0.70f),
    };

    /// <summary>
    /// Vrednost karte posle modifikatora. Prati serversku logiku:
    /// "agitated" pojacava samo negativne, "skeptical" slabi samo pozitivne,
    /// "hyped" pojacava sve.
    /// </summary>
    public static int Apply(int value, string modifier, float factor) {
        bool applies = modifier switch {
            Agitated => value < 0,
            Skeptical => value > 0,
            Hyped => true,
            _ => false,
        };

        if (!applies) {
            return value;
        }

        // Rust-ov f32::round zaokruzuje pola dalje od nule; C#-ov podrazumevani
        // Math.Round zaokruzuje na parno, pa se mora reci eksplicitno.
        return (int)MathF.Round(value * factor, MidpointRounding.AwayFromZero);
    }
}
