# Phần của Tâm: Security, Settings, Community

Tài liệu này giải thích code đã làm cho 3 phần việc của Tâm, cách chạy, cách test, và từng file làm gì. Mục tiêu là đọc xong thì hiểu được code và tự trả lời được khi giảng viên hỏi.

---

## 1. Đã làm những gì

| Phần | Tính năng | Ở đâu |
|---|---|---|
| **Security** | Đăng ký, đăng nhập, đăng xuất, phân quyền User/Expert/Admin (đã có từ trước) | `services/identity` |
| | **Mới:** đổi mật khẩu, đổi xong thì các máy khác bị đăng xuất | `services/identity` + app |
| | **Mới:** app tự gia hạn token khi hết hạn, người dùng không bị văng ra | `frontend/mobile/src/auth/AuthContext.tsx` |
| | **Mới:** danh sách trang web được gọi API (CORS) để trong file cấu hình | `appsettings.json` của từng service |
| **Settings** | Đổi ngôn ngữ EN/VI, **lưu lại** khi mở app lần sau | `frontend/mobile/src/i18n` |
| | Màn **đổi mật khẩu** | `frontend/mobile/src/app/change-password.tsx` |
| | Đăng xuất, hiện tên/email/vai trò thật | `frontend/mobile/src/app/(tabs)/settings.tsx` |
| **Community** | **Forum service mới**: lưu bài, lượt tim, theo dõi vào database `forum_db` | `services/forum` |
| | Xem bài: Tất cả / Chuyên gia / Đang theo dõi | app + Forum |
| | Huy hiệu **Verified Expert** cho bài của chuyên gia | app + Forum |
| | Tim / bỏ tim, theo dõi / bỏ theo dõi | app + Forum |
| | Đăng bài, xóa bài của mình (Admin xóa được mọi bài) | app + Forum |

**Chưa làm (ngoài phạm vi):** bình luận và đăng ảnh trong bài viết. Ô ảnh mẫu và số bình luận trong mockup đã được bỏ khỏi app vì chưa có backend.

---

## 2. Cài đặt lần đầu

Làm sau khi đã chạy được Identity theo `README.md` ở thư mục gốc.

### 2.1. Cập nhật Identity

Chép đè các file mới vào `services/identity/Identity.Api/`. Không cần cài thêm package hay tạo migration, vì không đổi bảng nào.

### 2.2. Cài Forum service

1. Chép các file mới vào `services/forum/Forum.Api/`
2. Xóa 2 file mẫu: `Controllers/WeatherForecastController.cs` và `WeatherForecast.cs`
3. Cài package:
   ```powershell
   cd services/forum/Forum.Api
   dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
   dotnet add package Microsoft.EntityFrameworkCore.Design
   dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
   ```
4. Khai báo mật khẩu database (giống Identity nhưng database là `forum_db`):
   ```powershell
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=forum_db;Username=treeo;Password=MAT_KHAU_POSTGRES"
   ```
5. Tạo bảng:
   ```powershell
   dotnet ef migrations add InitialCreate -o Data/Migrations
   ```
6. Chạy:
   ```powershell
   dotnet run
   ```
   Thành công khi thấy `Da tao 3 bai dang mau cho Forum` và `Now listening on: http://localhost:5107`.

### 2.3. Cập nhật app

Chép đè thư mục `frontend/mobile/src` mới, rồi `npx expo start -c`.

Chạy trên điện thoại thì file `frontend/mobile/.env` cần thêm dòng cho Forum:
```
EXPO_PUBLIC_API_URL=http://<IP-máy-tính>:5101
EXPO_PUBLIC_FORUM_URL=http://<IP-máy-tính>:5107
```
và chạy Forum bằng `dotnet run --urls http://0.0.0.0:5107`.

### Mỗi lần làm việc cần chạy

1. Docker Desktop
2. Identity: `dotnet run` trong `services/identity/Identity.Api` (cổng 5101)
3. Forum: `dotnet run` trong `services/forum/Forum.Api` (cổng 5107)
4. App: `npx expo start` trong `frontend/mobile`

---

## 3. Test

### Bằng file `.http` (REST Client)

**Identity**: mở `Identity.Api.http`, chạy request 1–9 như cũ.

**Forum**: mở `services/forum/Forum.Api/Forum.Api.http`, chạy lần lượt 1–12:

