using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using FractureGuard.Api.Models;

namespace FractureGuard.Api.Services;

public class AnalysisJobService : IAnalysisJobService, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private const string QueueName = "analysis-requests";

    public AnalysisJobService(IConfiguration config)
    {
        var factory = new ConnectionFactory
        {
            HostName = config["RABBITMQ_HOST"] ?? "localhost",
            UserName = config["RABBITMQ_USER"] ?? "guest",
            Password = config["RABBITMQ_PASS"] ?? "guest",
        };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false);
    }

    public Task PublishAsync(AnalysisRequest request)
    {
        var body = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var props = _channel.CreateBasicProperties();
        props.ContentType = "application/json";
        props.Persistent = true;
        _channel.BasicPublish("", QueueName, props, body);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
    }
}
