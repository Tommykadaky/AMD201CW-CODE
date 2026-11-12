# Phần của Nguyên Lê: Cảm biến độ ẩm, Máy bơm, Thông báo bơm

Tài liệu này giải thích code của phần phần cứng và Sensor service: đã làm gì, cách chạy, cách test, từng file làm gì. Mục tiêu là đọc xong thì hiểu được code và tự trả lời được khi giảng viên hỏi.

> **Quan trọng, đọc trước:** phần C# được viết trong môi trường không cài được .NET, nên **chưa được biên dịch hay chạy test ở đó**. Đã kiểm tra được: app (TypeScript) qua `npx tsc --noEmit` và build bản web thành công; firmware qua kiểm tra cú pháp C++ với thư viện ArduinoJson thật; simulator đã chạy thử với một server giả. Việc đầu tiên khi nhận code là chạy mục 2.1 (build + test). Nếu có lỗi biên dịch, gửi nguyên thông báo lỗi để sửa.

---

## 1. Đã làm những gì

### 1.1. Code cũ có vấn đề gì

| Chỗ | Vấn đề trong code cũ | Đã sửa thành |
|---|---|---|
| `Sensor.Api` | Chỉ `Console.WriteLine`, không lưu gì | Lưu vào `sensor_db` (3 bảng: thiết bị, mẫu độ ẩm, lần bơm) |
| `Sensor.Api` | Ai cũng gửi được dữ liệu giả | Mỗi thiết bị có **device key** riêng, gửi trong header `X-Device-Key` |
| `Sensor.Api` | App không lấy được dữ liệu | Có API cho app: danh sách thiết bị, lịch sử 24h, tưới ngay, bật/tắt tự tưới |
| `Sensor.Api` | `UseHttpsRedirection` trong khi ESP32 chỉ gửi HTTP | Bỏ, ESP32 gọi HTTP trong mạng LAN |
| `Sensor.Api` | Chữ tiếng Việt có dấu trong code, còn file mẫu `WeatherForecast` | Code toàn tiếng Anh, đã xóa file mẫu |
| Firmware | **Mật khẩu Wi-Fi nằm trong code và đã lên GitHub** | Chuyển ra `include/config.h`, file này bị `.gitignore` chặn |
| Firmware | `delay(5000)` chặn cả vòng lặp: bơm có thể chạy quá 5 giây | Vòng lặp không chặn (`millis()`), bơm luôn được kiểm tra |
| Firmware | Bật bơm khi < 30%, tắt ngay khi ≥ 30%: **bơm bật tắt liên tục** quanh mốc 30 | Có khoảng đệm (hysteresis): bật dưới 30%, tắt khi tới 60% |
| Firmware | Không giới hạn thời gian bơm (cảm biến hỏng là bơm chạy mãi, tràn nước) | Tối đa 8 giây/lần tự tưới, nghỉ 1 phút giữa 2 lần, dừng cứng ở 85% |
| Firmware | Mất Wi-Fi lúc khởi động là kẹt mãi trong `while` | Tự kết nối lại mỗi 10 giây, vẫn tưới bình thường khi không có mạng |
| Firmware | Đọc ADC 1 lần nên số nhảy lung tung | Lấy trung bình 10 lần đọc rồi làm mượt |
| Firmware | Không nghe lệnh từ server | Đọc phản hồi: ngưỡng độ ẩm, bật/tắt tự tưới, lệnh "Tưới ngay" |
| App | Độ ẩm 42%, biểu đồ, nhật ký, thẻ IoT đều là **dữ liệu mẫu**; nút Water now chỉ giả lập | Tất cả lấy từ Sensor service. Chưa ghép cảm biến thì vẫn hiện dữ liệu mẫu để demo được |

### 1.2. Tính năng mới

