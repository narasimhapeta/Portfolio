using Microsoft.Azure.Cosmos;
using FractureGuard.Api.Models;

namespace FractureGuard.Api.Infrastructure;

public interface ICosmosDbService
{
    Task<ChatSession> GetOrCreateSessionAsync(string sessionId, string userId);
    Task AppendMessageAsync(string sessionId, ChatMessage message);
    Task<List<ChatSession>> GetSessionsByUserAsync(string userId);
}

public class CosmosDbService : ICosmosDbService
{
    private readonly Container _container;

    public CosmosDbService(IConfiguration config)
    {
        var client = new CosmosClient(
            config["COSMOS_ENDPOINT"],
            config["COSMOS_KEY"]
        );
        var db = client.GetDatabase(config["COSMOS_DB"] ?? "FractureGuardDB");
        _container = db.GetContainer("ChatSessions");
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
    }

    public async Task AppendMessageAsync(string sessionId, ChatMessage message)
    {
        var patch = PatchOperation.Add("/messages/-", message);
        await _container.PatchItemAsync<ChatSession>(
            sessionId, new PartitionKey(sessionId), new[] { patch }
        );
    }

    public async Task<List<ChatSession>> GetSessionsByUserAsync(string userId)
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
}
