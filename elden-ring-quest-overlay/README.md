# Elden Ring · La bàn nhiệm vụ (DEMO)

Mở **EldenQuestOverlay.exe**. App tự tìm `ER0000.sl2`; chọn nhân vật trong cửa sổ cài đặt. Bấm **TẤT CẢ** để tìm kiếm, xem đầy đủ và chọn bước trên HUD. Bấm **BẢN ĐỒ** để mở ứng dụng bản đồ riêng nằm cạnh thư mục HUD.

## Hướng dẫn theo nguồn

App lấy trực tiếp các ô checklist từ [Redmaw game gốc](https://eldenring.redmaw.dev/sheets/walkthrough) và [Redmaw DLC](https://eldenring.redmaw.dev/sheets/dlc-walkthrough). Lần kiểm tra này có **4.517 bước trong 69 khu vực**: 3.391 game gốc, 1.126 DLC. Thứ tự, ID và nội dung từng ô được giữ theo nguồn; nội dung nguồn là tiếng Anh. Màn hình **TẤT CẢ** cho tìm kiếm, lọc khu vực, xem toàn văn và đặt bước làm nhiệm vụ. Bấm **Đồng bộ nguồn** để lấy bản mới.

Bản dịch tiếng Việt của cả 4.517 bước nằm trong `translation-vi.json` cạnh exe. Tên riêng trong game (boss, vật phẩm, địa danh, lựa chọn hội thoại) giữ tiếng Anh để khớp với game. Đồng bộ nguồn không ghi đè file này; bước mới từ nguồn sẽ hiện tiếng Anh cho tới khi được dịch. Ô tìm kiếm nhận cả tiếng Việt lẫn tiếng Anh.

Lần đồng bộ đầu cần Internet. App lưu `source-cache.json` cạnh exe để dùng ngoại tuyến. Nếu chưa có cache và không tải được nguồn, app tạm hiện tuyến demo 95 mốc. Bản tải xuống không kèm sẵn văn bản walkthrough của bên thứ ba.

**Vị trí bước gợi ý từ save chỉ là ước lượng.** Những bước trước đó vẫn nằm trong danh sách và được báo cần rà soát; app không tự đánh dấu đã xong. Hãy bấm **TẤT CẢ**, tìm đúng nơi bạn đang ở và chọn bước. Nút **HOÀN TẤT** xác nhận thủ công. Trình đọc save hiện chưa đủ chứng cứ để tự xác nhận toàn bộ checklist, inventory, hội thoại, achievement hoặc NG+.

## HUD trong game

Trong Elden Ring, chọn **System → Graphics → Screen Mode → Borderless Windowed** để HUD hiện trên game. Fullscreen độc quyền trên máy này che cửa sổ ngoài game.

HUD có nút **Trước / Hoàn tất / Tiếp / Bản đồ / Ẩn** và biểu tượng **☰** để mở danh sách đầy đủ. Sau khi ẩn, bấm ô nhỏ **NHIỆM VỤ · F8** hoặc nhấn F8 để mở lại. Biểu tượng khay hệ thống cũng có các thao tác này.

**Tay cầm (Xbox, hoặc tay PS qua DS4Windows/Steam):** dùng nút Home (logo Xbox/PS), Elden Ring không dùng nút này.

| Nút Home | Tác dụng |
|---|---|
| Bấm 1 lần | Bước tiếp |
| Bấm nhanh 2 lần | Bước trước |
| Giữ ~1 giây | Ẩn/hiện HUD |

Tắt **Settings → Gaming → Game Bar → "Open Game Bar using this button on a controller"** để nút Home không bật Game Bar.

| Phím | Tác dụng |
|---|---|
| `F10` hoặc `Ctrl+Shift+F10` | Bước trước chưa xác nhận |
| `F11` hoặc `Ctrl+Shift+F11` | Bước sau chưa xác nhận |
| `F8` hoặc `Ctrl+Shift+F8` | Ẩn/hiện HUD |
| `Ctrl+Shift+F9` | Xác nhận bước hiện tại rồi sang bước tiếp |
| `Ctrl+Shift+F12` | Mở cài đặt |

[Bản đồ riêng](../elden-ring-map/EldenRingMap.exe) có tìm kiếm và bộ lọc marker trong cửa sổ riêng, không cần mở web. App chỉ đọc save, không sửa `.sl2`; vị trí bước và dấu xác nhận lưu trong `progress.json`. Yêu cầu Windows 64 bit, .NET Desktop Runtime 9 và Node.js 20+.

Đây vẫn là demo của mục tiêu 100% gồm achievement, toàn bộ nội dung game gốc và DLC. Số bước khớp các ô checklist nguồn ở lần kiểm tra này; chưa có bộ đối chiếu tự động chứng minh mọi boss, elite, item, ending và achievement đã hoàn tất trên save. Xem [AGENT.md](AGENT.md) để tiếp tục phát triển.