| Tính năng | Ở đâu |
|---|---|
| Lưu độ ẩm, nhưng **không lưu mọi mẫu 5 giây**: chỉ lưu mỗi 60 giây, hoặc khi độ ẩm đổi ≥ 3%, hoặc khi bơm bật/tắt. Giảm khoảng 17.000 dòng/ngày xuống khoảng 1.500 mà không mất thông tin | `Services/ReadingPolicy.cs` → `ShouldStore` |
| Tự xóa dữ liệu cũ hơn 30 ngày (chạy nền mỗi giờ) | `Services/ReadingCleanupService.cs` |
| Ghi lại từng lần bơm (tự động hay bấm từ app, độ ẩm trước/sau, bao nhiêu giây) | bảng `PumpRuns` |
| Nút **Water now** trên app bơm thật: app gửi lệnh, ESP32 nhận lệnh ở lần gửi kế tiếp (≤ 5 giây), app chờ tới khi bơm xong rồi mới báo và ghi vào chuỗi tưới | `DevicesController.Water` + `hooks/useSensorData.ts` |
| Bật/tắt **Auto-watering** trên app → ESP32 làm theo trong ≤ 5 giây | `PATCH /api/sensors/devices/{id}` |
| Biểu đồ 24h thật (trung bình theo giờ) + vạch đánh dấu lúc bơm chạy | `Services/HistoryBuilder.cs` |
| **Thông báo bơm** qua RabbitMQ (Thân đã chuẩn bị sẵn loại `pump`) | `Services/ReadingIngestor.cs` |
| Thông báo **"Cây đang khát nước"** khi đất khô mà đang tắt tự tưới (tối đa 1 lần/6 giờ) | loại `soil_dry` |
| Thông báo **"Kiểm tra bình nước"**: bơm chạy nhưng độ ẩm không tăng → có thể hết nước | loại `water_tank` |
| Thiết bị online/offline (không gửi dữ liệu quá 30 giây là offline) | `ReadingPolicy.IsOnline` |
| Màn **ghép thiết bị** trong app: nhập Device ID + Device key, đổi tên cây, bỏ ghép | `app/pair-device.tsx` |
| **Simulator** giả lập ESP32 để test và demo không cần phần cứng | `tools/sensor-simulator` |
| 46 test: unit test cho các hàm logic + 11 test chạy cả luồng `ReadingIngestor` trên database giả (InMemory) | `services/sensor/Sensor.Tests` |

### 1.3. Sửa lỗi logic lần 2 (11/11)

| Lỗi | Hậu quả | Đã sửa |
|---|---|---|
| **Chưa có migration** cho `sensor_db` | Service tắt ngay khi chạy (`No migrations found`), container Docker sập, CI đỏ | Đã thêm `Data/Migrations/…_InitialCreate` (3 bảng). **Không cần chạy `dotnet ef migrations add` nữa** |
| Thiết bị mất điện / mất mạng **giữa lúc bơm** | Lần bơm mở mãi; app hiện "đang bơm" mãi; khi có mạng lại thì báo "đã tưới 600 giây" | Thiết bị offline quay lại: đóng lần bơm cũ, không gửi thông báo sai. App chỉ hiện `pumpOn` khi thiết bị online |
| Bấm **Water now** lúc bơm đang tự chạy | Server giao lệnh, ESP32 bỏ qua (đang bơm) → lệnh mất, app chờ tới timeout | Lệnh được **giữ lại**, giao ngay sau khi bơm dừng (nếu còn trong 60 giây) |
| Bấm **Water now** khi đất ≥ 85% | ESP32 từ chối (dừng cứng 85%) → app chờ tới timeout | Server trả `409` "đất đã đủ ẩm" ngay; ESP32/simulator cũng kiểm tra lại |
| Bấm **Water now** 2 lần | Lệnh thứ 2 ghi đè lệnh thứ nhất | Lần 2 trả về lệnh đang chờ, không tạo lệnh mới |
| Báo **"Kiểm tra bình nước"** ngay lúc bơm tắt | Cảm biến thật chậm (nước cần thời gian ngấm, firmware làm mượt số đo) → báo sai gần như mỗi lần tưới | Kiểm tra **45 giây sau** khi bơm tắt. Thông báo "Plant watered" luôn được gửi |
| Bình hết nước + tự tưới bật | Cứ ~70 giây lại 1 thông báo "Plant watered" (sai) và 1 "Kiểm tra bình nước" | Chỉ báo bình nước **1 lần** mỗi đợt; không báo "Plant watered" cho lần tự tưới khi bình đang hết; tự xóa cờ khi đất ẩm lên lại |
| Lần tự tưới ngay sau 1 lệnh tay | Có thể bị ghi nhầm là `manual` | 1 lệnh = 1 lần bơm tay |
| Biểu đồ 24h | Vạch bơm có thể nằm ngoài biểu đồ (lệch tới 59 phút) | Lịch sử bơm và biểu đồ dùng cùng khung giờ |
| IP thiết bị qua Gateway | Hiện IP của Gateway | Lấy IP thật từ `X-Forwarded-For` |
| Bỏ ghép thiết bị | Chủ mới thừa hưởng cài đặt tự tưới của chủ cũ | Đặt lại mặc định |

