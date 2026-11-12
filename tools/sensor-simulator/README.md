# Sensor simulator

Giả lập ESP32: gửi độ ẩm lên Sensor service mỗi 5 giây, tự bật bơm khi đất khô, và nhận lệnh "Water now" từ app. Dùng để test và demo khi không mang theo phần cứng.

Cần Node 18+ (đã cài sẵn để chạy app Expo). Không cần `npm install`.

```powershell
cd tools/sensor-simulator
node simulator.mjs
```

| Tùy chọn | Ý nghĩa | Mặc định |
|---|---|---|
| `--url` | Địa chỉ Sensor service | `http://localhost:5182` |
| `--device` | Device ID | `TREEO_SIM_01` |
| `--key` | Device key (ít nhất 16 ký tự) | `sim-key-change-me-0123456789` |
| `--start` | Độ ẩm lúc bắt đầu (%) | `45` |
| `--dry-rate` | Độ ẩm mất đi mỗi phút (%) | `3` (để `10` khi demo cho nhanh) |
| `--interval` | Số giây giữa 2 lần gửi | `5` |
| `--empty-tank` | Bơm chạy nhưng không có nước ra (để test cảnh báo "kiểm tra bình nước") | tắt |

Ví dụ demo nhanh cảnh đất khô và tự tưới:

```powershell
node simulator.mjs --start 33 --dry-rate 10
```

Sau khi chạy, vào app → **Settings → IoT device → Pair device**, nhập đúng Device ID và key ở trên.
