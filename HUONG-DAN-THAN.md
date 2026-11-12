# Phần của Thân: Thời tiết, Chuỗi tưới cây, Thông báo (RabbitMQ), AI Chat

Tài liệu này giải thích code đã làm cho 4 phần việc của Thân, cách chạy, cách test, và từng file làm gì. Mục tiêu là đọc xong thì hiểu được code và tự trả lời được khi giảng viên hỏi.

> **Quan trọng, đọc trước:** code C# trong phần này được viết trong môi trường không cài được .NET, nên **chưa được biên dịch hay chạy test ở đó**. Phần app (TypeScript) đã kiểm tra bằng `npx tsc --noEmit` và không có lỗi. Việc đầu tiên khi nhận code là chạy mục 2.1 (build + test). Nếu có lỗi biên dịch, gửi nguyên thông báo lỗi để sửa.

---

## 1. Đã làm những gì

| Phần | Tính năng | Ở đâu |
|---|---|---|
| **Thời tiết** | Lấy thời tiết thật (nhiệt độ, độ ẩm, dự báo 3 ngày) từ Open-Meteo, không cần API key | `services/reminder` |
| | Cache 10 phút để không gọi Open-Meteo liên tục | `Reminder.Api/Services/WeatherService.cs` |
| | Phát hiện **nắng nóng** (≥ 35°C, đổi được trong cấu hình) và gửi thông báo | `Reminder.Api/Controllers/WeatherController.cs` |
| | Thẻ thời tiết trên màn Garden dùng dữ liệu thật, tự dịch EN/VI | app `src/app/(tabs)/index.tsx` |
| **Chuỗi tưới cây** (kiểu Duolingo) | Lưu mỗi ngày đã tưới vào database `garden_db` | `services/garden` |
| | Tính chuỗi hiện tại, chuỗi dài nhất, 7 chấm trong tuần, "sắp mất chuỗi" | `Garden.Api/Services/StreakCalculator.cs` |
| | Đạt mốc 3/7/14/30/60/100/200/365 ngày thì gửi thông báo | `Garden.Api/Controllers/GardenController.cs` |
| **Thông báo (RabbitMQ)** | Notification service nhận tin từ RabbitMQ, lưu `notification_db` | `services/notification` |
| | Garden và Reminder là bên **gửi** (publisher), Notification là bên **nhận** (consumer) | `*/Messaging/` |
| | Chuông trên màn Garden có số chưa đọc, màn Thông báo: đọc, đọc hết, xóa | app `src/app/notifications.tsx` |
| **AI chat** | AI Assistant service nhận câu hỏi, gọi Google Gemini (miễn phí) hoặc Claude, trả lời | `services/ai-assistant` |
| | 2 chế độ Care Plan / Feng Shui, giới hạn 10 câu/phút/người | `AiAssistant.Api` |
| | Màn AI trong app gọi service thật thay vì câu trả lời mẫu | app `src/app/(tabs)/ai.tsx` |

**Chưa làm (ngoài phạm vi):**
- Thông báo của máy bơm (phần của Nguyên Lê). Hợp đồng tin nhắn đã có sẵn loại `pump`, Nguyên Lê chỉ cần gửi thêm một tin cùng dạng (xem mục 4.4).
- Thông báo đẩy ra điện thoại (push notification). Hiện thông báo nằm trong app, bấm chuông để xem.
- Vị trí thời tiết lấy theo GPS. Hiện mặc định TP. Hồ Chí Minh (API đã hỗ trợ `lat`/`lon`).
- Chuỗi tưới đang được ghi khi bấm **Water now** trên app (bơm đang là giả lập 3 giây, bơm thật là phần của Nguyên Lê).
- AI không nhớ cuộc trò chuyện sau khi đóng app (app gửi lại lịch sử mỗi lần hỏi).
- AI không chẩn đoán bệnh cây (cố ý, có ghi trong câu nhắc hệ thống).

**Sửa vào file của người khác:** có, rất ít, đều là file app. Danh sách đầy đủ nằm trong `THAY-DOI-FILE-GOC.md` để xem và duyệt. Toàn bộ phần backend là file mới trong thư mục service của Thân.

---

## 2. Cài đặt lần đầu

Làm sau khi đã chạy được Identity theo `README.md` ở thư mục gốc (Docker Desktop đã bật, đã có tài khoản `champ@test.com`).

