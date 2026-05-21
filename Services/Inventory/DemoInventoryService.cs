using Blazored.SessionStorage;
using Kolekta.Web.Models.Cards;

namespace Kolekta.Web.Services.Inventory;

public class DemoInventoryService
{
    private const string KEY = "demo_inventory";

    private readonly ISessionStorageService _session;

    public DemoInventoryService(ISessionStorageService session)
    {
        _session = session;
    }

    public async Task<List<Card>> GetInventory()
    {
        var cards = await _session.GetItemAsync<List<Card>>(KEY);

        return cards ?? new List<Card>();
    }

    public async Task AddCard(Card card)
    {
        var cards = await GetInventory();

        cards.Add(card);

        await _session.SetItemAsync(KEY, cards);
    }

    public async Task Clear()
    {
        await _session.RemoveItemAsync(KEY);
    }
}