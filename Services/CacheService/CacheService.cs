using HealthcareKnowledgeAssistant.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace HealthcareKnowledgeAssistant.Services.CacheService
{
    public class CacheService
    {
        private readonly IDatabase? _database;
        private readonly RedisSettings _settings;
        private readonly ILogger<CacheService> _logger;

        // Used when Redis is enabled.
        public CacheService(
            IConnectionMultiplexer redis,
            IOptions<RedisSettings> options,
            ILogger<CacheService> logger)
        {
            _database = redis.GetDatabase();
            _settings = options.Value;
            _logger = logger;
        }

        // Used when Redis is intentionally disabled (for example MonsterASP.NET).
        public CacheService(
            IOptions<RedisSettings> options,
            ILogger<CacheService> logger)
        {
            _database = null;
            _settings = options.Value;
            _logger = logger;
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            if (!_settings.Enabled || _database is null)
            {
                return default;
            }

            RedisValue value;

            try
            {
                value = await _database.StringGetAsync(key);
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Redis cache read failed for {CacheKey}. Continuing without cached response.",
                    key);
                return default;
            }

            if (value.IsNullOrEmpty)
            {
                _logger.LogInformation("Cache MISS for {CacheKey}", key);
                return default;
            }

            _logger.LogInformation("Cache HIT for {CacheKey}", key);

            try
            {
                return JsonSerializer.Deserialize<T>(value.ToString());
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Redis cache value could not be deserialized for {CacheKey}.",
                    key);
                return default;
            }
        }

        public async Task SetAsync<T>(string key, T value)
        {
            if (!_settings.Enabled || _database is null)
            {
                return;
            }

            try
            {
                await _database.StringSetAsync(
                    key,
                    JsonSerializer.Serialize(value),
                    TimeSpan.FromMinutes(_settings.DefaultTtlMinutes));
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Redis cache write failed for {CacheKey}.", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            if (!_settings.Enabled || _database is null)
            {
                return;
            }

            try
            {
                await _database.KeyDeleteAsync(key);
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Redis cache delete failed for {CacheKey}.", key);
            }
        }
    }
}