### 2.1. Build và chạy test (làm đầu tiên)

Ở thư mục gốc dự án:

```powershell
dotnet build TreeO.slnx
dotnet test services/garden/Garden.Tests
dotnet test services/reminder/Reminder.Tests
dotnet test services/notification/Notification.Tests
dotnet test services/ai-assistant/AiAssistant.Tests
```

Đúng khi: build `0 Error(s)`, mỗi lệnh test báo `Passed` và `Failed: 0`. Các test này **không cần** Docker, mạng hay API key vì chỉ kiểm tra hàm tính toán thuần.

Nếu `dotnet build` báo lỗi package, chạy `dotnet restore TreeO.slnx` rồi build lại.

### 2.2. Cổng của các service

| Service | Cổng | Ghi chú |
|---|---|---|
| Identity | 5101 | của Tâm |
| Garden | **5103** | chuỗi tưới cây |
| Reminder | **5104** | thời tiết |
| Notification | **5105** | thông báo |
| AI Assistant | **5106** | AI chat |
| Forum | 5107 | của Tâm (xem lưu ý dưới) |

> **Lưu ý:** file mẫu của AI Assistant ban đầu dùng cổng 5107, trùng với Forum trong hướng dẫn của Tâm. Nên AI đổi sang **5106**. Trong file `launchSettings.json` của Forum trong zip là 5296, nếu Forum chạy sai cổng thì chạy `dotnet run --urls http://localhost:5107`.

### 2.3. Tạo bảng cho Garden và Notification

Hai service này có database nên cần tạo migration **một lần** (giống Tâm làm cho Forum). Reminder và AI không dùng database.

```powershell
dotnet tool install --global dotnet-ef        # chỉ lần đầu, bỏ qua nếu đã có

cd services/garden/Garden.Api
dotnet ef migrations add InitialCreate -o Data/Migrations

cd ../../notification/Notification.Api
dotnet ef migrations add InitialCreate -o Data/Migrations
```

Khi service chạy, nó tự tạo bảng từ migration này (`MigrateAsync` trong `Program.cs`). Database `garden_db` và `notification_db` đã được tạo sẵn bởi `infra/postgres/init.sql`.

### 2.4. Khai báo mật khẩu (user-secrets)

Mật khẩu **không nằm trong code**. Thay `MAT_KHAU_POSTGRES` và `MAT_KHAU_RABBITMQ` bằng giá trị trong `infra/.env`.

```powershell
# Garden
cd services/garden/Garden.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=garden_db;Username=treeo;Password=MAT_KHAU_POSTGRES"
dotnet user-secrets set "RabbitMq:Password" "MAT_KHAU_RABBITMQ"

# Notification
cd ../../notification/Notification.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=notification_db;Username=treeo;Password=MAT_KHAU_POSTGRES"
dotnet user-secrets set "RabbitMq:Password" "MAT_KHAU_RABBITMQ"

# Reminder (chỉ cần RabbitMQ)
cd ../../reminder/Reminder.Api
dotnet user-secrets set "RabbitMq:Password" "MAT_KHAU_RABBITMQ"
```

**Không** sửa mật khẩu trong `appsettings.Development.json`, để nguyên chữ mẫu `DOI_THANH_POSTGRES_PASSWORD_TRONG_ENV`.

### 2.5. API key cho AI (Google Gemini, có gói miễn phí)

AI Assistant mặc định gọi **Google Gemini** (`Ai:Provider` = `gemini`). Tạo key miễn phí ở aistudio.google.com (đăng nhập tài khoản Google, bấm **Get API key**, không cần thẻ):

```powershell
cd services/ai-assistant/AiAssistant.Api
dotnet user-secrets set "Gemini:ApiKey" "AIza..."
```

Rồi **tắt AI (`Ctrl + C`) và chạy lại `dotnet run`** (service chỉ đọc key lúc khởi động).

- Key **chỉ nằm trên máy**, không commit lên GitHub, không để vào `appsettings`, không gửi trong chat.
- Chưa đặt key thì AI trả `503` với câu "The AI service isn't set up yet". Các tính năng còn lại không bị ảnh hưởng.
- Báo "model wasn't found" (`404`): Google đã đổi tên model. Xem danh sách model dùng được với key của bạn rồi sửa `Gemini:Model` trong `appsettings.json`:
  ```powershell
  curl.exe -H "x-goog-api-key: AIza..." "https://generativelanguage.googleapis.com/v1beta/models"
  ```
  Chọn tên model có `generateContent` và dạng "flash" (bỏ chữ `models/` ở đầu).