| Request | Kết quả đúng | Chứng minh điều gì |
|---|---|---|
| 1. Đăng nhập | `200` | Lấy token để dùng cho các request sau |
| 2. Tất cả bài | `200`, 3 bài mẫu | Forum đọc được token của Identity |
| 3. Chỉ chuyên gia | `200`, 2 bài | Bộ lọc Expert |
| 4. Đăng bài | `201` | Tạo bài, tên tác giả lấy từ token |
| 5. Tim | `likeCount: 1` | |
| 6. Bỏ tim | `likeCount: 0` | |
| 7. Theo dõi Linh Tran | `204` | |
| 8. Tab Following | Chỉ bài của Linh Tran | Bộ lọc Following |
| 9. Tự theo dõi mình | `400` | Chặn trường hợp vô lý |
| 10. Xóa bài của mình | `204` | |
| 11. Xóa bài người khác | `403` | **Phân quyền**: chỉ chủ bài hoặc Admin |
| 12. Không gửi token | `401` | **Bảo mật**: không đăng nhập thì không vào được |

### Trên app

1. Đăng nhập → tab Community thấy 3 bài mẫu
2. Bấm Expert Guides → chỉ còn bài có huy hiệu Verified Expert
3. Tim một bài → số tăng, tim đỏ; bấm lại → bỏ tim
4. Follow Minh Pham → sang tab Following chỉ thấy bài của Minh
5. Create post → viết → Post → bài hiện đầu danh sách, có chữ "You" và nút "Delete post"
6. Settings → Change password: nhập sai mật khẩu cũ → báo lỗi; nhập đúng → "Password changed"
7. Settings → VI → tắt app mở lại → vẫn tiếng Việt
8. Đăng xuất → đăng nhập bằng mật khẩu mới

---

## 4. Code hoạt động thế nào

### 4.1. Forum biết người gọi là ai bằng cách nào?

Forum **không hỏi Identity** và **không đọc database của Identity**. Nó chỉ kiểm tra token:

```
App đăng nhập → Identity cấp token (có id, tên, role, ký bằng Jwt:Key)
App gọi Forum kèm token → Forum kiểm tra chữ ký bằng CÙNG Jwt:Key
  → chữ ký đúng: tin thông tin trong token (id, tên, role)
  → chữ ký sai / hết hạn: trả 401
```

Đây là điểm quan trọng của microservices: mỗi service độc lập, không gọi qua lại khi không cần. Vì vậy `Jwt:Issuer`, `Jwt:Audience`, `Jwt:Key` của Forum phải **giống hệt** Identity. Khác nhau một ký tự là Forum trả 401 cho mọi request.

File liên quan:
- `Forum.Api/Program.cs`: phần `AddJwtBearer` cấu hình kiểm tra token
- `Forum.Api/Services/CurrentUser.cs`: đọc id, tên, role từ token

### 4.2. Database của Forum

`Forum.Api/Models/Post.cs` có 3 bảng:

| Bảng | Cột chính | Ghi chú |
|---|---|---|
| `Posts` | Id, AuthorId, AuthorName, AuthorRole, Content, CreatedAt | Tên và role tác giả **chép** từ token lúc đăng bài |
| `PostLikes` | PostId + UserId (khóa chính 2 cột) | Khóa 2 cột nên 1 người không thể tim 1 bài 2 lần |
| `Follows` | FollowerId + FolloweeId (khóa chính 2 cột) | A theo dõi B |

Vì sao chép tên tác giả vào bài thay vì hỏi Identity? Vì mỗi service có database riêng (`identity_db`, `forum_db`), Forum không được đọc bảng `Users` của Identity. Đánh đổi: nếu người dùng đổi tên, bài cũ vẫn hiện tên cũ. Nên ghi điểm này vào report như một trade-off của database-per-service.

`Data/ForumDbContext.cs` cấu hình khóa, chỉ mục, và **xóa bài thì xóa luôn lượt tim** (`OnDelete(Cascade)`).

### 4.3. Các API của Forum (`Controllers/ForumController.cs`)

Cả controller có `[Authorize]` nên mọi API đều cần token.

| API | Làm gì |
|---|---|
| `GET /api/forum/posts?filter=all\|experts\|following` | Lấy 50 bài mới nhất. Mỗi bài trả kèm `likeCount`, `likedByMe`, `isMine`, `followingAuthor` tính sẵn cho người đang xem |
| `POST /api/forum/posts` | Đăng bài (tối đa 1000 ký tự) |
| `DELETE /api/forum/posts/{id}` | Xóa: chủ bài hoặc Admin, người khác bị `403` |
| `POST` / `DELETE /api/forum/posts/{id}/like` | Tim / bỏ tim. Tim 2 lần không lỗi, vẫn tính 1 |
| `POST` / `DELETE /api/forum/follows/{authorId}` | Theo dõi / bỏ theo dõi. Tự theo dõi mình → `400` |

`ForumSeeder.cs` tạo 3 bài mẫu khi database trống và `SeedSamplePosts` là `true` (chỉ bật trong Development).

