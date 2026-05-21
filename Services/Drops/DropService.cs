namespace Kolekta.Web.Services.Drops;

using Kolekta.Web.Data.Seed;
using Kolekta.Web.Models.Cards;

public class DropService
{
    private readonly CharacterPoolBuilder _poolBuilder;

    // =========================================
    // SETTINGS
    // =========================================

    // How many recent series to remember and avoid re-picking.
    // Prevents the same franchise from dominating consecutive drops.
    // EDIT: raise for stricter series variety, lower to relax it.
    private const int SeriesCooldownSize = 20;

    // How many recent characters to remember and avoid re-picking.
    // Prevents the exact same card from appearing twice in a session.
    // EDIT: raise for longer "no repeat" memory, lower to relax it.
    private const int CharacterCooldownSize = 50;

    // Maximum attempts when picking a card that passes cooldown checks.
    // Acts as a safety valve so the method never hangs if the pool is
    // very small or cooldowns are very aggressive.
    // EDIT: raise if you have a large pool and strict cooldowns.
    private const int MaxPickAttempts = 30;

    // =========================================
    // RARITY WEIGHTS (must sum to 100)
    // =========================================

    // Each threshold is cumulative. Roll < 60 → Common, etc.
    // EDIT: adjust the numbers to rebalance drop rates.
    private const int CommonThreshold = 60; // 60 %
    private const int RareThreshold = 85; // 25 %
    private const int EpicThreshold = 97; // 12 %
    // Legendary fills the remainder          //  3 %

    // =========================================
    // STATE
    // =========================================

    // Cooldown queues track recently used series and characters so
    // we can avoid re-picking them for the next several drops.
    // These are instance-level so they persist for the lifetime of
    // the service (scoped or singleton depending on DI registration).
    private readonly Queue<string> _recentSeries = new();
    private readonly Queue<string> _recentCharacters = new();

    // Thread-safe random. Random.Shared is a static, thread-safe
    // instance available in .NET 6+. Using it instead of `new Random()`
    // avoids the seeding collision that can make multiple instances
    // produce identical sequences when created close together in time.
    private static Random Rng => Random.Shared;

    // =========================================
    // CONSTRUCTOR
    // =========================================

    public DropService(CharacterPoolBuilder poolBuilder)
    {
        _poolBuilder = poolBuilder;
    }

    // =========================================
    // SINGLE DROP
    // =========================================

    public async Task<Card?> DropAsync()
    {
        var pool = await _poolBuilder.GetPool();

        if (pool.Count == 0)
            return null;

        var seed = PickFromPool(pool, excludeSeries: null);

        if (seed is null)
            return null;

        return ConvertToCard(seed);
    }

    // =========================================
    // DROP N CARDS  (replaces the hard-coded DropThreeCards)
    // =========================================

    // Picks `count` cards that are all from different series.
    // If the pool is too small to satisfy that constraint the method
    // falls back gracefully and returns however many unique cards it can.
    // EDIT: keep the old name by adding a DropThreeCards() wrapper below,
    // or call DropMultipleAsync(3) directly from your controllers.
    public async Task<List<Card>> DropMultipleAsync(int count = 3)
    {
        var pool = await _poolBuilder.GetPool();

        if (pool.Count == 0)
            return [];

        var results = new List<Card>(count);

        // Track which series appeared in THIS batch so each slot
        // gets a different franchise, independent of the global cooldown.
        var usedInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < count; i++)
        {
            // Merge the global cooldown set with the per-batch set so
            // the pick method can avoid both at once.
            var excludeSeries = new HashSet<string>(
                _recentSeries,
                StringComparer.OrdinalIgnoreCase
            );

            foreach (var s in usedInBatch)
                excludeSeries.Add(s);

            var seed = PickFromPool(pool, excludeSeries);

            if (seed is null)
                break; // pool exhausted or too restricted — stop early

            usedInBatch.Add(seed.Series);
            results.Add(ConvertToCard(seed));
        }

