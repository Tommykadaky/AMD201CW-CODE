using System.Text.Json;
using TreeO.Contracts.Events;
using Notification.Api.Data;

namespace Notification.Api.Messaging;

// Doc va kiem tra tin nhan RabbitMQ truoc khi luu.
// Tach rieng thanh ham thuan (khong dung database, khong dung RabbitMQ) de viet unit test.
public static class NotificationMessageParser
{
    // Web = camelCase + khong phan biet hoa thuong, khop voi cach cac service khac gui
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // true: tin hop le, "message" da duoc cat gon cho vua do dai cot trong database
    // false: tin hong -> "error" noi ly do (consumer se bo tin nay, khong gui lai hang doi)
    public static bool TryParse(ReadOnlySpan<byte> body, out NotificationRequested? message, out string error)
    {
        message = null;
        error = "";

        NotificationRequested? raw;
        try
        {
            raw = JsonSerializer.Deserialize<NotificationRequested>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            error = "JSON khong hop le: " + ex.Message;
            return false;
        }

        if (raw is null)
        {
            error = "Tin nhan rong.";
            return false;
        }
        if (raw.UserId == Guid.Empty)
        {
            error = "Thieu userId.";
            return false;
        }

        var type = (raw.Type ?? "").Trim();
        var title = (raw.Title ?? "").Trim();
        if (type.Length == 0)
        {
            error = "Thieu type.";
            return false;
        }
        if (title.Length == 0)
        {
            error = "Thieu title.";
            return false;
        }

        var dedupKey = string.IsNullOrWhiteSpace(raw.DedupKey)
            ? null
            : Truncate(raw.DedupKey.Trim(), NotificationLimits.DedupKey);

        // Toi da 10 tham so, moi tham so cat con 100 ky tu
        Dictionary<string, string>? data = null;
        if (raw.Data is { Count: > 0 })
        {
            data = raw.Data
                .Take(10)
                .ToDictionary(p => Truncate(p.Key, 40), p => Truncate(p.Value ?? "", 100));
        }

        message = raw with
        {
            Type = Truncate(type, NotificationLimits.Type),
            Title = Truncate(title, NotificationLimits.Title),
            Body = Truncate((raw.Body ?? "").Trim(), NotificationLimits.Body),
            Data = data,
            DedupKey = dedupKey,
        };
        return true;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
