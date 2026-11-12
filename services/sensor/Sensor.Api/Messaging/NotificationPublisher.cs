using System.Text.Json;
using TreeO.Contracts.Events;
using RabbitMQ.Client;

namespace Sensor.Api.Messaging;

// Sends notifications to the Notification service through RabbitMQ (same contract as Garden and Reminder).
public interface INotificationPublisher
{
    // Never throws: if RabbitMQ is down it only logs a warning, so readings from the device keep working.
    // Returns true when the message was published.
    Task<bool> PublishAsync(NotificationRequested message, CancellationToken ct = default);
}

public sealed class RabbitMqNotificationPublisher(
    RabbitMqSettings settings,
    ILogger<RabbitMqNotificationPublisher> logger) : INotificationPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task<bool> PublishAsync(NotificationRequested message, CancellationToken ct = default)
    {
        // Give up after 5 seconds instead of hanging
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            var channel = await GetChannelAsync(timeout.Token);
            var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
            var props = new BasicProperties { ContentType = "application/json", Persistent = true };

            await channel.BasicPublishAsync(
                exchange: MessagingNames.Exchange,
                routingKey: MessagingNames.RoutingKeyFor(message.Type),
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: timeout.Token);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Could not publish {Type} notification to RabbitMQ: {Message}", message.Type, ex.Message);
            return false;
        }
    }

    // Open the connection once and reuse it; reopen it if it dropped
    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true }) return _channel;

        await _lock.WaitAsync(ct);
        try
        {
            if (_channel is { IsOpen: true }) return _channel;

            if (_channel is not null) await _channel.DisposeAsync();
            if (_connection is not null) await _connection.DisposeAsync();
            _channel = null;
            _connection = null;

            var factory = new ConnectionFactory
            {
                HostName = settings.Host,
                Port = settings.Port,
                UserName = settings.User,
                Password = settings.Password,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(3),
            };
            _connection = await factory.CreateConnectionAsync(cancellationToken: ct);
            _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

            // Topic exchange; the Notification service declares the same one
            await _channel.ExchangeDeclareAsync(
                exchange: MessagingNames.Exchange, type: ExchangeType.Topic, durable: true, autoDelete: false,
                cancellationToken: ct);
            return _channel;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
        _lock.Dispose();
    }
}
