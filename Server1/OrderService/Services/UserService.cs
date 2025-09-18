using System.Text.Json;
using OrderService.Models.DTOs;
using StackExchange.Redis;

namespace OrderService.Services;

public class UserService(HttpClient httpClient, IConnectionMultiplexer redis) : IUserService
{
    private readonly IDatabase _cache = redis.GetDatabase();

    public async Task<UserDto?> GetUserByIdAsync(int userId)
    {
        var cacheKey = $"user:{userId}";

        // 1. Проверяем кэш
        var cached = await _cache.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            return JsonSerializer.Deserialize<UserDto>(cached!, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        // 2. Если нет в кэше → идём в UserService
        var response = await httpClient.GetAsync($"/api/users/{userId}");
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        var user = JsonSerializer.Deserialize<UserDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (user != null)
        {
            // 3. Сохраняем в Redis с TTL (например, 10 минут)
            await _cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(user), TimeSpan.FromMinutes(10));
        }

        return user;
    }
}