- Báo "Can't reach the AI service right now" và terminal AI ghi `canceled due to the configured HttpClient.Timeout of 60 seconds`: model đó **treo** với key của bạn (đã gặp với `gemini-3.7-flash` và `gemini-3.8-flash`, trong khi `gemini-3.5-flash`, `gemini-3.6-flash`, `gemini-3.5-flash-lite` trả lời trong 1 đến 3 giây). Cách thử từng model bằng `curl.exe` với giới hạn 15 giây: gửi `{"contents":[{"parts":[{"text":"Say hi"}]}]}` đến `.../v1beta/models/<tên>:generateContent`; model nào trả `HTTP 200` nhanh thì dùng. Mặc định hiện tại là `gemini-3.6-flash`.
- Gói miễn phí có giới hạn số câu mỗi phút/ngày; quá hạn mức thì báo "busy or the free quota is used up". Cùng với giới hạn `Ai:RequestsPerMinute` (mặc định 10) của mình.
- Muốn dùng Claude thay Gemini: đặt `"Ai:Provider": "anthropic"` trong `appsettings.json` và `dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."` (API này tính tiền).

### 2.6. Cập nhật app

Chép đè thư mục `frontend/mobile/src` mới (nếu chưa dùng zip cả dự án). Chạy trên điện thoại thì `frontend/mobile/.env` cần đủ các dòng:

```
EXPO_PUBLIC_API_URL=http://<IP-máy-tính>:5101
EXPO_PUBLIC_FORUM_URL=http://<IP-máy-tính>:5107
EXPO_PUBLIC_GARDEN_URL=http://<IP-máy-tính>:5103
EXPO_PUBLIC_REMINDER_URL=http://<IP-máy-tính>:5104
EXPO_PUBLIC_NOTIFICATION_URL=http://<IP-máy-tính>:5105
EXPO_PUBLIC_AI_URL=http://<IP-máy-tính>:5106
```

và chạy mỗi service bằng `dotnet run --urls http://0.0.0.0:<cổng>`. Điện thoại và máy tính phải cùng Wi-Fi.

---

## 3. Cách mở và chạy ứng dụng

### Mỗi lần làm việc cần chạy

Mở **6 terminal** trong VS Code (dấu `+` ở khung terminal), mỗi cái chạy một việc:

| # | Việc | Lệnh |
|---|---|---|
| 1 | Docker | Mở Docker Desktop, chờ **Engine running**, rồi `cd infra` → `docker compose up -d` |
| 2 | Identity | `cd services/identity/Identity.Api` → `dotnet run` |
| 3 | Notification | `cd services/notification/Notification.Api` → `dotnet run` |
| 4 | Garden | `cd services/garden/Garden.Api` → `dotnet run` |
| 5 | Reminder | `cd services/reminder/Reminder.Api` → `dotnet run` |
| 6 | AI | `cd services/ai-assistant/AiAssistant.Api` → `dotnet run` |
| 7 | App | `cd frontend/mobile` → `npm install` (lần đầu) → `npx expo start` |

(Cần thêm Forum của Tâm nếu muốn dùng tab Community.)

Mỗi service chạy thành công khi thấy `Now listening on: http://localhost:<cổng>`. Mở `http://localhost:<cổng>/health` trên trình duyệt phải ra `{"status":"ok",...}`.

**Notification phải chạy trước hoặc cùng lúc** với Garden/Reminder, nhưng không bắt buộc: RabbitMQ giữ tin trong hàng đợi `notification.requests` cho đến khi Notification chạy (miễn là hàng đợi đã được tạo, tức là Notification đã chạy ít nhất một lần).

### Mở app

Sau `npx expo start`, terminal hiện mã QR và menu phím:
- **Trình duyệt trên máy tính:** bấm phím `w`. App mở ở `http://localhost:8081`. Cách này không cần chỉnh IP.
- **Điện thoại:** cài **Expo Go**, quét mã QR (cùng Wi-Fi, đã tạo `.env` như mục 2.6). Sau khi sửa `.env` thì chạy `npx expo start -c`.
- Đăng nhập bằng `champ@test.com` / `12345678` (hoặc tài khoản bạn đã đăng ký).