Cấu hình mới trong `appsettings.json` → `Sensor`: `HardStopPercent` (85, phải giống firmware) và `TankCheckDelaySeconds` (45, phải nhỏ hơn 60 giây nghỉ của firmware). Firmware lên bản `1.1.1`.

**Chưa làm (ngoài phạm vi lần này):**
- **Google Sheets:** các nút "Sync to Sheets", "Open sheet", "Export data" vẫn chưa có chức năng.
- Lần **tự tưới** của ESP32 chưa được tính vào chuỗi tưới (streak). Chỉ lần bấm Water now trên app được tính, vì Garden service cần token của người dùng.
- Mỗi tài khoản hiện chỉ hiện **1 thiết bị** trên màn Garden (backend đã hỗ trợ nhiều thiết bị).

---

## 2. Cài đặt lần đầu

Làm sau khi đã chạy được Identity theo `README.md` (Docker Desktop đã bật, đã có tài khoản `champ@test.com`).

### 2.1. Build và chạy test (làm đầu tiên)

Ở thư mục gốc dự án:

```powershell
dotnet build TreeO.slnx
dotnet test services/sensor/Sensor.Tests
```

Đúng khi: build `0 Error(s)`, test báo `Passed` và `Failed: 0`. Test **không cần** Docker hay mạng vì chỉ kiểm tra các hàm logic thuần.

### 2.2. Khai báo mật khẩu (user-secrets)

Thay `MAT_KHAU_POSTGRES` và `MAT_KHAU_RABBITMQ` bằng giá trị trong `infra/.env`:

```powershell
cd services/sensor/Sensor.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=sensor_db;Username=treeo;Password=MAT_KHAU_POSTGRES"
dotnet user-secrets set "RabbitMq:Password" "MAT_KHAU_RABBITMQ"
```

Database `sensor_db` đã được tạo sẵn bởi `infra/postgres/init.sql`.

### 2.3. Tạo bảng (migration)

Migration đã có sẵn trong `Data/Migrations`. Lúc `dotnet run`, service tự tạo 3 bảng `Devices`, `Readings`, `PumpRuns`. **Không** chạy `dotnet ef migrations add InitialCreate` nữa (sẽ bị trùng).

Kiểm tra migration khớp với Models (giống bước CI):

```powershell
cd services/sensor/Sensor.Api
dotnet ef migrations has-pending-model-changes
```

Đúng khi báo `No changes have been made to the model since the last migration`. Nếu máy nào đã tự tạo migration Sensor trước đây: xóa migration cũ của mình, giữ bản trong repo, rồi xóa bảng cũ bằng `docker exec -it treeo-postgres psql -U treeo -d postgres -c "DROP DATABASE sensor_db WITH (FORCE);" -c "CREATE DATABASE sensor_db;"` (mất dữ liệu cảm biến thử).

