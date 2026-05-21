namespace Kolekta.Web.Models.Cards;

public class Card
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string CharacterName { get; set; } = "";

    public string Series { get; set; } = "";

    public string ImageUrl { get; set; } // <- just URL

    public string Rarity { get; set; } = "";

    public int DropWeight { get; set; }  // lower = rarer
    public int Popularity { get; set; } // 👈 NEW

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}