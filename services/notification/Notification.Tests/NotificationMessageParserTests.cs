using System.Text;
using TreeO.Contracts.Events;
using Notification.Api.Data;
using Notification.Api.Messaging;

namespace Notification.Tests;

// Test viec doc va kiem tra tin nhan RabbitMQ (khong can RabbitMQ hay database)
public class NotificationMessageParserTests
{
    private static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);

    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void TryParse_AcceptsValidMessage()
    {
        var json = $$"""
        { "userId": "{{UserId}}", "type": "heatwave", "title": "Heatwave", "body": "Hot tomorrow",
          "data": { "maxTemp": "36" }, "dedupKey": "heatwave:x:2026-11-08", "occurredAtUtc": "2026-11-07T01:00:00Z" }
        """;

        var ok = NotificationMessageParser.TryParse(Bytes(json), out var msg, out var error);

        Assert.True(ok, error);
        Assert.NotNull(msg);
        Assert.Equal(UserId, msg!.UserId);
        Assert.Equal("heatwave", msg.Type);
        Assert.Equal("36", msg.Data!["maxTemp"]);
        Assert.Equal("heatwave:x:2026-11-08", msg.DedupKey);
    }

    [Fact]
    public void TryParse_RoundTripsWhatPublishersSend()
    {
        var original = new NotificationRequested(UserId, NotificationTypes.Streak, "7 day streak", "Nice!",
            new Dictionary<string, string> { ["days"] = "7" }, "streak:a:b", DateTime.UtcNow);
        var body = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(original, NotificationMessageParser.JsonOptions);

        var ok = NotificationMessageParser.TryParse(body, out var msg, out _);

        Assert.True(ok);
        Assert.Equal(original.UserId, msg!.UserId);
        Assert.Equal("7", msg.Data!["days"]);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]                                                                                        // thieu userId
    [InlineData("""{ "userId": "11111111-1111-1111-1111-111111111111", "title": "x" }""")]                    // thieu type
    [InlineData("""{ "userId": "11111111-1111-1111-1111-111111111111", "type": "pump" }""")]                  // thieu title
    [InlineData("""{ "userId": "11111111-1111-1111-1111-111111111111", "type": "pump", "title": "  " }""")]   // title toan dau cach
    public void TryParse_RejectsBadMessages(string json)
    {
        var ok = NotificationMessageParser.TryParse(Bytes(json), out var msg, out var error);

        Assert.False(ok);
        Assert.Null(msg);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void TryParse_TruncatesLongFields()
    {
        var json = $$"""
        { "userId": "{{UserId}}", "type": "pump",
          "title": "{{new string('t', NotificationLimits.Title + 50)}}",
          "body": "{{new string('b', NotificationLimits.Body + 50)}}",
          "dedupKey": "{{new string('k', NotificationLimits.DedupKey + 50)}}" }
        """;

        var ok = NotificationMessageParser.TryParse(Bytes(json), out var msg, out _);

        Assert.True(ok);
        Assert.Equal(NotificationLimits.Title, msg!.Title.Length);
        Assert.Equal(NotificationLimits.Body, msg.Body.Length);
        Assert.Equal(NotificationLimits.DedupKey, msg.DedupKey!.Length);
    }

    [Fact]
    public void TryParse_LimitsDataToTenEntries()
    {
        var entries = string.Join(",", Enumerable.Range(0, 15).Select(i => $"\"k{i}\": \"v\""));
        var json = $$"""{ "userId": "{{UserId}}", "type": "pump", "title": "x", "data": { {{entries}} } }""";

        var ok = NotificationMessageParser.TryParse(Bytes(json), out var msg, out _);

        Assert.True(ok);
        Assert.Equal(10, msg!.Data!.Count);
    }

    [Fact]
    public void TryParse_BlankDedupKeyBecomesNull_AndMissingBodyBecomesEmpty()
    {
        var json = $$"""{ "userId": "{{UserId}}", "type": "pump", "title": "x", "dedupKey": "  " }""";

        var ok = NotificationMessageParser.TryParse(Bytes(json), out var msg, out _);

        Assert.True(ok);
        Assert.Null(msg!.DedupKey);
        Assert.Equal("", msg.Body);
    }

    [Fact]
    public void RoutingKey_MatchesBinding()
    {
        Assert.Equal("notification.heatwave", MessagingNames.RoutingKeyFor(NotificationTypes.Heatwave));
        Assert.StartsWith("notification.", MessagingNames.RoutingKeyFor("anything"));
    }
}