### 2.4. Chạy

```powershell
dotnet run
```

Thành công khi thấy `Now listening on: http://0.0.0.0:5182`. Service nghe ở `0.0.0.0` (không phải `localhost`) để ESP32 trong cùng mạng Wi-Fi gọi được.

> **Windows Firewall:** lần đầu chạy, Windows có thể hỏi cho phép mạng. Chọn **Private networks → Allow**. Không thì ESP32 không gọi tới được.

### 2.5. Cập nhật app

Chạy app trên điện thoại thật thì thêm vào `frontend/mobile/.env`:

```
EXPO_PUBLIC_SENSOR_URL=http://<IP-máy-tính>:5182
```

Chạy trên trình duyệt cùng máy thì không cần, app mặc định dùng `http://localhost:5182`.

---

## 3. Test

### 3.1. Test API bằng file `.http`

Mở `services/sensor/Sensor.Api/Sensor.Api.http`, bấm **Send Request** lần lượt từ 0 đến 20 (cần Identity chạy ở 5101):

| Request | Kết quả đúng |
|---|---|
| 0. Health | `200` |
| 1. Thiết bị gửi độ ẩm | `200`, có `pumpSeconds`, `autoWatering`, `dryThreshold`, `wetThreshold` |
| 2. Gửi với key sai | `401` |
| 3. Đăng nhập | `200` |
| 4. Ghép thiết bị | `200` |
| 5. Danh sách thiết bị | `200`, `online: true` (nếu gửi request 1 trong 30 giây gần đây) |
| 6. Tưới ngay | `202` (`409` nếu thiết bị offline, gửi lại request 1 rồi thử lại) |
| 7. Thiết bị gửi tiếp | `pumpSeconds: 5` (đã nhận lệnh) |
| 8, 9. Bơm bật rồi tắt | `200`. Sau request 9 có thông báo "Plant watered" trong app |
| 10. Lịch sử 24h | 24 điểm + danh sách lần bơm |
| 11. Hoạt động | lần bơm vừa rồi, `trigger: "manual"` |
| 12. Tắt tự tưới | `200`, `autoWatering: false` |
| 13. Tưới ngay | `202` |
| 14. Bấm lại lần 2 | `202`, "already waiting" (không tạo lệnh thứ 2) |
| 15. Thiết bị báo đang bơm | `pumpSeconds: 0` (lệnh được giữ lại) |
| 16. Thiết bị báo bơm đã tắt | `pumpSeconds: 5` (lệnh được giao bây giờ) |
| 17. Thiết bị báo đất 90% | `200` |
| 18. Tưới ngay khi đất 90% | `409`, "The soil is already wet" |
| 19. Không gửi token | `401` |
| 20. Bỏ ghép | `204` |

Request 13 đến 18 phải bấm liền nhau (trong 30 giây), nếu không thiết bị bị coi là offline và request 13 trả `409`. Khi đó bấm lại request 1 rồi làm tiếp.

### 3.2. Test cả hệ thống bằng simulator (không cần ESP32)

Bật: Docker, Identity (5101), Garden (5103), Notification (5105), Sensor (5182), rồi:

```powershell
cd tools/sensor-simulator
node simulator.mjs --start 33 --dry-rate 10
```

Trong app: **Settings → IoT device → Pair device**, nhập:
- Device ID: `TREEO_SIM_01`
- Device key: `sim-key-change-me-0123456789`

