# Map for Goblins — cài trên máy mới

Bộ file đã chạy được trên máy cũ từ 28/09/2026: Elden Ring bản repack LinkNeverDie **1.17.1**,
mod nạp bằng **ModEngine2 của repack** (không phải Mod Engine 3), có Việt hoá.

Tài liệu này viết cho cả người lẫn Claude/Codex ở máy mới. **Đọc hết mục "Bẫy" trước khi sửa gì.**

## Trong thư mục này

| File | Là gì |
|---|---|
| `Game\mod\regulation.bin` | potato2 + MfG v1.17 ghép trên nền regulation **gốc 1.17.1** (đã có RideParam của Torrent). SHA256 `BEE8CE1F…583E`, 2.111.888 B |
| `Game\mod\menu\02_120_worldmap.gfx` | menu bản đồ của MfG v1.17 |
| `Game\mod\script\talk\m00_00_00_00.talkesdbnd.dcx` | menu cấu hình MfG ở Site of Grace |
| `Game\mod\font\` | font Việt hoá có sẵn trong thư mục `mod` của repack |
| `Game\VietHoa\msg\engus\` | Việt hoá. `item_dlc02` + `menu_dlc02` đã **thêm** chữ của MfG (2.507 PlaceName, 186 EventTextForTalk), không đè tiếng Việt |
| `Game\modengine2\config.toml` | `mods = mod, VietHoa`, `external_dlls = []` |
| `regulation-chi-mfg-1171.bin` | chỉ MfG, **không** potato2 (giữ nguyên cỏ/tầm nhìn). SHA256 `84C18173…DBED` |
| `goc-v1.17\` | bản tải gốc MfG v1.17 (Nexus mods/3091) — chỉ dùng khi phải ghép lại |

potato2 = cỏ ×0,3, tầm nhìn co lại khi CPU quá tải, tắt bóng vật xa >80 m. Đo trên máy cũ
(i5-12450H) được +4 FPS ở cảnh nặng. Máy mới mạnh hơn thì dùng `regulation-chi-mfg-1171.bin`.

Không kèm: QuestPath (`external_dlls`) và `mod-2B` của máy cũ.

## Bước 0 — kiểm tra đúng bản game

Bộ file chỉ chép thẳng được khi máy mới chạy **đúng bản này**:

| File của game | Phải khớp |
|---|---|
| `Game\eldenring.exe` | File version `2.7.1.0`, SHA256 `1A3547101327F65D0C76DA2F9190AC0AA66871EA42BAE2AECC61E11A8B597891` |
| `Game\regulation.bin` (gốc, không phải trong `mod\`) | 2.045.728 B, SHA256 `766521F9508DE3A3532DF61C45A1C2D93340F1FF7ED8306AB20DF761712CA2AB` |

```powershell
$g = 'D:\duong\dan\toi\ELDEN.RING\Game'
(Get-Item "$g\eldenring.exe").VersionInfo.FileVersion
(Get-FileHash "$g\eldenring.exe").Hash
(Get-FileHash "$g\regulation.bin").Hash
```

Khác bản → xem mục "Khác bản game".

## Cài (cùng bản)

1. Đóng game.
2. Sao lưu của máy mới: `Game\mod\`, `Game\VietHoa\msg\engus\`, `Game\modengine2\config.toml`.
3. Chép đè thư mục `Game\` ở đây lên thư mục `Game\` của game.
4. (Tuỳ chọn) Không muốn potato2: chép `regulation-chi-mfg-1171.bin` đè lên `Game\mod\regulation.bin`.
5. Chạy thẳng `Game\eldenring.exe`. LND Launcher của repack hỏi "chặn truy cập website" —
   bấm No thì nó thoát luôn; chạy exe thẳng vẫn nạp đủ ModEngine2 + Việt hoá.
6. Vào game **bản đồ trống là bình thường**. Ngồi ở Site of Grace → mục mới của MfG → bật
   nhóm biểu tượng muốn hiện.
7. Màn tiêu đề ghi Calibrations **1.16.1** — bình thường (xem bẫy 4). Gọi Torrent ngoài trời
   phải ra ngựa.

Save cũ dùng được luôn: MfG đọc flag gốc của game nên thứ đã nhặt/đã giết tự ẩn khỏi bản đồ.

**Gỡ:** xoá `Game\mod\regulation.bin`, `Game\mod\menu\02_120_worldmap.gfx`,
`Game\mod\script\talk\m00_00_00_00.talkesdbnd.dcx`; trả 2 file `*_dlc02` của VietHoa từ bản sao lưu.

## Khác bản game

- **Steam chính chủ, không repack:** làm theo `goc-v1.17\Readme.txt` — cài Mod Engine 3, chạy
  `Map for Goblins.me3`. Game mới hơn 1.17 thì tải bản MfG mới hơn trên Nexus.
- **Repack bản khác:** đừng chép cả `regulation.bin` của MfG. Lấy `regulation.bin` gốc của game mới,
  thay **nguyên** hai bảng `BonfireWarpParam` + `WorldMapPointParam` bằng bảng của MfG (row
  256 B / 236 B), rồi đóng gói lại. Chỉ hai bảng đó là của MfG; còn lại để nguyên của game.
  Chép cả file MfG 1.17.0 lên game 1.17.1 thì **Torrent không xuất hiện** khi gọi (thiếu
  RideParam 80020–80050) — đó là lý do có mod Nexus 10736 "1.17 Fix".
- Muốn giữ potato2 trên bản khác: phải dựng lại từ đầu bằng WitchyBND, chép file `.bin` ở đây
  sang là sai bản.

## Bẫy (đều đã dẫm trên máy cũ)

1. **ModEngine2 của repack đọc `Game\modengine2\config.toml`** (log ghi "global config"),
   **KHÔNG** đọc `Game\config_eldenring.toml`. Sửa nhầm file thì không có gì thay đổi.
2. **Không chép `msg\engus` của MfG vào `Game\mod\`.** `mod` đứng trước `VietHoa` trong
   `config.toml` nên tiếng Anh đè mất Việt hoá phần DLC. Bản ghép sẵn ở `Game\VietHoa\msg\engus\`.
3. **Không dùng `MapForGoblins.dll`** (bản DLL có overlay F1/minimap): chặn bản đồ gốc, treo
   rồi crash. Để `external_dlls` không trỏ tới nó.
4. **WitchyBND 3.0.1.0 không đóng gói được regulation 1.17** (thoát -1, không báo lỗi). Phải ghi
   tạm version `11601000` vào `_witchy-bnd4.xml` rồi mới đóng gói; nội dung 194 bảng vẫn đúng.
5. **So hai regulation thì so nội dung từng row**, không so hash file `.param`: công cụ của tác
   giả MfG ghi lại file nên 130/194 bảng "khác hash" dù nội dung y hệt.
6. **ModEngine2 chỉ đọc mod lúc game khởi động.** Chép/đổi file khi game đang chạy là vô tác dụng.
   Kiểm tra thật bằng cách vào game, đừng tin file trên đĩa.
7. `config.toml` **không BOM, xuống dòng CRLF.** PowerShell 5.1 `Set-Content -Encoding UTF8`
   thêm BOM — ghi bằng `[IO.File]::WriteAllText(path, text, (New-Object Text.UTF8Encoding $false))`.
