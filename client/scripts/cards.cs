using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

/// <summary>
/// Jedna karta, ista struktura kao unos u data/cards.json.
/// </summary>
public class Card {
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("epoch")]
    public int Epoch { get; set; }

    [JsonPropertyName("class")]
    public string Class { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("elves")]
    public int Elves { get; set; }

    [JsonPropertyName("dwarves")]
    public int Dwarves { get; set; }

    [JsonPropertyName("humans")]
    public int Humans { get; set; }
}

/// <summary>
/// Lokalna kopija spila, samo za prikaz. Server salje kljuceve karata
/// ("Card 1"), a ovde ih prevodimo u ime, opis i efekte.
///
/// data/cards.json mora da ostane identican serverskom src/cards/cards.json.
/// </summary>
public static class CardDatabase {
    private const string CardsPath = "res://data/cards.json";

    private static Dictionary<string, Card> _cards;

    public static IReadOnlyDictionary<string, Card> Cards {
        get {
            EnsureLoaded();
            return _cards;
        }
    }

    /// <summary>Vraca null ako karta ne postoji, umesto da baca izuzetak.</summary>
    public static Card Get(string key) {
        EnsureLoaded();
        return _cards.TryGetValue(key, out var card) ? card : null;
    }

    private static void EnsureLoaded() {
        if (_cards != null) {
            return;
        }

        _cards = new Dictionary<string, Card>();

        using var file = FileAccess.Open(CardsPath, FileAccess.ModeFlags.Read);

        if (file == null) {
            GD.PushError($"Ne mogu da otvorim {CardsPath}: {FileAccess.GetOpenError()}");
            return;
        }

        try {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, Card>>(file.GetAsText());

            if (parsed != null) {
                _cards = parsed;
            }
        } catch (JsonException ex) {
            GD.PushError($"{CardsPath} nije validan JSON: {ex.Message}");
        }
    }
}