Những gì phải thấy:
1. Màn Garden: độ ẩm thật, "updated 3s ago", thẻ Settings hiện **Online**.
2. Khoảng 20 giây sau độ ẩm xuống dưới 30% → simulator in `Pump ON (auto)`, vài giây sau `Pump OFF`. Chuông có thông báo **Plant watered**.
3. Bấm **Water now** → nút đổi thành "Watering…", khoảng 5 đến 10 giây sau báo "Watering completed — soil is now X%", chuỗi tưới +1.
4. Tắt **Auto-watering** → simulator in `auto=off` ở lần gửi kế tiếp. Đợi đất khô → thông báo **Your plant is thirsty**.
5. Tắt simulator (`Ctrl + C`), đợi 30 giây → app hiện **Offline**. Bấm Water now → báo cảm biến mất kết nối.
6. Chạy `node simulator.mjs --empty-tank --start 25` → bơm chạy nhưng độ ẩm không tăng → có **Plant watered**, rồi khoảng **45 giây** sau có **Check the water tank** (chỉ 1 lần, các lần tự tưới sau không báo thêm). Tắt simulator, chạy lại không có `--empty-tank` → lần tưới tiếp theo có **Plant watered** bình thường.
7. **Mất điện giữa lúc bơm:** chạy `node simulator.mjs --start 25 --dry-rate 10`, đợi thấy `Pump ON`, bấm `Ctrl + C` ngay. Đợi hơn 30 giây (app hiện **Offline**, nút không còn "đang bơm"). Chạy lại simulator → **không** có thông báo "watered for 600s", mục Hoạt động hiện lần bơm đó đã đóng.
8. **Water now khi đang tự tưới:** khi simulator in `Pump ON (auto)`, bấm **Water now** ngay → app báo đang bơm (`409`). Bấm khi đất đang ≥ 85% → app báo "The soil is already wet".

Biểu đồ 24h cần dữ liệu từ **ít nhất 2 giờ khác nhau** mới vẽ. Trước đó app hiện dòng "The chart appears once the sensor has reported for a while".

### 3.3. Test với ESP32 thật

1. Copy `firmware/esp32-moisture/include/config.example.h` thành `include/config.h` (cùng thư mục).
2. Điền vào `config.h`:
   - `WIFI_SSID`, `WIFI_PASSWORD`: Wi-Fi **2.4 GHz** (ESP32 không bắt được 5 GHz).
   - `SERVER_URL`: `http://<IP-máy-tính>:5182`. Xem IP bằng `ipconfig` → **IPv4 Address**.
   - `DEVICE_ID`, `DEVICE_KEY`: tự đặt, key ít nhất 16 ký tự.
3. Upload bằng PlatformIO (lần đầu sẽ tự tải thư viện ArduinoJson). Mở Serial Monitor (115200).
4. Thấy `[HTTP] Sent 42% pump=off auto=on` là đã gửi được. Vào app ghép bằng đúng `DEVICE_ID` và `DEVICE_KEY`.

### 3.4. Nạp firmware qua Wi-Fi (OTA, không cần dây USB)

Từ firmware `1.2.0`, ESP32 nhận firmware mới qua Wi-Fi. **Lần đầu vẫn phải nạp bằng USB** (bản cũ trong repo không có OTA).

1. Thêm vào `include/config.h` (xem `config.example.h`): `OTA_HOSTNAME` và `OTA_PASSWORD` (tự đặt).
2. Nạp 1 lần bằng USB như mục 3.3. Serial Monitor hiện `[OTA] Ready: upload to treeo-esp32.local or 192.168.x.x`.
3. Mở `platformio.ini`, mục `[env:esp32dev_ota]`, sửa `upload_port` thành IP vừa thấy (app cũng hiện IP ở Settings → IoT device).
4. Từ giờ, rút dây USB. Trong terminal ở `firmware/esp32-moisture`:
   ```powershell
   $env:TREEO_OTA_PASSWORD = "giong-OTA_PASSWORD-trong-config.h"
   pio run -e esp32dev_ota -t upload
   ```
   Hoặc chọn `env:esp32dev_ota` ở thanh dưới của PlatformIO rồi bấm Upload.

Máy tính và ESP32 phải cùng Wi-Fi. Windows Firewall hỏi thì chọn **Allow** (OTA dùng cổng 3232). Lúc nạp, bơm luôn được tắt trước.

