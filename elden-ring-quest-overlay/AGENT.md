# AGENT.md — Elden Ring quest overlay

## Mục tiêu

Xây app nhiệm vụ trong game dựa trên save và walkthrough, hướng tới ba chuẩn 100% người dùng chọn: achievement, boss/quest/vật phẩm/ending của game gốc, và Shadow of the Erdtree. Giữ từng bước có nguồn; không lặng lẽ đánh dấu hoặc bỏ qua việc chưa xác minh.

## Trạng thái hiện tại

- `EldenQuestOverlay.exe` là HUD Windows Forms luôn nổi, nhận chuột, có nút ẩn/hiện và phím tắt toàn cục. Chạy trên Borderless Windowed; fullscreen độc quyền có thể che HUD.
- `reader.cjs` đọc `.sl2` chỉ đọc bằng `@zebbedaja/er-save-parser` 0.1.13. Save cho thông tin slot và một số event flag; không có chứng cứ đầy đủ cho mọi bước nguồn.
- `source-guide.cjs` lấy hai trang Redmaw khi chạy và parse toàn bộ checkbox trong 69 khu vực. Lần kiểm tra: 3.391 game gốc + 1.126 DLC = 4.517 bước, 4.517 ID duy nhất. App lưu cache cá nhân `source-cache.json`. Khi không có cache và không truy cập được nguồn, 95 mốc trong `guide.js` và `supplement.js` là tuyến tạm.
- Màn hình **TẤT CẢ** tìm kiếm, lọc khu vực, xem toàn văn và nhảy đến bước. HUD hiện văn bản bước theo nguồn. Bước nguồn cần xác nhận thủ công. Vị trí khởi đầu gợi ý từ tuyến 95 mốc chỉ là ước lượng; những bước trước vẫn cần rà soát.
- `progress.json` lưu vị trí và xác nhận theo slot riêng với save. Bản đồ độc lập ở `../elden-ring-map/EldenRingMap.exe`, mở từ HUD và có dữ liệu bản đồ/marker cục bộ.

## File và build

- `source/Program.cs`, `source/QuestOverlay.csproj`: UI, hotkeys, state, đọc save, đồng bộ nguồn.
- `translation-vi.json`: `{sections: {sectionId: tên}, steps: {stepId: văn bản}}`, nạp trong `ApplySourceGuide`. Bước thiếu bản dịch hiện tiếng Anh; `QuestStep.SourceText` giữ bản gốc cho tìm kiếm và dò vị trí từ save.
- `source/source-guide.mjs`: parser walkthrough. Build: `npx esbuild source/source-guide.mjs --bundle --platform=node --format=cjs --target=node20 --outfile=source-guide.cjs` sau khi cài `esbuild` và `cheerio`.
- `source/reader.mjs`, `source/guide.js`, `source/supplement.js`: parser save và tuyến demo. Build `reader.cjs` bằng esbuild theo cùng cấu hình.
- `dotnet publish source/QuestOverlay.csproj -c Release -o . --no-self-contained` với .NET SDK 9.
- Giữ license parser MIT. Không đưa save cá nhân, `progress.json` hoặc `source-cache.json` vào bản phát hành công khai. Không ghi ngược `.sl2`.

## Việc còn cần làm để đạt 100% thật

1. Đối chiếu 4.517 checkbox nguồn với các bảng boss, elite, item độc nhất, quest, ending và achievement; bổ sung ID/vị trí/điều kiện cho mục còn thiếu. Số checkbox chưa chứng minh phủ đủ ba chuẩn người dùng chọn.
2. Tạo đồ thị điều kiện và điểm không thể quay lại. Hỗ trợ nhánh quest, NG+ và DLC mà không ngầm bỏ các bước đã qua khi chọn bước hiện tại.
3. Gắn bằng chứng riêng cho từng bước: event flag, inventory, achievement, manual hoặc unknown. Chỉ tự hoàn tất khi có chứng cứ tin cậy; save không có flag không có nghĩa là chưa hoàn tất.
4. Liên kết bước với marker bản đồ bằng ID hoặc tọa độ chuẩn thay cho tìm theo tên. Tạo hướng đi từ Site of Grace gần nhất khi không có vị trí người chơi tin cậy.
5. Kiểm thử nhiều save/slot/patch/NG+, hotkey trong game, nhiều DPI và đồng bộ nguồn khi cấu trúc trang thay đổi. Giữ ID và xác nhận khi nguồn thêm hoặc sắp xếp lại bước.

## Nguồn

- [Redmaw game gốc](https://eldenring.redmaw.dev/sheets/walkthrough)
- [Redmaw DLC](https://eldenring.redmaw.dev/sheets/dlc-walkthrough)
- [Save parser](https://github.com/zebbedaja/er-save-parser)
- [Bản đồ độc lập](../elden-ring-map/README.md)
