using System.Text.Json;
using TreeO.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Notification.Api.Data;
using Notification.Api.Models;

namespace Notification.Api.Services;

// Luu 1 tin nhan RabbitMQ thanh thong bao trong database.
public class NotificationIngestor(NotificationDbContext db)
{
    // true = da luu moi, false = bi trung (DedupKey nay da co) nen bo qua
    public async Task<bool> SaveAsync(NotificationRequested msg, CancellationToken ct = default)
    {
        if (msg.DedupKey is not null && await db.Notifications.AnyAsync(n => n.DedupKey == msg.DedupKey, ct))
            return false;

        db.Notifications.Add(new UserNotification
        {
            UserId = msg.UserId,
            Type = msg.Type,
            Title = msg.Title,
            Body = msg.Body,
            DataJson = msg.Data is { Count: > 0 } ? JsonSerializer.Serialize(msg.Data) : null,
            DedupKey = msg.DedupKey,
        });

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException) when (msg.DedupKey is not null)
        {
            // 2 tin cung khoa toi cung luc: tin kia da luu truoc -> coi nhu trung
            db.ChangeTracker.Clear();
            if (await db.Notifications.AnyAsync(n => n.DedupKey == msg.DedupKey, ct)) return false;
            throw;
        }
    }
}