> **Relay kích mức thấp (active low):** nếu bơm chạy ngay khi cắm điện và tắt khi "ON", đổi `RELAY_ON = LOW` và `RELAY_OFF = HIGH` đầu file `main.cpp`.

> **Mật khẩu Wi-Fi cũ đã lên GitHub** trong lịch sử commit. Nên đổi mật khẩu Wi-Fi đó.

---

## 4. Hệ thống chạy như thế nào

```
ESP32 / simulator                    Sensor.Api (5182)                         App
-----------------                    -----------------                         ---
mỗi 1 giây: đọc độ ẩm
tự bật/tắt bơm (chạy được cả khi mất mạng)
mỗi 5 giây ──POST /readings──────▶  kiểm tra X-Device-Key
            (moisture, pumpOn)       cập nhật giá trị "hiện tại" của thiết bị
                                     lưu mẫu nếu cần (ShouldStore)
                                     bơm vừa tắt? ──▶ RabbitMQ ──▶ Notification ──▶ chuông
          ◀── pumpSeconds, autoWatering, ngưỡng ──
                                                    ◀──GET /devices (mỗi 5 giây)── màn Garden
                                                    ◀──POST /devices/{id}/water─── bấm Water now
lần gửi kế tiếp nhận pumpSeconds = 5 → bơm 5 giây
                                                    app thấy pumpOn: true → false → báo xong,
                                                    gọi Garden ghi chuỗi tưới
```

**Vì sao server không "đẩy" lệnh xuống ESP32?** ESP32 nằm sau router Wi-Fi nhà, server không gọi vào được. Nên ESP32 hỏi định kỳ, và lệnh đi kèm trong câu trả lời (gọi là *polling*). Độ trễ tối đa 5 giây, chấp nhận được với việc tưới cây. Muốn tức thì thì dùng MQTT (RabbitMQ đã bật plugin MQTT ở cổng 1883), nhưng phức tạp hơn nhiều.

**Vì sao logic bơm nằm ở ESP32 chứ không ở server?** Mất Wi-Fi hoặc server tắt thì cây vẫn được tưới. Server chỉ gửi *cài đặt* (ngưỡng, bật/tắt tự tưới) và *lệnh* (tưới ngay).

**Vì sao thiết bị dùng device key chứ không dùng JWT?** ESP32 không có tài khoản người dùng và không đăng nhập được. Mỗi thiết bị có một key riêng, giống mật khẩu. Server chỉ lưu bản băm SHA-256 của key, so sánh bằng `FixedTimeEquals` để không lộ thông tin qua thời gian phản hồi.

**Ghép thiết bị:** thiết bị mới gửi dữ liệu lần đầu thì tự được đăng ký (chưa thuộc về ai). Người dùng nhập đúng ID + key trong app thì thiết bị thuộc về họ. Thiết bị đã thuộc về người khác thì báo `409`. Muốn bắt buộc phải ghép trong app trước rồi mới nhận dữ liệu: đặt `Sensor:AllowAutoRegister` = `false`.

---

## 5. Từng file làm gì

### Backend `services/sensor/Sensor.Api`

