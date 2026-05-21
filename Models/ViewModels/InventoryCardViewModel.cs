using Kolekta.Web.Models.Cards;

namespace Kolekta.Web.Models.ViewModels;

public class InventoryCardViewModel
{
    public Card Card { get; set; } = default!;

    public int Quantity { get; set; }

    public DateTime LastObtainedAt { get; set; }
}