---

## 4. Test

### 4.1. Bằng file `.http` (REST Client)

Mỗi service có file `.http` riêng, mở file rồi bấm **Send Request** từng request từ trên xuống.

**Reminder** (`services/reminder/Reminder.Api/Reminder.Api.http`, cần Internet):

| Request | Kết quả đúng | Chứng minh điều gì |
|---|---|---|
| 1. `/health` | `200` | Service đang chạy |
| 3. Thời tiết mặc định | `200`, có `current`, `forecast` 3 ngày, `heatwave` | Gọi được Open-Meteo |
| 4. Thời tiết Hà Nội | `200`, `locationName` là Ha Noi | Nhận tọa độ từ người gọi |
| 5. Tọa độ sai | `400` | Kiểm tra dữ liệu đầu vào |
| 6. Không gửi token | `401` | **Bảo mật** |

**Garden** (`Garden.Api.http`):

| Request | Kết quả đúng | Chứng minh điều gì |
|---|---|---|
| Xem chuỗi lúc đầu | `200`, `current: 0`, `week` có 7 ngày | Người mới chưa có chuỗi |
| Ghi nhận tưới | `200`, `current: 1`, `firstTimeToday: true` | Lưu vào database |
| Ghi nhận tưới lần 2 cùng ngày | `current` vẫn là 1, `firstTimeToday: false` | Tưới nhiều lần/ngày chỉ tính 1 ngày |
| Không gửi token | `401` | **Bảo mật** |

**Notification** (`Notification.Api.http`): xem danh sách (`200`), đánh dấu đọc hết (`204`, danh sách lại có `unreadCount: 0`), không gửi token (`401`), đánh dấu id không tồn tại (`404`).

**AI** (`AiAssistant.Api.http`): chat Care Plan (`200` có `reply`, hoặc `503` nếu chưa đặt key), mode sai (`400`), tin cuối không phải của user (`400`), không gửi token (`401`).

### 4.2. Test RabbitMQ (luồng thông báo từ đầu đến cuối)

**Cách 1: gửi tay bằng trang quản lý RabbitMQ** (không cần chờ nhiều ngày)

1. Lấy `id` của bạn: chạy request `/api/auth/me` trong `Identity.Api.http` (trường `id`).
2. Mở `http://localhost:15672`, đăng nhập bằng `RABBITMQ_USER` / `RABBITMQ_PASSWORD` trong `infra/.env`.
3. Tab **Exchanges** → bấm `treeo.events` → mục **Publish message**:
   - **Routing key:** `notification.test`
   - **Payload:**
     ```json
     {"userId":"<id-của-bạn>","type":"pump","title":"Pump finished","body":"Soil is now 65%","dedupKey":"demo-1"}
     ```
   - Bấm **Publish message**. Hiện "Message published".
4. Trên app, bấm chuông (hoặc gọi `GET /api/notifications`): thấy thông báo "Pump finished".
5. Publish lại **cùng** `dedupKey` → vẫn chỉ có **1** thông báo (chống trùng).

(`treeo.events` chỉ xuất hiện sau khi Notification đã chạy ít nhất một lần.)

**Cách 2: thông báo nắng nóng thật**

1. Trong `Reminder.Api/appsettings.json` đổi `Weather:HeatwaveThresholdC` thành `20`, chạy lại Reminder.
2. Mở app (hoặc gọi `GET /api/weather`).
3. Thẻ thời tiết hiện dòng **Heatwave tomorrow**, và sau vài giây chuông có thông báo "Heatwave tomorrow". Gọi thêm lần nữa không tạo thêm thông báo.
4. Test xong nhớ đổi ngưỡng về `35`.

**Cách 3: mốc chuỗi 3 ngày**

Chuỗi cần 3 ngày liên tiếp. Để không phải chờ, chèn 2 ngày trước vào database (thay `<id>` bằng id của bạn, dùng Docker):

```powershell
docker exec -it treeo-postgres psql -U treeo -d garden_db
```

Khi hiện dấu nhắc `garden_db=#`, dán câu SQL này (một dòng), Enter, rồi gõ `\q` để thoát:

```sql
INSERT INTO "WateringDays" ("UserId","Date","Count","LastWateredAtUtc") VALUES ('<id>', CURRENT_DATE - 2, 1, now()), ('<id>', CURRENT_DATE - 1, 1, now());
```

