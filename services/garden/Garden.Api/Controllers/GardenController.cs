using System.Globalization;
using Garden.Api.Data;
using Garden.Api.DTOs;
using Garden.Api.Messaging;
using Garden.Api.Models;
using Garden.Api.Services;
using TreeO.Contracts.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Garden.Api.Controllers;

// Moi API o day deu can dang nhap (gui kem token). Moi nguoi chi thay chuoi cua chinh minh.
[ApiController]
[Route("api/garden")]
[Authorize]
public class GardenController(
    GardenDbContext db,
    INotificationPublisher publisher,
    IConfiguration config) : ControllerBase
{
    // Ngay doi o 00:00 gio Viet Nam (UTC+7). Chinh trong appsettings.json: Streak:UtcOffsetHours
    private int UtcOffsetHours => config.GetValue("Streak:UtcOffsetHours", 7);

    // GET /api/garden/streak   -> chuoi tuoi cay hien tai
    [HttpGet("streak")]
    public async Task<ActionResult<StreakDto>> GetStreak()
    {
        if (CurrentUser.Id(User) is not Guid me) return Unauthorized();

        var today = StreakCalculator.TodayAt(DateTime.UtcNow, UtcOffsetHours);
        return Ok(await BuildStreakAsync(me, today));
    }

    // POST /api/garden/watering   -> ghi nhan "hom nay da tuoi cay"
    // Tuoi nhieu lan trong ngay van chi tinh 1 ngay. Dat moc 3/7/14/30... ngay thi gui thong bao.
    [HttpPost("watering")]
    public async Task<ActionResult<WateringResultDto>> RecordWatering()
    {
        if (CurrentUser.Id(User) is not Guid me) return Unauthorized();

        var now = DateTime.UtcNow;
        var today = StreakCalculator.TodayAt(now, UtcOffsetHours);

        var row = await db.WateringDays.FindAsync(me, today);
        var firstTimeToday = row is null;
        if (row is null)
        {
            db.WateringDays.Add(new WateringDay { UserId = me, Date = today, Count = 1, LastWateredAtUtc = now });
        }
        else
        {
            row.Count++;
            row.LastWateredAtUtc = now;
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException) when (firstTimeToday)
        {
            // 2 yeu cau cung luc deu thay "hom nay chua co dong": yeu cau kia da luu truoc -> day chi la lan tuoi them
            db.ChangeTracker.Clear();
            var existing = await db.WateringDays.FindAsync(me, today);
            if (existing is null) throw;
            existing.Count++;
            existing.LastWateredAtUtc = now;
            await db.SaveChangesAsync();
            firstTimeToday = false;
        }

        var streak = await BuildStreakAsync(me, today);

        // Thong bao chi gui o lan tuoi dau tien trong ngay va dung moc
        var milestone = firstTimeToday && StreakCalculator.IsMilestone(streak.Current);
        if (milestone)
        {
            await publisher.PublishAsync(new NotificationRequested(
                UserId: me,
                Type: NotificationTypes.Streak,
                Title: $"{streak.Current}-day care streak!",
                Body: $"You've watered your plant {streak.Current} days in a row. Keep it up!",
                Data: new Dictionary<string, string> { ["days"] = streak.Current.ToString(CultureInfo.InvariantCulture) },
                DedupKey: $"streak:{me}:{Iso(today)}",
                OccurredAtUtc: now));
        }

        return Ok(new WateringResultDto(streak, firstTimeToday, milestone));
    }

    private async Task<StreakDto> BuildStreakAsync(Guid userId, DateOnly today)
    {
        var rows = await db.WateringDays.AsNoTracking().Where(w => w.UserId == userId).ToListAsync();
        var summary = StreakCalculator.Compute(rows.Select(r => r.Date), today);

        // 7 ngay gan nhat, ngay cuoi la hom nay
        var wateredSet = rows.Select(r => r.Date).ToHashSet();
        var week = Enumerable.Range(0, 7)
            .Select(i => today.AddDays(i - 6))
            .Select(d => new StreakDayDto(Iso(d), wateredSet.Contains(d)))
            .ToList();

        DateTime? lastAt = rows.Count > 0 ? rows.Max(r => r.LastWateredAtUtc) : null;

        return new StreakDto(
            summary.Current,
            summary.Longest,
            summary.WateredToday,
            summary.AtRisk,
            summary.TotalDays,
            summary.LastWateredDate is DateOnly last ? Iso(last) : null,
            lastAt,
            week);
    }

    // Luon dung dinh dang yyyy-MM-dd, khong phu thuoc ngon ngu/lich cua may chu
    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