        return results;
    }

    // Convenience wrapper so existing callers keep working unchanged.
    public Task<List<Card>> DropThreeCards() =>
        DropMultipleAsync(3);

    // =========================================
    // CORE PICK LOGIC
    // =========================================

    // Shuffles the pool into a temporary list, then walks it looking
    // for the first candidate that passes all cooldown / exclusion checks.
    // Falls back to a purely random pick if no candidate passes within
    // MaxPickAttempts, so a tiny pool never deadlocks.
    private CharacterSeed? PickFromPool(
        IReadOnlyList<CharacterSeed> pool,
        HashSet<string>? excludeSeries)
    {
        // Fisher-Yates shuffle on indices so we visit candidates in a
        // random order without copying or mutating the original pool list.
        var indices = Enumerable.Range(0, pool.Count).ToArray();
        ShuffleIndices(indices);

        CharacterSeed? fallback = null;
        int attempts = 0;

        foreach (var idx in indices)
        {
            if (attempts >= MaxPickAttempts)
                break;

            attempts++;

            var candidate = pool[idx];

            // --- Series cooldown check ---
            // Skip if this series was recently dropped globally.
            if (_recentSeries.Contains(candidate.Series))
                continue;

            // Skip if this series is already in the current batch.
            if (excludeSeries is not null &&
                excludeSeries.Contains(candidate.Series))
                continue;

            // --- Character cooldown check ---
            // Skip if this exact character was recently dropped.
            if (_recentCharacters.Contains(candidate.Name))
                continue;

            // All checks passed — register in cooldowns and return.
            RegisterCooldowns(candidate);
            return candidate;
        }

        // Fallback: if every candidate failed the checks (pool too small
        // or cooldowns too aggressive) just return a random card without
        // enforcing any restriction, so the player always gets something.
        fallback = pool[Rng.Next(pool.Count)];
        RegisterCooldowns(fallback);
        return fallback;
    }

    // =========================================
    // COOLDOWN REGISTRATION
    // =========================================

    // Pushes the picked character and series into their respective
    // cooldown queues, evicting the oldest entry when the queue is full.
    private void RegisterCooldowns(CharacterSeed seed)
    {
        // Series cooldown
        if (!_recentSeries.Contains(seed.Series))
        {
            if (_recentSeries.Count >= SeriesCooldownSize)
                _recentSeries.Dequeue();

            _recentSeries.Enqueue(seed.Series);
        }

        // Character cooldown
        if (!_recentCharacters.Contains(seed.Name))
        {
            if (_recentCharacters.Count >= CharacterCooldownSize)
                _recentCharacters.Dequeue();

            _recentCharacters.Enqueue(seed.Name);
        }
    }

    // =========================================
    // FISHER-YATES SHUFFLE (in-place, indices only)
    // =========================================

    // Shuffling indices rather than the pool list avoids allocating a
    // full copy of the pool on every pick, keeping memory usage flat.
    private static void ShuffleIndices(int[] indices)
    {
        for (int i = indices.Length - 1; i > 0; i--)
        {
            int j = Rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }
    }

    // =========================================
    // SEED → CARD CONVERTER
    // =========================================

    private Card ConvertToCard(CharacterSeed seed)
    {
        return new Card
        {
            CharacterName = seed.Name,
            Series = seed.Series,
            ImageUrl = seed.ImageUrl,
            Rarity = RollRarity()
        };
    }

    // =========================================
    // RARITY SYSTEM
    // =========================================

    // Uses cumulative thresholds so the numbers are easy to reason about:
    // Common 60 %, Rare 25 %, Epic 12 %, Legendary 3 %.
    // EDIT: adjust the constants at the top of the file to rebalance.
    private static string RollRarity()
    {
        var roll = Rng.Next(100);

        return roll switch
        {
            < CommonThreshold => "Common",
            < RareThreshold => "Rare",
            < EpicThreshold => "Epic",
            _ => "Legendary"
        };
    }
}