Rồi bấm **Water now** trên app: chuỗi thành 3, thông báo "3-day care streak!" xuất hiện ở chuông. (Ngày trong bảng là ngày theo giờ Việt Nam, nên nếu máy chạy Postgres đang ở múi giờ UTC và đang gần nửa đêm thì có thể lệch 1 ngày; khi đó đổi `CURRENT_DATE` cho đúng ngày Việt Nam.)

**Kiểm tra RabbitMQ không làm hỏng API:** tắt container RabbitMQ (`docker stop treeo-rabbitmq`), gọi `GET /api/weather` hoặc bấm tưới. API vẫn trả kết quả bình thường (chỉ không có thông báo). Bật lại bằng `docker start treeo-rabbitmq`.

### 4.3. Trên app

1. Đăng nhập → màn Garden: thẻ thời tiết hiện nhiệt độ, tình trạng và độ ẩm thật (lúc đầu "Loading weather…").
2. Tắt Reminder rồi mở lại app: thẻ hiện "Weather isn't available right now." chứ app không sập.
3. Bấm **Water now** → sau 3 giây có toast; dòng chuỗi chuyển thành "1-Day Care Streak", chấm của hôm nay thành dấu tick xanh.
4. Bấm **Water now** lần nữa → chuỗi vẫn là 1.
5. Chèn dữ liệu theo "Cách 3" → bấm tưới → toast có thêm dòng "Streak milestone!", chuông hiện số đỏ.
6. Bấm chuông → màn Notifications: thông báo chưa đọc có chấm xanh. Bấm vào thì mờ đi, "Mark all as read" đọc hết, dấu × để xóa.
7. Settings → VI: thẻ thời tiết, chuỗi, thông báo, màn AI đều chuyển tiếng Việt (nội dung thông báo nắng nóng/chuỗi cũng được dịch).
8. Tab AI: gõ câu hỏi → hiện "TreeO AI is thinking…" rồi câu trả lời. Đổi sang Feng Shui thì cuộc trò chuyện được làm mới. Chưa đặt API key thì thấy lỗi màu đỏ trong khung chat.

---

## 5. Code hoạt động thế nào

### 5.1. Luồng tổng quát

```
App ──(JWT)──> Reminder  ──┐
App ──(JWT)──> Garden    ──┼─ publish NotificationRequested ─> RabbitMQ (exchange treeo.events)
                           │                                        │ routing key "notification.<type>"
                           │                                        ▼
                           │                              queue notification.requests
                           │                                        │
App <─(JWT)── Notification <────── consumer đọc, kiểm tra, lưu notification_db
App ──(JWT)──> AI Assistant ──> API của Google Gemini (hoặc Anthropic)
```

Mỗi service **tự kiểm tra JWT** bằng cùng `Jwt:Issuer/Audience/Key` với Identity (cùng cách Forum của Tâm làm), nên `Jwt` của các service mới phải **giống hệt** Identity.

### 5.2. Hợp đồng tin nhắn (`shared/TreeO.Contracts/Events/NotificationRequested.cs`)

Một file dùng chung cho cả bên gửi và bên nhận, để không gõ sai tên exchange/queue/routing key. `NotificationRequested` gồm: người nhận (`UserId`), loại (`Type`), tiêu đề/nội dung tiếng Anh dự phòng, tham số `Data` (vd `maxTemp`), khóa chống trùng `DedupKey`, thời điểm.

Loại `heatwave`, `streak` được app dịch sang EN/VI bằng `Data` (`src/i18n/notificationText.ts`). Loại lạ (vd `pump`) thì app hiện thẳng `Title`/`Body` do bên gửi đặt, nên Nguyên Lê chỉ việc gửi thêm tin `type: "pump"` mà không cần sửa Notification.

### 5.3. Reminder: thời tiết

