# Các file có sẵn đã bị sửa (để xem và duyệt)

Phần của Thân **không sửa** code backend của người khác. Mọi service của Thân (`garden`, `reminder`, `notification`, `ai-assistant`) là file mới hoặc điền vào project mẫu còn trống. Chỉ có các thay đổi sau trong **file đã có sẵn**:

## Trong dự án chung

| File | Thay đổi | Lý do |
|---|---|---|
| `shared/TreeO.Contracts/Events/NotificationRequested.cs` | **Thêm file mới** (file `Class1.cs` mẫu giữ nguyên) | Hợp đồng tin nhắn RabbitMQ dùng chung |

Các file `.csproj`, `appsettings*.json`, `launchSettings.json`, `*.http`, `Program.cs` của `Garden.Api`, `Reminder.Api`, `Notification.Api`, `AiAssistant.Api` là file mẫu do tạo project, được **ghi đè** để điền nội dung (project của Thân). Các file `WeatherForecast` mẫu được để nguyên.

Các file test `UnitTest1.cs` mẫu được để nguyên. File test mới nằm cạnh.

## Trong app (`frontend/mobile/src`), đều là file của nhóm

| File | Thay đổi | Lý do |
|---|---|---|
| `api/config.ts` | Thêm 4 địa chỉ: `GARDEN_URL`, `REMINDER_URL`, `NOTIFICATION_URL`, `AI_URL` (+ ghi chú `.env`) | App cần biết địa chỉ service mới |
| `i18n/LanguageContext.tsx` | Thêm cụm key mới vào **cả** bảng `en` và `vi` (không đổi key cũ) | Chuỗi chữ cho thời tiết, chuỗi tưới, thông báo, AI |
| `api/http.ts` | Thêm 1 dòng import `AI_URL`, hằng `AI_TIMEOUT_MS = 90_000` và chọn thời gian chờ 90 giây riêng cho địa chỉ AI (các API khác vẫn 15 giây) | Mô hình AI trả lời chậm hơn 15 giây |
| `app/_layout.tsx` | Thêm 1 dòng `<Stack.Screen name="notifications" />` trong nhóm đã đăng nhập | Đăng ký màn Thông báo |
| `app/(tabs)/index.tsx` | Màn Garden: thẻ thời tiết, chuỗi tưới, chuông lấy dữ liệu thật thay vì chữ cố định; gọi ghi nhận tưới sau khi bơm; toast có thêm dòng ghi chú. **Giữ nguyên** bố cục, bơm giả lập, độ ẩm mẫu | Tính năng thời tiết, chuỗi tưới, thông báo |
| `app/(tabs)/ai.tsx` | Chat gọi AI service thật thay vì câu trả lời "(Demo)"; bỏ 2 tin nhắn mẫu; chữ lấy từ bảng ngôn ngữ | Tính năng AI chat |

File mới trong app (không ảnh hưởng gì có sẵn): `api/weather.ts`, `api/garden.ts`, `api/notifications.ts`, `api/ai.ts`, `hooks/useGardenData.ts`, `components/StreakWeek.tsx`, `i18n/notificationText.ts`, `app/notifications.tsx`.

## Không sửa

`Identity`, `Forum`, `Sensor`, `PlantCatalog`, `Gateway`, `infra/`, `firmware/`, `docs/`, `setup.ps1`, `README.md`, `TreeO.slnx` (đã có sẵn đủ các project), `HUONG-DAN-TAM.md`.

## Điểm lệch phát hiện trong zip (không do Thân gây ra)

- `services/forum/Forum.Api/Program.cs` trong zip vẫn là bản mẫu (chưa có kiểm tra JWT), và Identity chưa có API đổi mật khẩu, trong khi `HUONG-DAN-TAM.md` mô tả đã có. Có vẻ bản code mới nhất của Tâm chưa nằm trong zip. Cần đồng bộ lại trước khi demo cả hệ thống.
- Cổng Forum: `launchSettings.json` ghi 5296, hướng dẫn của Tâm ghi 5107.