| File | Làm gì |
|---|---|
| `Program.cs` | Đăng ký database, JWT (giống Garden), CORS, RabbitMQ, job dọn dữ liệu. Tự chạy migration khi khởi động. `/health` |
| `appsettings.json` → mục `Sensor` | Mọi con số: ngưỡng 30/60%, lưu mỗi 60 giây, offline sau 30 giây, giữ dữ liệu 30 ngày... Đổi ở đây, không cần sửa code |
| `Models/Device.cs` | 1 thiết bị: chủ sở hữu, giá trị hiện tại (độ ẩm, bơm, IP, Wi-Fi), cài đặt (tự tưới), lệnh đang chờ |
| `Models/SensorReading.cs` | 1 mẫu độ ẩm đã lưu |
| `Models/PumpRun.cs` | 1 lần bơm: bắt đầu, kết thúc, auto/manual, độ ẩm trước/sau |
| `Data/SensorDbContext.cs` | 3 bảng + index `(DeviceId, RecordedAtUtc)` để truy vấn lịch sử nhanh |
| `Controllers/SensorsController.cs` | API cho **thiết bị**: `POST /api/sensors/readings` |
| `Controllers/DevicesController.cs` | API cho **app** (cần đăng nhập): danh sách, ghép, đổi tên, bật/tắt tự tưới, bỏ ghép, lịch sử, hoạt động, tưới ngay |
| `Services/ReadingIngestor.cs` | Xử lý 1 lần gửi của thiết bị: kiểm tra key, lưu, mở/đóng lần bơm, giao lệnh, gửi thông báo |
| `Services/ReadingPolicy.cs` | **Mọi quyết định** dưới dạng hàm thuần (không database, không đồng hồ): có lưu không, online không, lệnh còn hạn không, auto hay manual, có báo khô không, có phải hết nước không |
| `Services/HistoryBuilder.cs` | Gom mẫu thành 24 điểm theo giờ cho biểu đồ |
| `Services/DeviceKeys.cs` | Băm và so sánh device key |
| `Services/ReadingCleanupService.cs` | Chạy nền mỗi giờ, xóa dữ liệu cũ bằng 1 câu `DELETE` (`ExecuteDeleteAsync`) |
| `Messaging/NotificationPublisher.cs` | Gửi tin RabbitMQ (cùng mẫu với Garden). RabbitMQ tắt thì chỉ ghi log, không làm hỏng việc nhận dữ liệu |
| `Data/Migrations/` | Migration tạo 3 bảng (đã có sẵn, không tạo lại) |
| `Sensor.Api.http` | 21 request để test |

### API

| API | Ai gọi | Làm gì |
|---|---|---|
| `POST /api/sensors/readings` | ESP32 (header `X-Device-Key`) | Gửi độ ẩm, nhận lệnh + cài đặt |
| `GET /api/sensors/devices` | App | Thiết bị của tôi + giá trị hiện tại |
| `POST /api/sensors/devices` | App | Ghép thiết bị `{deviceId, deviceKey, name}` |
| `GET /api/sensors/devices/{id}` | App | 1 thiết bị |
| `PATCH /api/sensors/devices/{id}` | App | Đổi tên / bật tắt tự tưới |
| `DELETE /api/sensors/devices/{id}` | App | Bỏ ghép + xóa lịch sử |
| `GET /api/sensors/devices/{id}/history?hours=24` | App | Biểu đồ |
| `GET /api/sensors/devices/{id}/activity?limit=10` | App | Các lần bơm gần nhất |
| `POST /api/sensors/devices/{id}/water` | App | Tưới ngay `{seconds}` (1 đến 15 giây, mặc định 5) |

### Firmware `firmware/esp32-moisture`

| File | Làm gì |
|---|---|
| `include/config.example.h` | Mẫu cấu hình. Copy thành `config.h` |
| `src/main.cpp` | Đọc cảm biến, logic bơm có giới hạn an toàn, kết nối Wi-Fi lại, gửi dữ liệu và làm theo phản hồi |
| `platformio.ini` | Thêm thư viện `ArduinoJson` |

### App `frontend/mobile/src`

| File | Làm gì |
|---|---|
| `api/sensor.ts` | Các hàm gọi API Sensor |
| `hooks/useSensorData.ts` | `useDevices` (hỏi lại mỗi 5 giây khi đang mở màn hình), `useDeviceHistory`, `waterAndWait` |
| `app/pair-device.tsx` | Màn ghép / quản lý thiết bị |
| `app/(tabs)/index.tsx` | Màn Garden dùng dữ liệu thật |
| `app/(tabs)/settings.tsx` | Thẻ IoT dùng dữ liệu thật, bấm vào mở màn ghép |

### Simulator `tools/sensor-simulator`

