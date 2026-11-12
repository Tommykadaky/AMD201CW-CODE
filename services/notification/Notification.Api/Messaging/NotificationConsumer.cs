using TreeO.Contracts.Events;
using Notification.Api.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Notification.Api.Messaging;

// Chay nen cung voi service: nghe queue "notification.requests" cua RabbitMQ,
// moi tin nhan nhan duoc thi luu thanh 1 thong bao trong notification_db.
public sealed class NotificationConsumer(
    IServiceScopeFactory scopeFactory,
    RabbitMqSettings settings,
    ILogger<NotificationConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // RabbitMQ chua bat (hoac chua san sang) thi thu lai moi 5 giay, khong lam sap service.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);   // chay mai cho den khi tat service
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Chua noi duoc RabbitMQ ({Message}). Thu lai sau 5 giay.", ex.Message);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName = settings.Host,
            Port = settings.Port,
            UserName = settings.User,
            Password = settings.Password,
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken: ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        // Khai bao exchange + queue + noi chung voi nhau. Khai bao lai nhieu lan van an toan.
        await channel.ExchangeDeclareAsync(
            exchange: MessagingNames.Exchange, type: ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: ct);
        await channel.QueueDeclareAsync(
            queue: MessagingNames.NotificationQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: ct);
        await channel.QueueBindAsync(
            queue: MessagingNames.NotificationQueue, exchange: MessagingNames.Exchange,
            routingKey: MessagingNames.NotificationBinding, arguments: null, cancellationToken: ct);

        // Nhan toi da 10 tin chua xac nhan cung luc
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) => await HandleAsync(channel, ea, ct);

        // autoAck = false: chi xac nhan (Ack) sau khi luu database xong, mat dien giua chung thi tin khong mat
        await channel.BasicConsumeAsync(
            queue: MessagingNames.NotificationQueue, autoAck: false, consumer: consumer, cancellationToken: ct);

        logger.LogInformation("Dang lang nghe RabbitMQ queue {Queue}", MessagingNames.NotificationQueue);

        // Giu ket noi mo cho den khi service tat (khi do Delay nem OperationCanceledException)
        await Task.Delay(Timeout.Infinite, ct);
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        // Tin hong (sai JSON, thieu truong): bo han, gui lai cung vo ich
        if (!NotificationMessageParser.TryParse(ea.Body.Span, out var message, out var error) || message is null)
        {
            logger.LogWarning("Bo tin nhan khong hop le: {Error}", error);
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var ingestor = scope.ServiceProvider.GetRequiredService<NotificationIngestor>();
            var saved = await ingestor.SaveAsync(message, ct);

            logger.LogInformation(
                saved ? "Da luu thong bao {Type} cho user {UserId}" : "Bo qua thong bao trung {Type} cho user {UserId}",
                message.Type, message.UserId);

            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            // Loi tam thoi (vd database chua san sang): doi 2 giay roi tra tin ve hang doi de thu lai
            logger.LogError(ex, "Luu thong bao that bai, se thu lai.");
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: ct);
            }
            catch (Exception)
            {
                // Dang tat service hoac mat ket noi: RabbitMQ tu giao lai tin nay sau
            }
        }
    }
}
