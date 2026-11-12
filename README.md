# TreeO – Smart Plant Care Companion

Dự án nhóm AMD201. Hướng dẫn này giúp clone về và chạy được trên máy Windows.

Làm theo đúng thứ tự: **Phần 1 → Phần 2 → Phần 3**.

**Tài liệu khác:**
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) – kiến trúc hệ thống (tiếng Anh, dùng được cho báo cáo)
- [`docs/API.md`](docs/API.md) – danh sách mọi API
- [`docs/DEPLOY.md`](docs/DEPLOY.md) – chạy cả hệ thống bằng Docker, CI/CD, deploy lên server
- `HUONG-DAN-TAM.md`, `HUONG-DAN-THAN.md`, `HUONG-DAN-NGUYEN.md` – phần việc của từng người

> **Đã đổi tên dự án từ MamXanh sang TreeO.** Ai đã cài dự án từ trước thì làm mục [Chuyển từ MamXanh sang TreeO](#chuyển-từ-mamxanh-sang-treeo-mỗi-máy-làm-1-lần) ở cuối file **một lần**.

---

## Phần 1. Cài phần mềm bắt buộc (chỉ làm 1 lần)

| Phần mềm | Dùng để | Tải ở đâu | Kiểm tra (gõ trong terminal) |
|---|---|---|---|
| **Git** + **GitHub Desktop** | Tải code về, đẩy code lên | desktop.github.com | `git --version` |
| **.NET 10 SDK** | Build và chạy backend C# | dotnet.microsoft.com → chọn **SDK**, bản Windows x64 | `dotnet --version` → ra `10.x.x` |
| **Docker Desktop** | Chạy Postgres và RabbitMQ | docker.com/products/docker-desktop | `docker -v` |
| **VS Code** | Viết code | code.visualstudio.com | |

**Extension trong VS Code** (bấm `Ctrl + Shift + X` để tìm):
- **C# Dev Kit** (Microsoft)
- **Docker** hoặc **Container Tools** (Microsoft)
- **REST Client** (Huachao Mao) – để test API

**Nếu cài Docker báo lỗi "Virtual Machine Platform"**: mở PowerShell bằng **Run as administrator**, gõ `wsl --install`, khởi động lại máy, rồi cài lại Docker Desktop.

**Cài công cụ tạo bảng database** (chỉ cần khi sửa Models, nhưng cài luôn cho đủ):
```powershell
dotnet tool install --global dotnet-ef
```

---

## Phần 2. Tải code và tạo file `.env` (chỉ làm 1 lần)

**1. Clone repo**

Trong GitHub Desktop: **File → Clone repository** → chọn repo của nhóm → chọn thư mục lưu → **Clone**.

Mở thư mục vừa clone bằng VS Code (**File → Open Folder**).

**2. Tạo file `.env`**

Trong thư mục `infra/` có file `.env.example`. Copy nó thành file mới tên **`.env`** (có dấu chấm ở đầu, không có đuôi):

```powershell
cd infra
copy .env.example .env
cd ..
```

**3. Đặt mật khẩu trong `.env`**

Mở `infra/.env`, đổi 2 chỗ `doi_mat_khau_nay` thành mật khẩu bạn tự đặt (viết liền, không dấu, không dấu cách):

```
POSTGRES_USER=treeo
POSTGRES_PASSWORD=mat_khau_cua_ban
RABBITMQ_USER=treeo
RABBITMQ_PASSWORD=mat_khau_cua_ban
```

Mật khẩu này **chỉ dùng trên máy bạn**, không cần giống ai. File `.env` đã bị `.gitignore` chặn nên sẽ không lên GitHub.

> **Nhớ kỹ mật khẩu Postgres**: Postgres chỉ đọc mật khẩu ở lần chạy đầu tiên. Lỡ đổi `.env` sau đó thì xem mục Lỗi hay gặp bên dưới.

---

## Phần 3. Chạy dự án

### 3.1. Bật database và RabbitMQ

Mở **Docker Desktop**, chờ góc dưới trái hiện **Engine running**.

Trong VS Code mở terminal (`Ctrl + ~`), đang ở thư mục gốc dự án:

```powershell
cd infra
docker compose up -d
cd ..
```

Thành công khi thấy `Container treeo-postgres Started` và `Container treeo-rabbitmq Started`. Trong Docker Desktop, nhóm `infra` có chấm xanh.

Từ lần sau chỉ cần mở Docker Desktop là 2 container này tự chạy lại.

### 3.2. Khai báo mật khẩu database cho Identity service (chỉ làm 1 lần)

```powershell
cd services/identity/Identity.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=identity_db;Username=treeo;Password=MAT_KHAU_POSTGRES"
```

Thay `MAT_KHAU_POSTGRES` bằng đúng `POSTGRES_PASSWORD` trong `infra/.env`.

Mật khẩu này được lưu riêng trên máy, **không nằm trong code**.
**Không** sửa mật khẩu trong `appsettings.Development.json`, để nguyên chữ mẫu `DOI_THANH_POSTGRES_PASSWORD_TRONG_ENV`.

### 3.3. Chạy Identity service

Vẫn ở thư mục `services/identity/Identity.Api`:

```powershell
dotnet run
```

Thành công khi thấy:
```
Da tao tai khoan Admin admin@treeo.local
Now listening on: http://localhost:5101
```

- Lần đầu sẽ có dòng `fail: ... __EFMigrationsHistory` → **bình thường**, EF tự tạo bảng.
- Mở `http://localhost:5101` trên trình duyệt ra 404 → **bình thường**, đây là API, không có trang web.
- Để nguyên terminal này chạy. Muốn tắt thì bấm `Ctrl + C`.

### 3.4. Test API

1. Mở file `services/identity/Identity.Api/Identity.Api.http`
2. Bấm chữ **Send Request** phía trên từng request, lần lượt từ 1 đến 9
3. Xem kết quả ở khung bên phải

| Request | Kết quả đúng |
|---|---|
| 1. Đăng ký | `200` (bấm lần 2 ra `409` vì email đã có, bỏ qua) |
| 2. Đăng nhập User | `200` |
| 3. Xem `/me` | `200` |
| 4. User gọi API Admin | `403` – đúng, User không có quyền |
| 5. Đăng xuất | `204` |
| 6. Dùng lại token đã đăng xuất | `401` – đúng, đã đăng xuất |
| 7. Đăng nhập Admin | `200`, role là Admin |
| 8. Admin xem danh sách user | `200` |
| 9. Đăng xuất Admin | `204` |

---

## Mỗi ngày làm việc

1. Mở Docker Desktop
2. GitHub Desktop: bấm **Fetch origin** / **Pull origin** để lấy code mới nhất
3. Tạo nhánh mới cho việc mình làm: **Current branch → New branch**, đặt tên kiểu `feature/ten-viec`
4. Chạy service đang làm bằng `dotnet run`
5. Làm xong: **Commit** → **Push** → **Create Pull Request** cho nhóm xem rồi mới merge vào `main`

**Không commit thẳng vào `main`.** Trước khi commit, kiểm tra danh sách file **không có** `.env`, `bin/`, `obj/`, `.vs/`.

---

## Thông tin Identity service

| API | Làm gì | Ai gọi được |
|---|---|---|
| `POST /api/auth/register` | Đăng ký (luôn là role User) | Ai cũng được |
| `POST /api/auth/login` | Đăng nhập, trả về `accessToken` + `refreshToken` | Ai cũng được |
| `POST /api/auth/refresh` | Đổi token mới khi access token hết hạn | Ai cũng được |
| `POST /api/auth/logout` | Đăng xuất (gửi kèm `refreshToken`) | Ai cũng được |
| `GET /api/auth/me` | Thông tin người đang đăng nhập | Đã đăng nhập |
| `GET /api/admin/users` | Danh sách user | Chỉ Admin |
| `PUT /api/admin/users/{id}/role` | Đổi role User / Expert / Admin | Chỉ Admin |

- Gọi API cần đăng nhập thì gửi kèm header: `Authorization: Bearer <accessToken>`
- Access token sống 60 phút, refresh token sống 7 ngày
- Tài khoản Admin để thử (chỉ dùng local): `admin@treeo.local` / `Admin@12345`. Khi deploy thật phải đổi mật khẩu này và `Jwt:Key`.

---

## Các địa chỉ trên máy

| Địa chỉ | Là gì |
|---|---|
| `http://localhost:5100` | **API Gateway** – cổng chung, app chỉ cần gọi cổng này (`EXPO_PUBLIC_GATEWAY_URL`) |
| `http://localhost:5100/health/all` | Kiểm tra mọi service còn sống không |
| `http://localhost:5101` | Identity service (API) |
| `http://localhost:5103` | Garden (chuỗi tưới cây) |
| `http://localhost:5104` | Reminder (thời tiết) |
| `http://localhost:5105` | Notification (thông báo) |
| `http://localhost:5106` | AI Assistant |
| `http://localhost:5107` | Forum |
| `http://localhost:5182` | Sensor (cảm biến độ ẩm + máy bơm) |
| `http://localhost:15672` | Trang quản lý RabbitMQ, đăng nhập bằng user/mật khẩu RabbitMQ trong `.env` |
| `localhost:5432` | Postgres (không mở bằng trình duyệt) |

Chạy Gateway: `cd services/gateway/Gateway.Api` rồi `dotnet run` (không cần database). Muốn chạy **cả hệ thống bằng Docker** thay vì mở nhiều terminal: xem `docs/DEPLOY.md`.

---

## Lỗi hay gặp

| Lỗi | Nguyên nhân | Cách sửa |
|---|---|---|
| `password authentication failed for user "treeo"` | Mật khẩu user-secrets khác mật khẩu Postgres | Chạy lại lệnh `dotnet user-secrets set` ở mục 3.2 với đúng mật khẩu trong `.env` |
| Vẫn lỗi trên dù mật khẩu đã giống `.env` | Đổi `.env` sau khi Postgres đã tạo, Postgres vẫn giữ mật khẩu cũ | `docker exec -it treeo-postgres psql -U treeo -d postgres -c "ALTER USER treeo WITH PASSWORD 'MAT_KHAU';"` |
| `file is locked by "Identity.Api"` khi build | Service đang chạy ở đâu đó | `Ctrl + C` ở terminal đang chạy, hoặc `Get-Process Identity.Api \| Stop-Process` |
| Không kết nối được database | Docker chưa mở hoặc container chưa chạy | Mở Docker Desktop, kiểm tra `treeo-postgres` có chấm xanh |
| Không thấy chữ **Send Request** trong file `.http` | Chưa cài REST Client | Cài extension rồi đóng file mở lại |
| Có container lạ tên ngẫu nhiên (vd `cranky_xxx`) trong Docker | Lỡ bấm Run trên image | Xóa nó đi, chỉ giữ nhóm `infra` |

**Lưu ý quan trọng:**
- Tắt database: `docker compose down` (trong thư mục `infra`), dữ liệu vẫn giữ.
- **Không dùng `docker compose down -v`**, chữ `-v` sẽ xóa sạch dữ liệu database.

---

## Khi sửa Models (thêm cột, thêm bảng)

Tắt service trước (`Ctrl + C`), rồi trong thư mục của service đó:

```powershell
dotnet ef migrations add TenThayDoi -o Data/Migrations
```

Commit cả thư mục `Data/Migrations`. Người khác pull về chỉ cần `dotnet run`, bảng tự cập nhật.

---

## Chuyển từ MamXanh sang TreeO (mỗi máy làm 1 lần)

Tên solution, thư viện chung, user database/RabbitMQ, tên container, exchange RabbitMQ và JWT đều đã đổi sang `treeo`. Máy đã cài từ trước đang có Postgres và RabbitMQ với user `mamxanh`, nên cần làm lại một lần.

**1. Lấy code mới.** Pull từ GitHub, hoặc giải nén zip vào **một thư mục mới** (đừng chép đè lên thư mục cũ: thư mục `shared/MamXanh.Contracts` cũ còn sót sẽ gây lỗi build trùng). Nếu đã tự tạo migration cho Sensor thì chép thư mục `services/sensor/Sensor.Api/Data/Migrations` từ chỗ cũ sang.

**2. Làm lại Postgres + RabbitMQ.** Việc này **xóa dữ liệu thử trên máy** (tài khoản, bài đăng, thông báo):

```powershell
cd infra
docker compose --profile app down -v
```

Mở `infra/.env`, đổi `POSTGRES_USER=treeo` và `RABBITMQ_USER=treeo` (mật khẩu giữ nguyên hoặc đổi tùy ý), rồi:

```powershell
docker compose up -d
cd ..
```

**3. Đặt lại chuỗi kết nối database cho mọi service.** Thay `MAT_KHAU_POSTGRES` bằng `POSTGRES_PASSWORD` trong `.env`, chạy ở thư mục gốc:

```powershell
$pg = "MAT_KHAU_POSTGRES"
$dbs = @{ "identity/Identity.Api" = "identity_db"; "forum/Forum.Api" = "forum_db"; "garden/Garden.Api" = "garden_db"; "notification/Notification.Api" = "notification_db"; "sensor/Sensor.Api" = "sensor_db" }
foreach ($p in $dbs.Keys) {
  dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=$($dbs[$p]);Username=treeo;Password=$pg" --project "services/$p"
}
```

Nếu đổi mật khẩu RabbitMQ ở bước 2 thì đặt lại `RabbitMq:Password` cho Garden, Reminder, Notification, Sensor (lệnh trong `HUONG-DAN-THAN.md` mục 2.4).

**4. Build và chạy lại.** `dotnet build TreeO.slnx`, chạy các service như bình thường, rồi **đăng ký lại** tài khoản thử (`champ@test.com`). Tài khoản Admin mới là `admin@treeo.local` / `Admin@12345`. Trên app phải đăng nhập lại (token cũ không còn hợp lệ).

**Nhánh đang làm dở** (code viết trước khi đổi tên): sửa `using MamXanh.Contracts.Events;` thành `using TreeO.Contracts.Events;`.