`simulator.mjs`: giả lập ESP32 với cùng luật bơm. Xem `README.md` trong thư mục đó.

---

## 6. Câu hỏi giảng viên có thể hỏi

**Sao không lưu tất cả dữ liệu 5 giây một lần?** 17.000 dòng/ngày/thiết bị, phần lớn trùng nhau. `ShouldStore` chỉ lưu khi có thông tin mới (đổi ≥ 3%, bơm bật/tắt) hoặc mỗi phút 1 lần. Giá trị "hiện tại" vẫn cập nhật mỗi 5 giây trên bảng `Devices`, nên app vẫn thấy số mới nhất.

**Hysteresis là gì?** Hai ngưỡng khác nhau để bật và tắt (30% và 60%). Nếu dùng chung một ngưỡng thì độ ẩm dao động quanh 30% sẽ làm bơm bật tắt liên tục, hỏng relay và bơm.

**Lỡ cảm biến hỏng, báo 0% mãi thì sao?** Mỗi lần tự tưới tối đa 8 giây, nghỉ ít nhất 1 phút. Nếu bơm chạy mà độ ẩm không tăng thì server gửi thông báo "kiểm tra bình nước".

**Vì sao tách `ReadingPolicy` thành hàm thuần?** Để viết unit test không cần database hay RabbitMQ. Thời gian `now` được truyền vào, nên test được các tình huống như "61 giây sau" mà không phải chờ.

**Microservices ở đâu trong phần này?** Sensor có database riêng (`sensor_db`), không đọc database của service khác. Nó biết người dùng là ai nhờ kiểm tra chữ ký JWT do Identity cấp, không cần gọi Identity. Nó báo cho Notification qua RabbitMQ (bất đồng bộ), nên Notification tắt thì Sensor vẫn chạy.

**Sao gửi thông báo không `await`?** ESP32 chỉ chờ phản hồi 2 giây. Nếu RabbitMQ chậm thì thiết bị sẽ bị timeout. Nên thông báo được gửi chạy nền, thiết bị nhận câu trả lời ngay.

---

## 7. Sửa vào file của người khác

| File | Thay đổi | Lý do |
|---|---|---|
| `shared/TreeO.Contracts/Events/NotificationRequested.cs` | Thêm 2 hằng `SoilDry`, `WaterTank` (không đổi gì cũ) | Loại thông báo mới |
| `frontend/mobile/src/api/config.ts` | Thêm `SENSOR_URL` | Địa chỉ Sensor service |
| `frontend/mobile/src/i18n/LanguageContext.tsx` | Thêm cụm key mới vào **cả** `en` và `vi` (không đổi key cũ) | Chữ cho phần cảm biến |
| `frontend/mobile/src/i18n/notificationText.ts` | Dịch thêm `pump`, `soil_dry`, `water_tank` | Thông báo hiện EN/VI |
| `frontend/mobile/src/app/notifications.tsx` | Thêm icon cho `soil_dry`, `water_tank` | |
| `frontend/mobile/src/app/_layout.tsx` | Thêm `<Stack.Screen name="pair-device" />` | Đăng ký màn mới |
| `frontend/mobile/src/components/MoistureChart.tsx` | Thêm props không bắt buộc (nhiều vạch bơm, ngưỡng, nhãn dịch được). Cách gọi cũ vẫn chạy | Biểu đồ dữ liệu thật |
| `frontend/mobile/src/app/(tabs)/index.tsx` | Thay dữ liệu mẫu bằng dữ liệu thật; **giữ nguyên** bố cục, thời tiết, chuỗi tưới, chuông của Thân | |
| `frontend/mobile/src/app/(tabs)/settings.tsx` | Thẻ IoT dùng dữ liệu thật | |
| `.gitignore` | Chặn `firmware/esp32-moisture/include/config.h` và `.pio/` | Không lộ mật khẩu Wi-Fi |

**Không sửa:** các service của Tâm và Thân, `infra/`, `README.md`.
