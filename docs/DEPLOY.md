# Chạy bằng Docker, CI/CD và Deploy

Tài liệu cho cả nhóm. Gồm 3 phần: **(1)** chạy cả hệ thống bằng Docker trên máy, **(2)** CI/CD trên GitHub, **(3)** đưa lên server thật.

> Cách làm hằng ngày **không đổi**: `docker compose up -d` vẫn chỉ bật Postgres + RabbitMQ, các service chạy bằng `dotnet run` như trong `README.md`.

---

## 1. Chạy cả hệ thống bằng Docker (dùng khi demo)

### 1.1. Chuẩn bị (1 lần)

1. Có file `infra/.env` (copy từ `.env.example`). Nếu đã có `.env` từ trước thì **mở `.env.example` và chép thêm các dòng mới** vào `.env` (JWT_KEY, GEMINI_API_KEY...).
2. Điền `GEMINI_API_KEY` nếu muốn AI trả lời (để trống thì AI báo "not set up yet", phần khác vẫn chạy).
3. Sensor đã có migration trong repo (`services/sensor/Sensor.Api/Data/Migrations`), không cần tạo.

### 1.2. Chạy

Tắt hết các terminal `dotnet run` (trùng cổng), rồi:

```powershell
cd infra
docker compose --profile app up -d --build
```

Lần đầu mất vài phút vì phải tải image .NET và build 8 service. Các lần sau nhanh hơn nhiều nhờ cache.

Kiểm tra: mở `http://localhost:5100/health/all` → thấy `"status": "ok"` và 7 service `ok`.

Trong app (`frontend/mobile/.env`) chỉ cần **một dòng**:
```
EXPO_PUBLIC_GATEWAY_URL=http://localhost:5100
```
(điện thoại thật thì thay `localhost` bằng IP máy tính)

### 1.3. Lệnh hay dùng

| Việc | Lệnh (trong `infra/`) |
|---|---|
| Xem service nào đang chạy | `docker compose --profile app ps` |
| Xem log 1 service | `docker compose logs -f sensor` |
| Build lại sau khi sửa code | `docker compose --profile app up -d --build` |
| Tắt tất cả (giữ dữ liệu) | `docker compose --profile app down` |
| Quay lại làm bằng `dotnet run` | `docker compose --profile app down` rồi `docker compose up -d` |

**Không bao giờ dùng `down -v`**: xóa sạch database.

### 1.4. Cổng

| Cổng | Service |
|---|---|
| **5100** | **API Gateway** (app chỉ cần gọi cổng này) |
| 5101 | Identity |
| 5103 | Garden |
| 5104 | Reminder |
| 5105 | Notification |
| 5106 | AI Assistant |
| 5107 | Forum |
| 5182 | Sensor |
| 15672 | Trang quản lý RabbitMQ |

---

## 2. CI/CD trên GitHub

File: `.github/workflows/ci-cd.yml`. Chạy tự động mỗi khi tạo Pull Request hoặc push vào `main`. Xem kết quả ở tab **Actions** trên GitHub.

| Job | Kiểm tra gì | Đỏ thì làm gì |
|---|---|---|
| Backend build + tests | Build cả solution, chạy mọi unit test | Đọc log, sửa lỗi biên dịch / test |
| Database migrations | Có ai sửa Model mà quên tạo migration không | Chạy `dotnet ef migrations add TenThayDoi -o Data/Migrations` trong service đó |
| App typecheck + web build | TypeScript không lỗi, app build được | Chạy `npx tsc --noEmit` trong `frontend/mobile` |
| ESP32 firmware | Firmware biên dịch được | Build bằng PlatformIO trên máy |
| Docker image (×8) | Mỗi service build được thành image | Thử `docker build -f services/<x>/<X>.Api/Dockerfile .` ở thư mục gốc |
| Deploy | Chỉ chạy khi đã cấu hình server (mục 3.4) | |

Khi push vào `main`, 8 image được đẩy lên **GitHub Container Registry**: `ghcr.io/<tên-github>/treeo/<service>:latest`. Xem ở trang repo → **Packages**.

