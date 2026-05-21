using Kolekta.Web.Models.Cards;

namespace Kolekta.Web.Models.Inventory;

public class UserCard
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = "";

    public Guid CardId { get; set; }

    public Card Card { get; set; } = default!;

    public DateTime ObtainedAt { get; set; } = DateTime.UtcNow;
}