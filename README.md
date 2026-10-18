# Elden Ring — chuyển sang máy mới

Chụp ngày 18/10/2026 từ máy MSI GF63.

## 1. Save game

`save/76561198686252493/ER0000.sl2` — lần lưu cuối **16/10/2026 10:13**, 28.967.888 byte,
SHA256 `0BE060F75C15D4C4AFDC17B9E4C47453B5ABF522DEF7FBDDE34A4BE79241620F`.

Trên máy mới:

1. Cài game, mở game một lần tới màn hình tiêu đề rồi thoát, để game tạo thư mục save.
2. Mở `%APPDATA%\EldenRing\` (dán vào thanh địa chỉ Explorer).
3. Chép `ER0000.sl2` vào thư mục số bên trong đó. Nếu có sẵn `ER0000.sl2` thì đổi tên nó
   thành `ER0000.sl2.cu` trước, để còn đường lùi.

Nếu thư mục số trên máy mới **khác** `76561198686252493` (khác tài khoản Steam / khác bản
crack), game có thể báo save hỏng vì Steam ID nằm trong file. Khi đó cần công cụ đổi Steam
ID cho save (vd. "Elden Ring Save Manager") — đừng ghi đè file gốc khi chưa có bản sao.

## 2. La bàn nhiệm vụ (overlay)

`elden-ring-quest-overlay/` — chép cả thư mục ra đâu cũng được, rồi chạy
`EldenQuestOverlay.exe`.

Cần cài: **.NET Desktop Runtime 9** và **Node.js 20+** (Windows 64 bit).

- `progress.json` giữ vị trí bước và các bước đã xác nhận. Đường dẫn save trong đó ghi
  `C:\Users\Admin\...` — nếu tên user Windows ở máy mới khác, mở cài đặt
  (`Ctrl+Shift+F12`) chọn lại save.
- `source-cache.json` là bản walkthrough đã tải, dùng được khi không có mạng.
- Nút **BẢN ĐỒ** mở `../elden-ring-map/EldenRingMap.exe` — thư mục bản đồ **không** nằm
  trong repo này (170 MB, ~10.000 file).
- Chơi ở **Borderless Windowed** thì HUD mới hiện đè lên game.

## 3. Map for Goblins

`map-for-goblins/` — bộ file đã ghép sẵn (MfG + potato2 + Việt hoá, game 1.17.1) và hướng dẫn cài.
Đọc [map-for-goblins/README.md](map-for-goblins/README.md), nhất là mục "Bẫy".
