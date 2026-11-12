namespace Notification.Api.Messaging;

// Doc tu appsettings.json muc "RabbitMq". Mat khau dat bang dotnet user-secrets (khong nam trong code).
public class RabbitMqSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;          // cong AMQP cua RabbitMQ trong docker-compose
    public string User { get; set; } = "treeo";
    public string Password { get; set; } = "";
}
