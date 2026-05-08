using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using FractureGuard.Api.Models;

namespace FractureGuard.Api.Services;

public class AnalysisJobService : IAnalysisJobService, IDisposable
{
    private const string QueueName = "analysis-requests";
    private readonly IConfiguration _config;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;
    private IModel? _channel;

    public AnalysisJobService(IConfiguration config)
    {
        _config = config;
    }

    private IModel EnsureChannel()
    {
        if (_channel is { IsOpen: true }) return _channel;

        var factory = new ConnectionFactory
        {
            HostName = _config["RABBITMQ_HOST"] ?? "localhost",
            UserName = _config["RABBITMQ_USER"] ?? "guest",
            Password = _config["RABBITMQ_PASS"] ?? "guest",
        };
        _connection?.Dispose();
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false);
        return _channel;
    }

    public async Task PublishAsync(AnalysisRequest request)
    {
        var body = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        await _lock.WaitAsync();
        try
        {
            var ch = EnsureChannel();
            var props = ch.CreateBasicProperties();
            props.ContentType = "application/json";
            props.Persistent = true;
            ch.BasicPublish("", QueueName, props, body);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}
