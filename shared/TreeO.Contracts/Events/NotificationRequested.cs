namespace TreeO.Contracts.Events;

// ============================================================================
// Hop dong tin nhan RabbitMQ cho thong bao (phan viec cua Than).
//
// Service nao muon gui thong bao cho nguoi dung thi publish 1 tin nhan
// NotificationRequested len exchange "treeo.events".
// Notification service nhan tin, luu vao notification_db, app doc ra o chuong thong bao.
//
// Ai gui?   Garden (moc chuoi tuoi cay), Reminder (canh bao nang nong),
//           va sau nay Sensor / may bom (Nguyen Le) chi can gui them tin cung dang nay.
// Ai nhan?  Notification service (queue "notification.requests").
// ============================================================================

// Ten exchange, queue, routing key dung chung de cac service khong go sai chinh ta
public static class MessagingNames
{
    public const string Exchange = "treeo.events";                 // exchange kieu topic
    public const string NotificationQueue = "notification.requests"; // hang doi cua Notification service
    public const string NotificationBinding = "notification.#";      // nhan moi routing key bat dau bang "notification."

    // Routing key khi gui: "notification." + Type  (vd: notification.heatwave)
    public static string RoutingKeyFor(string type) => $"notification.{type}";
}

// Cac loai thong bao app da biet cach dich sang EN/VI.
// Loai moi (vd "pump" cua may bom) van hien duoc: app se hien Title/Body do ben gui dat san.
public static class NotificationTypes
{
    public const string Heatwave = "heatwave";   // canh bao nang nong ngay mai
    public const string Streak = "streak";       // dat moc chuoi tuoi cay
    public const string Pump = "pump";           // may bom (danh cho Nguyen Le)
    public const string SoilDry = "soil_dry";      // soil below the dry threshold and auto-watering is off (Sensor)
    public const string WaterTank = "water_tank";  // pump ran but moisture did not rise: tank empty? (Sensor)
}

// Mot yeu cau gui thong bao cho 1 nguoi dung.
//   UserId        nguoi nhan (lay tu claim "sub" trong token)
//   Type          loai thong bao (xem NotificationTypes)
//   Title, Body   noi dung tieng Anh du phong (app dich theo Type neu biet Type)
//   Data          tham so cho ban dich, vd { "maxTemp": "36" }
//   DedupKey      khoa chong trung: cung khoa gui 2 lan chi luu 1 thong bao (co the null)
//   OccurredAtUtc thoi diem su kien xay ra (UTC)
public record NotificationRequested(
    Guid UserId,
    string Type,
    string Title,
    string Body,
    Dictionary<string, string>? Data,
    string? DedupKey,
    DateTime OccurredAtUtc);