- `OpenMeteoClient`: gọi `api.open-meteo.com/v1/forecast` lấy thời tiết hiện tại và dự báo theo ngày.
- `WeatherInterpreter` (hàm thuần, có test): đổi mã thời tiết WMO của Open-Meteo thành nhãn ngắn (`clear`, `rain`...), tính nắng nóng (`maxC ≥ ngưỡng`), bỏ qua ngày thiếu dữ liệu, báo lỗi nếu thiếu trường bắt buộc.
- `WeatherService`: cache `IMemoryCache` theo vị trí (làm tròn tọa độ) 10 phút.
- `WeatherController`: kiểm tra tọa độ (`400` nếu sai), Open-Meteo lỗi thì trả `502` kèm thông báo dễ hiểu. Nếu ngày mai nắng nóng thì **gửi thông báo không chờ** (fire-and-forget) để RabbitMQ chậm không làm chậm API. Khóa chống trùng `heatwave:{user}:{ngày mai}` nên mỗi người chỉ nhận 1 thông báo cho mỗi ngày nắng nóng.

### 5.4. Garden: chuỗi tưới cây

- Bảng `WateringDays`: khóa chính 2 cột (`UserId`, `Date`), nên một người chỉ có tối đa 1 dòng mỗi ngày, tưới nhiều lần chỉ tăng `Count`.
- `StreakCalculator.Compute` (hàm thuần, có test) tính chuỗi dựa trên **giờ Việt Nam (UTC+7)**: chuỗi = số ngày liên tiếp kết thúc hôm nay, **hoặc hôm qua** nếu hôm nay chưa tưới (còn thời hạn đến hết ngày, giống Duolingo). Quá ngày đó mà chưa tưới thì chuỗi về 0. `AtRisk` = có chuỗi nhưng hôm nay chưa tưới.
- Hai request tưới cùng lúc: nếu cùng lúc cả hai thấy "hôm nay chưa có dòng" thì một cái bị lỗi trùng khóa, code bắt lỗi đó và coi là lần tưới thêm (không văng 500).
- Chỉ ở **lần tưới đầu tiên của ngày** và đúng mốc mới gửi thông báo, khóa chống trùng `streak:{user}:{ngày}`.

### 5.5. Notification: nhận từ RabbitMQ

- `NotificationConsumer` là `BackgroundService` chạy cùng service: khai báo exchange (topic) `treeo.events`, queue `notification.requests`, nối bằng `notification.#`. Nếu chưa kết nối được RabbitMQ thì thử lại sau 5 giây, **không làm sập** service.
- **Ack thủ công**: chỉ báo "đã xử lý" sau khi lưu database xong. Lỗi tạm thời (database) thì `nack` và đưa tin lại hàng đợi; tin hỏng (JSON sai, thiếu `userId`/`title`) thì bỏ, nếu cứ đưa lại sẽ lặp vô hạn.
- `NotificationMessageParser` (hàm thuần, có test): đọc JSON, kiểm tra, cắt ngắn cho vừa độ dài cột database.
- `NotificationIngestor`: lưu, **bỏ qua nếu `DedupKey` đã có** (có unique index trong database; đua nhau thì bắt lỗi trùng khóa).
- `NotificationsController`: mọi API đều lọc theo `UserId` lấy từ token, nên người này không đọc/xóa được thông báo người khác (đánh dấu id của người khác ra `404`).

### 5.6. AI Assistant

- `ChatController` (`POST /api/ai/chat`, cần đăng nhập, có rate limit): kiểm tra yêu cầu bằng `ChatRules.Validate` (chế độ hợp lệ, vai trò `user`/`assistant`, mỗi tin ≤ 1000 ký tự, tin cuối là của user), cắt lịch sử còn 20 tin gần nhất, ghép các tin liên tiếp cùng vai trò (API của AI yêu cầu xen kẽ), tạo câu nhắc hệ thống theo chế độ và tên người dùng.
- `GeminiChatClient` (mặc định) và `AnthropicChatClient` cùng cài giao diện `IChatClient`, chọn bằng cấu hình `Ai:Provider`. Cả hai gọi REST bằng `HttpClient`; Gemini dùng vai trò `user`/`model`, hệ thống nhắc đặt ở `systemInstruction`. Lỗi từ AI được đổi thành câu tiếng Anh an toàn (`401` thành "rejected our API key", `429` thành "busy"...), **không** lộ nội dung lỗi gốc cho người dùng.
- API key nằm trong user-secrets, **không bao giờ gửi xuống app**: app chỉ gọi service của mình.
- Câu nhắc hệ thống yêu cầu AI không chẩn đoán bệnh cây, bám chủ đề cây, trả lời theo ngôn ngữ người hỏi.

### 5.7. App

