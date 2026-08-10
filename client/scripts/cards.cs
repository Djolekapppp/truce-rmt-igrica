
using System.Collections.Generic;
using System.Text.Json;

public class Card
{
    //Ovde moze da ide i slika ili tako neš
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Nature { get; set; } = 0;
    public int Faith { get; set; } = 0;
    public int Science { get; set; } = 0;

}

public class CardDatabase
{
    public Dictionary<string, Card> Cards { get; set; } = new();

    public void LoadCards()
    {
        string json = System.IO.File.ReadAllText("../data/cards.json");

        var cardList = JsonSerializer.Deserialize<List<Card>>(json);
        if (cardList != null)
        {
            foreach (var card in cardList)
            {
                Cards[card.Name] = card;
            }
        }
    }

    public Card GetCard(string name)
    {
        if (Cards.TryGetValue(name, out var card))
        {
            return card;
        }
        else
        {
            throw new KeyNotFoundException($"Card with name '{name}' not found.");
        }
    }
}
