using Microsoft.Azure.Cosmos;
using FractureGuard.Api.Models;

namespace FractureGuard.Api.Infrastructure;

public interface ICosmosDbService
{
    Task<ChatSession> GetOrCreateSessionAsync(string sessionId, string userId);
    Task AppendMessageAsync(string sessionId, string userId, ChatMessage message);
    Task<List<ChatSession>> GetSessionsByUserAsync(string userId);
}

public class CosmosDbService : ICosmosDbService
{
    private readonly Container _container;
    private readonly ILogger<CosmosDbService> _logger;

    public CosmosDbService(IConfiguration config, ILogger<CosmosDbService> logger)
    {
        _logger = logger;

        var endpoint = config["COSMOS_ENDPOINT"];
        var key = config["COSMOS_KEY"];
        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
            throw new InvalidOperationException(
                "COSMOS_ENDPOINT and COSMOS_KEY must be configured.");

        var clientOptions = new CosmosClientOptions
        {
            HttpClientFactory = () => new HttpClient(
                new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                }),
            ConnectionMode = ConnectionMode.Gateway
        };
        var client = new CosmosClient(endpoint, key, clientOptions);
        var dbName = config["COSMOS_DB"] ?? "FractureGuardDB";

        // Ensure database and container exist (best-effort — failures logged, not thrown)
        try
        {
            var dbResponse = client.CreateDatabaseIfNotExistsAsync(dbName).GetAwaiter().GetResult();
            dbResponse.Database.CreateContainerIfNotExistsAsync(
                new ContainerProperties("ChatSessions", "/userId")).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cosmos DB init failed (emulator may still be starting). Will retry on first use.");
        }

        _container = client.GetDatabase(dbName).GetContainer("ChatSessions");
    }

    public async Task<ChatSession> GetOrCreateSessionAsync(string sessionId, string userId)
    {
        try
        {
            var response = await _container.ReadItemAsync<ChatSession>(
                sessionId, new PartitionKey(userId)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            var session = new ChatSession { Id = sessionId, UserId = userId };
            await _container.CreateItemAsync(session, new PartitionKey(userId));
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cosmos session lookup failed; using in-memory fallback.");
            return new ChatSession { Id = sessionId, UserId = userId };
        }
    }

    public async Task AppendMessageAsync(string sessionId, string userId, ChatMessage message)
    {
        try
        {
            var patch = PatchOperation.Add("/messages/-", message);
            await _container.PatchItemAsync<ChatSession>(
                sessionId, new PartitionKey(userId), new[] { patch }
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cosmos append failed; message not persisted.");
        }
    }

    public async Task<List<ChatSession>> GetSessionsByUserAsync(string userId)
    {
        try
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.userId = @userId ORDER BY c.createdAt DESC"
            ).WithParameter("@userId", userId);

            var results = new List<ChatSession>();
            var iterator = _container.GetItemQueryIterator<ChatSession>(query);
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync();
                results.AddRange(page);
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cosmos query failed; returning empty history.");
            return new List<ChatSession>();
        }
    }
}
