namespace Sensor.Api.Messaging;

// Read from the "RabbitMq" section of appsettings.json. The password is set with dotnet user-secrets (not in code).
public class RabbitMqSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;          // AMQP port from infra/docker-compose.yml
    public string User { get; set; } = "treeo";
    public string Password { get; set; } = "";
}
