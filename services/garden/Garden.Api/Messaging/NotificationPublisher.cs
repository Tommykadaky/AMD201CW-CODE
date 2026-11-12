using System.Text.Json;
using TreeO.Contracts.Events;
using RabbitMQ.Client;

namespace Garden.Api.Messaging;

// Gui thong bao sang Notification service qua RabbitMQ.
public interface INotificationPublisher
{
    // Gui 1 thong bao. KHONG nem loi: RabbitMQ tat thi chi ghi log canh bao,
    // de tinh nang chinh (tuoi cay, xem thoi tiet) van chay binh thuong.
    // Tra ve true neu da gui, false neu that bai.
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
        // Toi da cho 5 giay: RabbitMQ khong tra loi thi bo, khong lam cham tinh nang chinh
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
            logger.LogWarning("Khong gui duoc thong bao {Type} qua RabbitMQ: {Message}", message.Type, ex.Message);
            return false;
        }
    }

    // Mo ket noi 1 lan roi dung lai cho cac lan gui sau; ket noi dut thi mo lai
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

            // Exchange kieu topic, ben nhan (Notification) cung khai bao giong het
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
