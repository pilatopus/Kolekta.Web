namespace Kolekta.Web.Services.Drops
{
    using Kolekta.Web.Data.Seed;
    using System.Collections.Concurrent;
    using System.Text.Json;

    public class CharacterSeeder
    {
        private readonly HttpClient _http;
        private readonly IWebHostEnvironment _env;

        // =========================================
        // SETTINGS
        // =========================================

        // Higher = more anime variety
        private const int MaxPages = 100;

        // Prevents Jikan rate limit
        private const int MaxConcurrency = 3;

        // Prevents huge anime dominating pool
        private const int MaxCharactersPerSeries = 15;

        // =========================================
        // CONSTRUCTOR
        // =========================================

        public CharacterSeeder(
            HttpClient http,
            IWebHostEnvironment env
        )
        {
            _http = http;
            _env = env;
        }

        // =========================================
        // MAIN SEED
        // =========================================

        public async Task SeedAsync(
            CancellationToken ct = default
        )
        {
            var dataPath = Path.Combine(
                _env.ContentRootPath,
                "Data",
                "characters.json"
            );

            // Skip if already seeded
            if (File.Exists(dataPath))
            {
                Console.WriteLine(
                    "Character database already exists."
                );

                return;
            }

            Directory.CreateDirectory(
                Path.GetDirectoryName(dataPath)!
            );

            Console.WriteLine(
                "Fetching anime pages..."
            );

            // =========================================
            // FETCH ALL ANIME PAGES
            // =========================================

            var animeTasks = Enumerable
                .Range(1, MaxPages)
                .Select(page => GetAnimePageAsync(page, ct));

            var animePages = await Task.WhenAll(animeTasks);

            // =========================================
            // COMBINE + RANDOMIZE
            // =========================================

            var allAnime = animePages
                .SelectMany(x => x)

                // Remove duplicates
                .GroupBy(x => x.Id)
                .Select(x => x.First())

                // Shuffle everything
                .OrderBy(x => Guid.NewGuid())
                .ToList();

            Console.WriteLine(
                $"Total anime found: {allAnime.Count}"
            );

            // =========================================
            // CHARACTER RESULTS
            // =========================================

            var results = new ConcurrentBag<CharacterSeed>();

            // =========================================
            // LIMIT CONCURRENT REQUESTS
            // =========================================

            using var semaphore = new SemaphoreSlim(
                MaxConcurrency
            );

            var characterTasks = allAnime.Select(async anime =>
            {
                await semaphore.WaitAsync(ct);

                try
                {
                    ct.ThrowIfCancellationRequested();

                    Console.WriteLine(
                        $"Fetching: {anime.Title}"
                    );

                    var charJson = await SafeGetStringAsync(
                        $"https://api.jikan.moe/v4/anime/{anime.Id}/characters",
                        ct
                    );

                    if (string.IsNullOrWhiteSpace(charJson))
                        return;

                    using var charDoc = JsonDocument.Parse(charJson);

                    if (!charDoc.RootElement.TryGetProperty("data", out var chars))
                        return;

                    // =========================================
                    // RANDOMIZE CHARACTERS
                    // =========================================

                    var randomizedCharacters = chars
                        .EnumerateArray()
                        .OrderBy(x => Guid.NewGuid())
                        .Take(MaxCharactersPerSeries);

                    foreach (var c in randomizedCharacters)
                    {
                        try
                        {
                            var character = c.GetProperty("character");

                            var name = character
                                .GetProperty("name")
                                .GetString();

                            var image = character
                                .GetProperty("images")
                                .GetProperty("jpg")
                                .GetProperty("image_url")
                                .GetString();

                            if (string.IsNullOrWhiteSpace(name))
                                continue;

                            if (string.IsNullOrWhiteSpace(image))
                                continue;

                            results.Add(new CharacterSeed
                            {
                                Name = name.Trim(),

                                Series = anime.Title.Trim(),

                                ImageUrl = image,

                                Popularity = Random.Shared.Next(1, 1000)
                            });
                        }
                        catch
                        {
                            // Skip invalid character
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Character fetch error: {ex.Message}"
                    );
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(characterTasks);

            // =========================================
            // CLEAN + BALANCE DATABASE
            // =========================================

            var cleanResults = results

                // Remove duplicates
                .GroupBy(x => new
                {
                    Name = x.Name.ToLower(),
                    Series = x.Series.ToLower()
                })
                .Select(x => x.First())

                // Prevent series domination
                .GroupBy(x => x.Series)
                .SelectMany(group => group
                    .OrderBy(x => Guid.NewGuid())
                    .Take(MaxCharactersPerSeries)
                )

                // Final shuffle
                .OrderBy(x => Guid.NewGuid())

                .ToList();

            Console.WriteLine(
                $"Final characters saved: {cleanResults.Count}"
            );

            // =========================================
            // SAVE JSON
            // =========================================

            var json = JsonSerializer.Serialize(
                cleanResults,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }
            );

            await File.WriteAllTextAsync(
                dataPath,
                json,
                ct
            );

            Console.WriteLine(
                "Character seeding completed."
            );
        }

        // =========================================
        // FETCH ANIME PAGE
        // =========================================

        private async Task<List<AnimeInfo>> GetAnimePageAsync(
            int page,
            CancellationToken ct
        )
        {
            try
            {
                // =========================================
                // RANDOM SORT TYPES
                // =========================================

                var sortOptions = new[]
                {
                    "members",
                    "favorites",
                    "score",
                    "popularity"
                };

                var selectedSort = sortOptions[
                    Random.Shared.Next(sortOptions.Length)
                ];

                var animeJson = await SafeGetStringAsync(
                    $"https://api.jikan.moe/v4/anime?page={page}" +
                    $"&order_by={selectedSort}" +
                    $"&sort={(page % 2 == 0 ? "asc" : "desc")}",
                    ct
                );

                if (string.IsNullOrWhiteSpace(animeJson))
                    return new();

                using var animeDoc = JsonDocument.Parse(animeJson);

                if (!animeDoc.RootElement.TryGetProperty("data", out var animeList))
                    return new();

                var animeResults = animeList
                    .EnumerateArray()
                    .Select(anime =>
                    {
                        try
                        {
                            var malId = anime
                                .GetProperty("mal_id")
                                .GetInt32();

                            var title = anime
                                .GetProperty("title")
                                .GetString();

                            if (string.IsNullOrWhiteSpace(title))
                                return null;

                            return new AnimeInfo
                            {
                                Id = malId,
                                Title = title
                            };
                        }
                        catch
                        {
                            return null;
                        }
                    })
                    .Where(x => x is not null)
                    .Cast<AnimeInfo>()

                    // Randomize page results
                    .OrderBy(x => Guid.NewGuid())

                    .ToList();

                return animeResults;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Anime fetch error: {ex.Message}"
                );

                return new();
            }
        }

        // =========================================
        // SAFE HTTP GET
        // =========================================

        private async Task<string?> SafeGetStringAsync(
            string url,
            CancellationToken ct
        )
        {
            try
            {
                using var response = await _http.GetAsync(
                    url,
                    ct
                );

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine(
                        $"HTTP {(int)response.StatusCode}: {url}"
                    );

                    return null;
                }

                return await response
                    .Content
                    .ReadAsStringAsync(ct);
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"HTTP error: {ex.Message}"
                );

                return null;
            }
        }

        // =========================================
        // INTERNAL MODEL
        // =========================================

        private sealed class AnimeInfo
        {
            public int Id { get; set; }

            public string Title { get; set; } = string.Empty;
        }
    }
}