- `src/api/weather.ts`, `garden.ts`, `notifications.ts`, `ai.ts`: các hàm gọi API, dùng `authFetch` có sẵn nên tự gắn token và tự gia hạn token.
- `src/hooks/useGardenData.ts`: `useWeather`, `useStreak`, `useUnreadCount`, tải lại mỗi lần quay về màn Garden, lỗi thì lưu vào `error` chứ không làm sập màn hình.
- `src/components/StreakWeek.tsx`: 7 chấm của tuần.
- `src/app/notifications.tsx`: màn Thông báo. Đọc/xóa cập nhật giao diện ngay, nếu server báo lỗi thì tải lại để lấy trạng thái thật.
- Mọi chuỗi chữ mới đều có trong **cả** bảng `en` và `vi` của `LanguageContext.tsx` (`vi: typeof en` nên thiếu key là TypeScript báo lỗi).

---

## 6. Câu hỏi giảng viên có thể hỏi

**Vì sao dùng RabbitMQ thay vì Garden gọi thẳng Notification?**
Hai service không phụ thuộc nhau. Notification sập hoặc chưa chạy thì tin vẫn nằm trong hàng đợi, bật lại là nhận đủ. Thêm bên gửi mới (máy bơm của Nguyên Lê) chỉ cần gửi cùng dạng tin, không sửa Notification.

**Nếu RabbitMQ sập thì API thời tiết, tưới cây có sập theo không?**
Không. Publisher có giới hạn thời gian kết nối (3 giây) và gửi (5 giây), lỗi thì chỉ ghi log và trả `false`, không ném lỗi ra. Người dùng vẫn nhận thời tiết/chuỗi, chỉ không có thông báo lần đó.

**Vì sao cần `DedupKey`?**
RabbitMQ đảm bảo "ít nhất một lần", tức có thể giao một tin hai lần, và app có thể gọi API thời tiết nhiều lần trong ngày. Khóa chống trùng + unique index trong database đảm bảo mỗi sự kiện chỉ thành một thông báo.

**Ack thủ công là gì, vì sao dùng?**
Nếu để RabbitMQ tự ack ngay khi giao tin, mà database lỗi giữa chừng thì tin mất. Ack thủ công chỉ báo đã xong sau khi lưu thành công; lỗi tạm thì đưa tin lại hàng đợi.

**Vì sao tin hỏng thì bỏ chứ không đưa lại hàng đợi?**
Tin JSON sai sẽ luôn sai, đưa lại chỉ lặp vô hạn. Chỉ lỗi tạm thời (database) mới đáng thử lại.

**Chuỗi tưới tính theo giờ nào, và "mất chuỗi" khi nào?**
Giờ Việt Nam (UTC+7, cấu hình `Streak:UtcOffsetHours`). Hôm qua có tưới mà hôm nay chưa thì chuỗi còn giữ đến hết ngày (app nhắc "Water today to keep your streak"). Qua hết ngày mà chưa tưới thì về 0.

**Vì sao cache thời tiết 10 phút?**
Thời tiết không đổi từng giây, nên nhiều người dùng chung một vị trí chỉ gọi Open-Meteo một lần, nhanh hơn và không vượt giới hạn của dịch vụ miễn phí.

**API key của AI bảo vệ thế nào?**
Chỉ nằm trên máy chủ (user-secrets), app không bao giờ thấy key. Mọi lời gọi cần JWT và bị giới hạn 10 câu/phút/người, tránh bị lạm dụng làm tốn tiền.

**AI có thể trả lời sai không?**
Có, đây là mô hình ngôn ngữ. Vì vậy câu nhắc hệ thống cấm chẩn đoán bệnh cây, và app có ghi chú "TreeO AI does not diagnose plant diseases". Phong thủy được nói rõ là truyền thống văn hóa, không phải khoa học.

**Test nào đã viết và kiểm tra gì?**
`Garden.Tests`: tính chuỗi (rỗng, liên tiếp, đứt chuỗi, hôm nay chưa tưới, múi giờ, mốc). `Reminder.Tests`: đọc dữ liệu Open-Meteo, nắng nóng, mã thời tiết, thiếu trường. `Notification.Tests`: đọc/kiểm tra/cắt ngắn tin RabbitMQ. `AiAssistant.Tests`: kiểm tra yêu cầu chat, cắt lịch sử, ghép tin, đọc phản hồi AI (cả Gemini và Claude). Đều là hàm thuần nên chạy không cần Docker hay mạng.