> Job "Database migrations" đỏ nghĩa là ai đó sửa Models mà quên tạo migration: chạy `dotnet ef migrations add <TenThayDoi> -o Data/Migrations` trong service đó rồi commit.

**Quy tắc nhóm:** chỉ merge Pull Request khi mọi job **xanh**. Có thể bật bắt buộc ở Settings → Branches → Add rule cho `main` → **Require status checks to pass**.

---

## 3. Deploy lên server

### 3.1. Chọn server

Một máy chủ ảo (VPS) **Ubuntu 22.04/24.04**, **tối thiểu 2 GB RAM** (8 service .NET + Postgres + RabbitMQ). Phải nằm trong ngân sách nhóm (dưới 1.000.000 VNĐ). Một số lựa chọn để so sánh (tự kiểm tra giá và điều kiện hiện tại trước khi mua):

- Tín dụng miễn phí cho sinh viên: **GitHub Student Developer Pack** (có ưu đãi của một số nhà cung cấp cloud), **Azure for Students**.
- VPS giá rẻ của các nhà cung cấp trong nước hoặc quốc tế, trả theo tháng (chỉ cần thuê trong thời gian demo / chấm điểm).

### 3.2. Cài đặt server (1 lần)

SSH vào server, rồi:

```bash
# Cài Docker
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER      # thoát SSH rồi vào lại để có hiệu lực

# Lấy code (cần file compose + init.sql)
git clone https://github.com/<tên-github>/<repo>.git ~/treeo
cd ~/treeo/infra
cp .env.example .env
nano .env
```

Trong `.env` trên server **bắt buộc** đổi:

| Biến | Giá trị |
|---|---|
| `POSTGRES_PASSWORD`, `RABBITMQ_PASSWORD` | mật khẩu mạnh |
| `JWT_KEY` | chuỗi ngẫu nhiên dài: chạy `openssl rand -base64 48` rồi dán vào |
| `ADMIN_PASSWORD` | mật khẩu Admin mới |
| `SEED_SAMPLE_POSTS` | `false` |
| `IMAGE_PREFIX` | `ghcr.io/<tên-github-viết-thường>/treeo` (bỏ dấu `#` đầu dòng) |
| `GEMINI_API_KEY` | key AI |

Thiếu `JWT_KEY` hoặc `ADMIN_PASSWORD` thì Docker **từ chối chạy**. Đây là cố ý, vì giá trị mặc định đang công khai trong repo.

Nếu repo để **private**, image cũng private, server cần đăng nhập để tải:
```bash
# Tạo token ở GitHub → Settings → Developer settings → Personal access tokens (quyền read:packages)
echo <TOKEN> | docker login ghcr.io -u <tên-github> --password-stdin
```

### 3.3. Chạy

```bash
cd ~/treeo/infra
docker compose -f docker-compose.yml -f docker-compose.prod.yml --profile app pull
docker compose -f docker-compose.yml -f docker-compose.prod.yml --profile app up -d
curl http://localhost/health/all
```

`docker-compose.prod.yml` chỉ mở **cổng 80 của Gateway** ra Internet. Database, RabbitMQ và từng service đều đóng, chỉ nói chuyện với nhau trong mạng nội bộ của Docker. Trên tường lửa của nhà cung cấp, chỉ cần mở cổng **22** (SSH) và **80**.

Sau đó:
- App: `EXPO_PUBLIC_GATEWAY_URL=http://<IP-server>`
- ESP32 (`config.h`): `SERVER_URL "http://<IP-server>"` (Gateway chuyển `/api/sensors/...` sang Sensor)
- Thêm địa chỉ web của app vào `CORS_ORIGIN_1` nếu chạy app bản web trên một tên miền.

### 3.4. Tự động deploy khi push (CD, không bắt buộc)

