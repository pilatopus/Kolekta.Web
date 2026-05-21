namespace Kolekta.Web.Services.Drops
{
    using Kolekta.Web.Data.Seed;
    using System.Text.Json;

    public class CharacterPoolBuilder
    {
        private readonly IWebHostEnvironment _env;

        private List<CharacterSeed> _pool = [];

        public IReadOnlyList<CharacterSeed> Pool => _pool;

        public CharacterPoolBuilder(
            IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<List<CharacterSeed>> GetPool()
        {
            if (_pool.Count > 0)
                return _pool;

            var path = Path.Combine(
                _env.ContentRootPath,
                "Data",
                "characters.json"
            );

            if (!File.Exists(path))
                return [];

            var json =
                await File.ReadAllTextAsync(path);

            _pool =
                JsonSerializer.Deserialize<List<CharacterSeed>>(json)
                ?? [];

            return _pool;
        }
    }
}