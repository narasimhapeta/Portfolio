using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using FractureGuard.Api.Models;
using FractureGuard.Api.Plugins;

namespace FractureGuard.Api.Services;

public class AnalysisResultConsumer : BackgroundService
{
    private const string ResultQueue = "analysis-results";
    private readonly IConfiguration _config;
    private readonly IServiceProvider _sp;
    private readonly ILogger<AnalysisResultConsumer> _logger;

    public AnalysisResultConsumer(IConfiguration config, IServiceProvider sp,
        ILogger<AnalysisResultConsumer> logger)
        => (_config, _sp, _logger) = (config, sp, logger);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _config["RABBITMQ_HOST"] ?? "localhost",
                    UserName = _config["RABBITMQ_USER"] ?? "guest",
                    Password = _config["RABBITMQ_PASS"] ?? "guest",
                };
                using var conn = factory.CreateConnection();
                using var ch = conn.CreateModel();
                ch.QueueDeclare(ResultQueue, durable: true, exclusive: false, autoDelete: false);

                var consumer = new EventingBasicConsumer(ch);
                consumer.Received += async (_, ea) =>
                {
                    try
                    {
                        var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                        var result = JsonSerializer.Deserialize<AnalysisResult>(body,
                            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

                        using var scope = _sp.CreateScope();
                        var reportPlugin = scope.ServiceProvider.GetRequiredService<ReportPlugin>();
                        var notifier     = scope.ServiceProvider.GetRequiredService<INotifierService>();

                        var report = await reportPlugin.GenerateReportAsync(result);
                        await notifier.SendReportAsync(result.SessionId, report);
                        ch.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process analysis result");
                        ch.BasicNack(ea.DeliveryTag, false, requeue: false);
                    }
                };

                ch.BasicConsume(ResultQueue, autoAck: false, consumer);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RabbitMQ consumer disconnected, retrying in 5s");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