1. Trên máy bạn: `ssh-keygen -t ed25519 -f deploy_key -N ""`
2. Copy nội dung `deploy_key.pub` vào `~/.ssh/authorized_keys` trên server.
3. GitHub repo → **Settings → Secrets and variables → Actions**:
   - Tab **Secrets**: `DEPLOY_SSH_KEY` = nội dung file `deploy_key` (khóa riêng)
   - Tab **Variables**: `DEPLOY_HOST` = IP server, `DEPLOY_USER` = tên user SSH, (tùy chọn) `DEPLOY_PATH` = thư mục repo trên server (mặc định `~/treeo`)

Từ đó mỗi lần merge vào `main`: test xanh → build image → đẩy lên ghcr → SSH vào server → `git pull` → `docker compose pull` → `up -d` → kiểm tra `/health/all`. Chưa đặt `DEPLOY_HOST` thì job này tự bỏ qua, không báo lỗi.

### 3.5. HTTPS (nâng cao)

Hiện tại server chạy **HTTP**. App trên điện thoại và ESP32 vẫn dùng được. Nếu cần HTTPS (ví dụ app bản web chạy trên tên miền `https://`), cần một tên miền và một reverse proxy cấp chứng chỉ tự động như Caddy đặt trước Gateway. Chưa làm vì phụ thuộc việc nhóm có tên miền hay không.

---

## 4. Lỗi hay gặp

| Lỗi | Nguyên nhân | Cách sửa |
|---|---|---|
| `port is already allocated` | Còn `dotnet run` hoặc container cũ đang giữ cổng | Tắt terminal `dotnet run`; `docker compose --profile app down` |
| Container `treeo-sensor` khởi động rồi tắt, log ghi `No migrations found` | Code cũ chưa có thư mục `Data/Migrations` của Sensor | Pull code mới nhất rồi build lại |
| `/health/all` báo 1 service `down` | Service đó lỗi khi khởi động | `docker compose logs <tên-service>` |
| `password authentication failed` trong log | Đổi `POSTGRES_PASSWORD` sau khi Postgres đã tạo dữ liệu | Xem mục Lỗi hay gặp trong `README.md` (lệnh `ALTER USER`) |
| `required variable JWT_KEY is missing` | Chạy prod mà chưa đặt `JWT_KEY` | Đặt trong `infra/.env` |
| App báo `429 Too many requests` | Đăng nhập sai quá 10 lần/phút | Đợi 1 phút. Đổi giới hạn ở `RateLimits` trong `appsettings.json` của Gateway |
| Server tải image báo `denied` | Repo private, server chưa `docker login ghcr.io` | Mục 3.2 |

---

## Phụ lục: file của các thành viên đã được sửa trong phần chung

| File | Thay đổi |
|---|---|
| `services/identity/Identity.Api/Program.cs`, `services/forum/Forum.Api/Program.cs` | Thêm `GET /health` (1 dòng) cho Gateway và Docker |
| `WeatherForecast.cs`, `Controllers/WeatherForecastController.cs` ở mọi service | Xóa (file mẫu, không ai dùng) |
| `UnitTest1.cs` trong Garden, Reminder, Notification, AiAssistant `.Tests` | Xóa (test rỗng; các project này đã có test thật). Giữ lại ở Identity, Forum, PlantCatalog vì chưa có test khác |
| `shared/TreeO.Contracts/Class1.cs` | Xóa (file mẫu) |
| `services/plant-catalog/PlantCatalog.Api/Program.cs` | Thu gọn thành khung trống có `/health` (service chưa có tính năng) |
| `frontend/mobile/src/api/config.ts` | Thêm `EXPO_PUBLIC_GATEWAY_URL`: 1 dòng thay cho 7 địa chỉ. Biến riêng từng service vẫn dùng được như cũ |
| `frontend/mobile/src/api/http.ts` | Thời gian chờ 90 giây của AI nhận theo đường dẫn `/api/ai/` (để vẫn đúng khi đi qua Gateway) |
| `infra/docker-compose.yml` | Thêm health check cho Postgres/RabbitMQ và các service ở profile `app`. Lệnh cũ `docker compose up -d` không đổi |
| `infra/.env.example` | Thêm biến cho chế độ Docker đầy đủ; bỏ 2 dòng lệnh thừa ở cuối file (`cd infra`, `docker compose up -d`) |