### 4.4. Đổi mật khẩu (Identity)

`PUT /api/auth/password`, file `Services/AuthService.cs`, hàm `ChangePasswordAsync`:

1. Kiểm tra mật khẩu cũ bằng `BCrypt.Verify`. Sai → `400`
2. Mật khẩu mới trùng mật khẩu cũ → `400`
3. Băm mật khẩu mới bằng BCrypt, lưu lại
4. **Thu hồi tất cả refresh token** → mọi máy khác bị đăng xuất khi token của chúng hết hạn
5. Cấp token mới cho máy đang dùng để không bị văng ra

Bước 4 là lý do bảo mật: nếu ai đó đã lấy được mật khẩu cũ và đang đăng nhập ở máy khác, đổi mật khẩu sẽ đá họ ra.

### 4.5. App tự gia hạn token (`src/auth/AuthContext.tsx`)

Access token sống 60 phút, refresh token sống 7 ngày. Hàm `authFetch` bọc mọi lời gọi API cần đăng nhập:

```
Trước khi gọi: token còn < 30 giây → gọi /api/auth/refresh lấy token mới trước
Gọi API kèm token
  → Server trả 401 (token hết hạn) → refresh 1 lần → gọi lại
  → Refresh cũng thất bại (đã quá 7 ngày, hoặc vừa đổi mật khẩu ở máy khác) → đăng xuất
```

`refreshing` (biến `useRef`) đảm bảo nếu 3 request cùng hết hạn một lúc thì chỉ gọi refresh **1 lần**, không phải 3.

### 4.6. Lưu trữ trên máy (`src/auth/storage.ts`)

| Dữ liệu | Điện thoại | Web |
|---|---|---|
| Token đăng nhập | `expo-secure-store` (mã hóa bằng Keychain / Keystore) | `localStorage` |
| Ngôn ngữ | như trên | như trên |

### 4.7. Màn Community (`src/app/(tabs)/community.tsx`)

- `useFocusEffect`: mỗi lần quay lại tab (ví dụ vừa đăng bài xong) thì tải lại danh sách
- **Optimistic update**: bấm tim là đổi màu ngay, gọi API sau; API lỗi thì trả lại như cũ và hiện thông báo. App vì vậy có cảm giác nhanh dù mạng chậm
- Follow một người thì đổi nút trên **mọi bài** của người đó
- Kéo xuống để tải lại (`RefreshControl`)
- Có đủ trạng thái: đang tải, lỗi (kèm nút Thử lại), danh sách trống

### 4.8. Đa ngôn ngữ (`src/i18n/LanguageContext.tsx`)

Mọi chữ hiện trên màn hình nằm trong 2 bảng `en` và `vi`. Trong màn hình dùng `t.tenChu` thay vì viết chữ trực tiếp. `vi` có kiểu `typeof en` nên quên dịch chữ nào là TypeScript báo lỗi ngay.

Đã dịch: thanh tab, Đăng nhập, Đăng ký, Settings, Đổi mật khẩu, Community, Đăng bài. Màn Garden và AI thuộc phần việc người khác, khi làm thì thêm chữ vào 2 bảng này.

Thông báo lỗi do server trả về (ví dụ "Mật khẩu hiện tại chưa đúng.") hiện đang luôn là tiếng Việt.

---

## 5. Câu hỏi giảng viên có thể hỏi

**Vì sao Forum không gọi Identity để kiểm tra người dùng?**
Token JWT đã có chữ ký. Forum chỉ cần cùng khóa để kiểm tra chữ ký là tin được nội dung token. Nhờ vậy Identity có sập thì Forum vẫn chạy với những người đang đăng nhập.

**Vì sao mật khẩu không lưu dạng gốc?**
BCrypt băm một chiều, có salt ngẫu nhiên. Lộ database cũng không đọc được mật khẩu.

**Vì sao cần cả access token và refresh token?**
Access token sống ngắn (60 phút) nên bị lộ cũng ít nguy hiểm, và không thể thu hồi. Refresh token sống dài nhưng lưu trong database nên thu hồi được (đăng xuất, đổi mật khẩu).

**Huy hiệu Expert lấy từ đâu?**
Admin đổi role người dùng thành Expert (`PUT /api/admin/users/{id}/role` ở Identity). Lần đăng nhập sau, token của họ có role Expert, và bài họ đăng sẽ có huy hiệu.

**Chặn người khác xóa bài của mình thế nào?**
Server kiểm tra `post.AuthorId == id trong token` hoặc role Admin. App có ẩn nút xóa, nhưng kể cả ai gọi API trực tiếp vẫn bị `403`. Không bao giờ chỉ dựa vào việc ẩn nút trên giao diện.
