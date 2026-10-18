using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace EldenQuestOverlay;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(true, "Local\\EldenQuestOverlayDemo", out var created);
        if (!created) { MessageBox.Show("La bàn nhiệm vụ đang chạy. Mở từ biểu tượng ở khay hệ thống hoặc nhấn Ctrl+Shift+F12."); return; }
        Application.Run(new OverlayContext(args.Contains("--preview"), args.Contains("--no-settings")));
    }
}

internal sealed class SaveData
{
    public string SavePath { get; set; } = "";
    public int SlotIndex { get; set; }
    public List<SlotInfo> Slots { get; set; } = [];
    public List<QuestStep> Steps { get; set; } = [];
}

internal sealed class SlotInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public override string ToString() => $"Slot {Index + 1} · {Name} · Lv {Level}";
}

internal sealed class QuestStep
{
    public int Index { get; set; }
    public string Id { get; set; } = "";
    public string Phase { get; set; } = "";
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Where { get; set; } = "";
    public List<string> Actions { get; set; } = [];
    public string SourceText { get; set; } = "";
    public string Warning { get; set; } = "";
    public string MapQuery { get; set; } = "";
    public string MapUrl { get; set; } = "";
    public string GuideUrl { get; set; } = "";
    public bool Manual { get; set; }
    public long? Flag { get; set; }
    public bool ConfirmedBySave { get; set; }
}

internal sealed class SourceGuide
{
    public string FetchedAt { get; set; } = "";
    public List<SourceSection> Sections { get; set; } = [];
    public List<SourceStep> Steps { get; set; } = [];
}

internal sealed class SourceSection
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Part { get; set; } = "";
    public int StartIndex { get; set; }
    public int Count { get; set; }
    public override string ToString() => $"{(Part == "dlc" ? "DLC" : "Gốc")} · {Name} ({Count})";
}

internal sealed class SourceStep
{
    public string Id { get; set; } = "";
    public string SectionId { get; set; } = "";
    public string Section { get; set; } = "";
    public string Part { get; set; } = "";
    public string Text { get; set; } = "";
    public bool Optional { get; set; }
    public bool Missable { get; set; }
    public bool Note { get; set; }
    public string MapQuery { get; set; } = "";
    public string SourceUrl { get; set; } = "";
}

internal sealed class GuideTranslation
{
    public Dictionary<string, string> Sections { get; set; } = [];
    public Dictionary<string, string> Steps { get; set; } = [];
}

internal sealed class Preferences
{
    public string SavePath { get; set; } = "";
    public int SlotIndex { get; set; }
    public bool ShowOnlyInGame { get; set; } = true;
    public bool UserHidden { get; set; }
    public Dictionary<int, int> CursorBySlot { get; set; } = [];
    public Dictionary<int, int> SourceCursorBySlot { get; set; } = [];
    public HashSet<string> ManualDone { get; set; } = [];
}

internal sealed class OverlayContext : ApplicationContext
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private readonly string _appDir = AppContext.BaseDirectory;
    private readonly string _preferencesPath;
    private readonly HudForm _hud;
    private readonly CompactForm _bubble;
    private readonly SettingsForm _settings;
    private readonly GuideForm _guide;
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly GamepadWatcher _gamepad;
    // Lossless Scaling phủ một cửa sổ topmost toàn màn hình lên game; giữa hai cửa sổ topmost,
    // cái được đưa lên sau thắng. Nên HUD tự đưa mình lên lại theo nhịp.
    private readonly System.Windows.Forms.Timer _keepOnTop = new() { Interval = 500 };
    private readonly bool _preview;
    private bool _reading;
    private bool _syncing;
    private DateTime _lastSaveWrite;
    private string _status = "Đang tìm file save…";
    private SaveData? _data;
    private List<QuestStep> _sourceSteps = [];
    private List<SourceSection> _sourceSections = [];
    private string _sourceRevision = "";
    private Preferences _preferences;

    public OverlayContext(bool preview, bool noSettings = false)
    {
        _preview = preview;
        _preferencesPath = Path.Combine(_appDir, "progress.json");
        _preferences = LoadPreferences();
        if (string.IsNullOrWhiteSpace(_preferences.SavePath)) _preferences.SavePath = FindLatestSave() ?? "";
        if (preview) _preferences.ShowOnlyInGame = false;
        _hud = new HudForm(this);
        _bubble = new CompactForm(this);
        _settings = new SettingsForm(this);
        _guide = new GuideForm(this);
        var menu = new ContextMenuStrip();
        menu.Items.Add("Cài đặt và chọn nhân vật", null, (_, _) => OpenSettings());
        menu.Items.Add("Ẩn / hiện HUD", null, (_, _) => ToggleVisibility());
        menu.Items.Add("Mở bản đồ trong ứng dụng", null, (_, _) => OpenMap());
        menu.Items.Add("Thoát", null, (_, _) => Exit());
        _tray = new NotifyIcon { Icon = SystemIcons.Information, Text = "Elden Ring · La bàn nhiệm vụ", ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => OpenSettings();
        _timer = new System.Windows.Forms.Timer { Interval = 3500 };
        _timer.Tick += (_, _) => { UpdateVisibility(); _ = RefreshSaveAsync(false); };
        _timer.Start();
        _gamepad = new GamepadWatcher(() => Move(1), () => Move(-1), ToggleVisibility);
        _keepOnTop.Tick += (_, _) =>
        {
            if (_hud.Visible) Native.KeepOnTop(_hud.Handle);
            if (_bubble.Visible) Native.KeepOnTop(_bubble.Handle);
        };
        _keepOnTop.Start();
        _hud.Show();
        if (!noSettings) _settings.Show();
        UpdateVisibility();
        _ = RefreshSaveAsync(true);
        LoadSourceCache();
        _ = SyncSourceAsync(false);
    }

    public string SavePath => _preferences.SavePath;
    public int SlotIndex => _preferences.SlotIndex;
    public bool ShowOnlyInGame => _preferences.ShowOnlyInGame;
    public string Status => _status;
    public IReadOnlyList<SlotInfo> Slots => _data?.Slots ?? [];
    public IReadOnlyList<SourceSection> SourceSections => _sourceSections;
    public string SourceRevision => _sourceRevision;
    public bool UsingSource => _sourceSteps.Count > 0;
    public IReadOnlyList<QuestStep> ActiveSteps => UsingSource ? _sourceSteps : _data?.Steps ?? [];
    private Dictionary<int, int> ActiveCursors => UsingSource ? _preferences.SourceCursorBySlot : _preferences.CursorBySlot;
    public QuestStep? CurrentStep => ActiveSteps.ElementAtOrDefault(Cursor);
    public int StepCount => ActiveSteps.Count;
    public int Cursor => ActiveCursors.GetValueOrDefault(_preferences.SlotIndex, UsingSource ? 0 : -1);
    public int DoneCount => ActiveSteps.Count(IsDone);
    public int BacklogCount => Cursor < 0 ? 0 : ActiveSteps.Take(Cursor).Count(step => !IsDone(step));
    public string CharacterLabel => _data?.Slots.FirstOrDefault(slot => slot.Index == SlotIndex)?.ToString() ?? "Chưa chọn nhân vật";

    public bool IsDone(QuestStep step) => step.ConfirmedBySave || _preferences.ManualDone.Contains($"{SlotIndex}:{step.Id}");
    public string Evidence(QuestStep step) => step.ConfirmedBySave ? "SAVE ĐÃ XÁC NHẬN" : _preferences.ManualDone.Contains($"{SlotIndex}:{step.Id}") ? "BẠN ĐÃ XÁC NHẬN" : step.Id.StartsWith("base:") || step.Id.StartsWith("dlc:") ? "BƯỚC THEO NGUỒN · XÁC NHẬN THỦ CÔNG" : step.Manual ? "CẦN TỰ XÁC NHẬN" : "CHỜ SAVE CẬP NHẬT";

    public IEnumerable<QuestStep> Upcoming(int count)
    {
        foreach (var step in ActiveSteps.Skip(Math.Max(0, Cursor + 1)).Where(step => !IsDone(step)).Take(count)) yield return step;
    }

    private Preferences LoadPreferences()
    {
        try { return JsonSerializer.Deserialize<Preferences>(File.ReadAllText(_preferencesPath), JsonOptions) ?? new(); }
        catch { return new(); }
    }

    private void SavePreferences()
    {
        try { File.WriteAllText(_preferencesPath, JsonSerializer.Serialize(_preferences, JsonOptions)); }
        catch (Exception ex) { _status = $"Không lưu được tiến độ: {ex.Message}"; }
        RefreshUI();
    }

    private static string? FindLatestSave()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EldenRing");
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateFiles(root, "ER0000.sl2", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path)).OrderByDescending(file => file.LastWriteTimeUtc).FirstOrDefault()?.FullName;
    }

    private void LoadSourceCache()
    {
        try { ApplySourceGuide(JsonSerializer.Deserialize<SourceGuide>(File.ReadAllText(Path.Combine(_appDir, "source-cache.json")), JsonOptions)); }
        catch { }
    }

    private void ApplySourceGuide(SourceGuide? guide)
    {
        if (guide?.Steps.Count is not > 0) return;
        // Bản dịch tiếng Việt nằm riêng để Đồng bộ nguồn không ghi đè; bước chưa dịch giữ nguyên tiếng Anh.
        var vi = LoadTranslation();
        foreach (var section in guide.Sections)
            if (vi.Sections.TryGetValue(section.Id, out var name)) section.Name = name;
        _sourceSections = guide.Sections;
        _sourceRevision = guide.FetchedAt;
        _sourceSteps = guide.Steps.Select((source, index) =>
        {
            var section = vi.Sections.GetValueOrDefault(source.SectionId, source.Section);
            var text = vi.Steps.GetValueOrDefault(source.Id, source.Text);
            return new QuestStep
            {
            Index = index,
            Id = source.Id,
            Phase = $"{(source.Part == "dlc" ? "DLC" : "GAME GỐC")} · {section}",
            Title = ShortTitle(text),
            Kind = source.Missable ? "DỄ BỎ LỠ" : source.Optional ? "TÙY CHỌN" : source.Note ? "LƯU Ý" : "100% WALKTHROUGH",
            Where = section,
            Actions = [text],
            SourceText = source.Text,
            Warning = source.Missable ? "Nguồn đánh dấu mục này có thể bỏ lỡ." : "",
            MapQuery = source.MapQuery,
            GuideUrl = source.SourceUrl,
            Manual = true,
            };
        }).ToList();
        EnsureSourceCursor();
        RefreshUI();
    }

    private GuideTranslation LoadTranslation()
    {
        try { return JsonSerializer.Deserialize<GuideTranslation>(File.ReadAllText(Path.Combine(_appDir, "translation-vi.json")), JsonOptions) ?? new(); }
        catch { return new(); }
    }

    private void EnsureSourceCursor()
    {
        if (!UsingSource || _data == null || _preferences.SourceCursorBySlot.ContainsKey(SlotIndex)) return;
        var demoIndex = _preferences.CursorBySlot.GetValueOrDefault(SlotIndex, 0);
        var demoStep = _data.Steps.ElementAtOrDefault(demoIndex);
        var query = demoStep?.MapQuery;
        var match = !string.IsNullOrWhiteSpace(query)
            ? _sourceSteps.FindIndex(step => step.SourceText.Contains(query, StringComparison.OrdinalIgnoreCase)) : -1;
        if (match < 0 && demoStep != null)
        {
            var area = demoStep.Phase.Split('·')[0].Trim();
            match = _sourceSteps.FindIndex(step => step.Phase.Contains(area, StringComparison.OrdinalIgnoreCase));
        }
        _preferences.SourceCursorBySlot[SlotIndex] = Math.Max(0, match);
        SavePreferences();
    }

    private static string ShortTitle(string text)
    {
        if (text.Length <= 42) return text;
        var cut = text.LastIndexOf(' ', 41);
        return text[..(cut > 24 ? cut : 41)] + "…";
    }

    public async Task SyncSourceAsync(bool force)
    {
        if (_syncing) return;
        _syncing = true;
        try
        {
            var psi = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, UseShellExecute = false, CreateNoWindow = true };
            psi.ArgumentList.Add(Path.Combine(_appDir, "source-guide.cjs"));
            using var process = Process.Start(psi) ?? throw new Exception("Không chạy được bộ đồng bộ nguồn.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var json = await stdout;
            if (process.ExitCode != 0) throw new Exception((await stderr).Trim());
            var guide = JsonSerializer.Deserialize<SourceGuide>(json, JsonOptions) ?? throw new Exception("Dữ liệu nguồn trống.");
            if (guide.Steps.Count < 4000) throw new Exception($"Nguồn chỉ trả {guide.Steps.Count} bước; chưa thay tuyến cũ.");
            File.WriteAllText(Path.Combine(_appDir, "source-cache.json"), json, Encoding.UTF8);
            ApplySourceGuide(guide);
            _status = $"Đã đồng bộ {guide.Steps.Count:N0} bước từ walkthrough gốc + DLC";
        }
        catch (Exception ex)
        {
            if (!UsingSource) _status = $"Chưa đồng bộ walkthrough đầy đủ: {ex.Message}";
        }
        finally { _syncing = false; RefreshUI(); }
    }

    public async Task RefreshSaveAsync(bool force)
    {
        if (_reading || string.IsNullOrWhiteSpace(SavePath)) { if (string.IsNullOrWhiteSpace(SavePath)) { _status = "Chọn file ER0000.sl2 trong Cài đặt."; RefreshUI(); } return; }
        if (!File.Exists(SavePath)) { _status = "Không tìm thấy file save. Chọn lại đường dẫn."; RefreshUI(); return; }
        var modified = File.GetLastWriteTimeUtc(SavePath);
        if (!force && modified == _lastSaveWrite) return;
        _reading = true;
        _status = "Đang đọc save chỉ đọc…";
        RefreshUI();
        try
        {
            var requestedPath = SavePath;
            var requestedSlot = SlotIndex;
            var readerPath = Path.Combine(_appDir, "reader.cjs");
            var psi = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, UseShellExecute = false, CreateNoWindow = true };
            psi.ArgumentList.Add(readerPath);
            psi.ArgumentList.Add(requestedPath);
            psi.ArgumentList.Add(requestedSlot.ToString());
            using var process = Process.Start(psi) ?? throw new Exception("Không mở được Node.js.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = await stdoutTask;
            var error = await stderrTask;
            if (process.ExitCode != 0) throw new Exception(error.Length > 250 ? error[^250..] : error);
            if (requestedPath != SavePath || requestedSlot != SlotIndex) return;
            _data = JsonSerializer.Deserialize<SaveData>(output, JsonOptions) ?? throw new Exception("Dữ liệu parser rỗng.");
            _lastSaveWrite = modified;
            if (!_preferences.CursorBySlot.ContainsKey(SlotIndex))
            {
                var farthest = _data.Steps.FindLastIndex(step => step.ConfirmedBySave);
                _preferences.CursorBySlot[SlotIndex] = FindPendingAfter(farthest, _data.Steps);
                SavePreferences();
            }
            else if (_data.Steps.ElementAtOrDefault(_preferences.CursorBySlot[SlotIndex]) is { } current && IsDone(current))
            {
                _preferences.CursorBySlot[SlotIndex] = FindPendingAfter(_preferences.CursorBySlot[SlotIndex], _data.Steps);
                SavePreferences();
            }
            EnsureSourceCursor();
            _status = $"Đã đồng bộ · {DateTime.Now:HH:mm:ss} · {_data.Steps.Count} mốc demo";
        }
        catch (Exception ex) { _status = $"Đọc save lỗi: {ex.Message}"; }
        finally { _reading = false; RefreshUI(); }
    }

    private int FindPendingAfter(int index, IReadOnlyList<QuestStep>? route = null)
    {
        var steps = route ?? ActiveSteps;
        if (steps.Count == 0) return -1;
        for (var i = Math.Max(0, index + 1); i < steps.Count; i++) if (!IsDone(steps[i])) return i;
        for (var i = 0; i <= Math.Min(index, steps.Count - 1); i++) if (!IsDone(steps[i])) return i;
        return -1;
    }

    public void SelectSlot(int slot)
    {
        if (slot == SlotIndex) return;
        _preferences.SlotIndex = slot;
        _lastSaveWrite = default;
        _data = null;
        SavePreferences();
        _ = RefreshSaveAsync(true);
    }

    public void SelectSave(string path)
    {
        _preferences.SavePath = path;
        _preferences.SlotIndex = 0;
        _preferences.CursorBySlot.Clear();
        _preferences.SourceCursorBySlot.Clear();
        _preferences.ManualDone.Clear();
        _lastSaveWrite = default;
        _data = null;
        SavePreferences();
        _ = RefreshSaveAsync(true);
    }

    public void SetOnlyInGame(bool value)
    {
        _preferences.ShowOnlyInGame = value;
        SavePreferences();
        UpdateVisibility();
    }

    public void MarkDone()
    {
        if (CurrentStep is not { } step) return;
        _preferences.ManualDone.Add($"{SlotIndex}:{step.Id}");
        ActiveCursors[SlotIndex] = FindPendingAfter(Cursor);
        SavePreferences();
    }

    public void Move(int direction)
    {
        if (ActiveSteps.Count == 0) return;
        var i = Cursor;
        for (var n = 0; n < ActiveSteps.Count; n++)
        {
            i = (i + direction + ActiveSteps.Count) % ActiveSteps.Count;
            if (!IsDone(ActiveSteps[i])) { ActiveCursors[SlotIndex] = i; SavePreferences(); return; }
        }
    }

    public void JumpTo(int index)
    {
        if (index < 0 || index >= ActiveSteps.Count) return;
        ActiveCursors[SlotIndex] = index;
        SavePreferences();
    }

    public void OpenGuideList() { _guide.Show(); _guide.BringToFront(); _guide.Activate(); _guide.RefreshFromContext(); }

    public void ToggleVisibility() { _preferences.UserHidden = !_preferences.UserHidden; SavePreferences(); UpdateVisibility(); }

    public void OpenSettings()
    {
        _settings.Show(); _settings.BringToFront(); _settings.Activate();
        _settings.RefreshFromContext();
    }

    public void OpenMap()
    {
        var mapExe = Path.GetFullPath(Path.Combine(_appDir, "..", "elden-ring-map", "EldenRingMap.exe"));
        if (!File.Exists(mapExe)) { _status = "Chưa tìm thấy EldenRingMap.exe cạnh thư mục HUD."; OpenSettings(); RefreshUI(); return; }
        try
        {
            var psi = new ProcessStartInfo(mapExe) { UseShellExecute = true };
            if (CurrentStep is { } step) psi.ArgumentList.Add($"--search={step.MapQuery}");
            Process.Start(psi);
        }
        catch (Exception ex) { _status = $"Không mở được bản đồ: {ex.Message}"; OpenSettings(); RefreshUI(); }
    }

    public void OpenGuide()
    {
        if (CurrentStep is not { } step) return;
        try { Process.Start(new ProcessStartInfo(step.GuideUrl) { UseShellExecute = true }); }
        catch (Exception ex) { _status = $"Không mở được hướng dẫn: {ex.Message}"; RefreshUI(); }
    }

    public void UpdateVisibility()
    {
        var inContext = !_preferences.ShowOnlyInGame || Native.IsEldenRingForeground();
        var showHud = inContext && !_preferences.UserHidden;
        var showBubble = inContext && _preferences.UserHidden;
        if (showHud && !_hud.Visible) _hud.Show();
        if (!showHud && _hud.Visible) _hud.Hide();
        if (showBubble && !_bubble.Visible) _bubble.Show();
        if (!showBubble && _bubble.Visible) _bubble.Hide();
    }

    private void RefreshUI() { if (!_hud.IsDisposed) _hud.Invalidate(); if (!_settings.IsDisposed) _settings.RefreshFromContext(); if (!_guide.IsDisposed && _guide.Visible) _guide.RefreshFromContext(); }

    public void Exit()
    {
        _timer.Stop(); _keepOnTop.Stop(); _gamepad.Dispose(); _tray.Visible = false; _tray.Dispose();
        _settings.Close(); _hud.Close(); _bubble.Close(); _guide.Close(); ExitThread();
    }
}

// Nút Home (Guide) của tay cầm XInput: Elden Ring không dùng nút này nên không đụng thao tác trong game.
// Bấm 1 lần = bước tiếp, bấm nhanh 2 lần = bước trước, giữ = ẩn/hiện HUD.
internal sealed class GamepadWatcher : IDisposable
{
    private const ushort GuideButton = 0x0400;
    private const int DoubleTapMs = 350, HoldMs = 700;
    private readonly Action _next, _previous, _toggle;
    private readonly System.Windows.Forms.Timer _poll = new() { Interval = 25 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private int _pad = -1;
    private long _nextScan, _pressedAt = -1, _lastTapAt = -1;
    private bool _wasDown, _holdFired;

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState { public uint Packet; public ushort Buttons; public byte LeftTrigger, RightTrigger; public short LX, LY, RX, RY; }
    // Ordinal 100 (XInputGetStateEx) là hàm duy nhất trả về trạng thái nút Guide.
    [DllImport("xinput1_4.dll", EntryPoint = "#100")] private static extern uint GetStateEx(int index, out XInputState state);

    public GamepadWatcher(Action next, Action previous, Action toggle)
    {
        _next = next; _previous = previous; _toggle = toggle;
        _poll.Tick += (_, _) => Poll();
        _poll.Start();
    }

    private bool GuideDown()
    {
        var now = _clock.ElapsedMilliseconds;
        try
        {
            if (_pad >= 0 && GetStateEx(_pad, out var state) == 0) return (state.Buttons & GuideButton) != 0;
            _pad = -1;
            // Dò cổng trống tốn thời gian, nên chỉ dò lại mỗi 2 giây.
            if (now < _nextScan) return false;
            _nextScan = now + 2000;
            for (var i = 0; i < 4; i++)
                if (GetStateEx(i, out var found) == 0) { _pad = i; return (found.Buttons & GuideButton) != 0; }
        }
        catch { _poll.Stop(); }
        return false;
    }

    private void Poll()
    {
        var now = _clock.ElapsedMilliseconds;
        var down = GuideDown();
        if (down && !_wasDown) { _pressedAt = now; _holdFired = false; }
        if (down && !_holdFired && now - _pressedAt >= HoldMs) { _holdFired = true; _lastTapAt = -1; _toggle(); }
        if (!down && _wasDown && !_holdFired)
        {
            if (_lastTapAt >= 0 && now - _lastTapAt <= DoubleTapMs) { _lastTapAt = -1; _previous(); }
            else _lastTapAt = now;
        }
        if (!down && _lastTapAt >= 0 && now - _lastTapAt > DoubleTapMs) { _lastTapAt = -1; _next(); }
        _wasDown = down;
    }

    public void Dispose() { _poll.Stop(); _poll.Dispose(); }
}

internal static class Native
{
    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_NOREPEAT = 0x4000;
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr handle, int id);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] public static extern bool ReleaseCapture();
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10, SWP_NOOWNERZORDER = 0x200;
    // Đưa cửa sổ lên đầu nhóm topmost mà không lấy focus của game.
    public static void KeepOnTop(IntPtr window) =>
        SetWindowPos(window, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    public static bool IsEldenRingForeground()
    {
        try
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out var id);
            return Process.GetProcessById((int)id).ProcessName.Equals("eldenring", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}

internal sealed class HudForm : Form
{
    private readonly OverlayContext _context;
    private readonly Font _eyebrow = new("Segoe UI", 8, FontStyle.Bold);
    private readonly Font _title = new("Segoe UI", 12, FontStyle.Bold);
    private readonly Font _icon = new("Segoe UI", 14, FontStyle.Bold);
    private readonly Font _body = new("Segoe UI", 9.5f);
    private readonly Font _small = new("Segoe UI", 8);
    private readonly Color _background = Color.FromArgb(22, 26, 33);
    private readonly Color _muted = Color.FromArgb(174, 185, 197);
    private readonly Color _gold = Color.FromArgb(224, 189, 111);
    public HudForm(OverlayContext context)
    {
        _context = context;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Width = 380; Height = 292;
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(area.Right - Width - 28, area.Top + 28);
        BackColor = _background;
        Opacity = .94;
        DoubleBuffered = true;
        Region = new Region(Rounded(new Rectangle(0, 0, Width, Height), 12));
        HandleCreated += (_, _) => RegisterKeys();
        HandleDestroyed += (_, _) => { for (var id = 1; id <= 8; id++) Native.UnregisterHotKey(Handle, id); };
    }

    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x00000080 | 0x00080000; return cp; }
    }

    protected override bool ShowWithoutActivation => true;

    private void RegisterKeys()
    {
        var mods = Native.MOD_CONTROL | Native.MOD_SHIFT | Native.MOD_NOREPEAT;
        Native.RegisterHotKey(Handle, 1, mods, (uint)Keys.F8);
        Native.RegisterHotKey(Handle, 2, mods, (uint)Keys.F9);
        Native.RegisterHotKey(Handle, 3, mods, (uint)Keys.F10);
        Native.RegisterHotKey(Handle, 4, mods, (uint)Keys.F11);
        Native.RegisterHotKey(Handle, 5, mods, (uint)Keys.F12);
        Native.RegisterHotKey(Handle, 6, Native.MOD_NOREPEAT, (uint)Keys.F10);
        Native.RegisterHotKey(Handle, 7, Native.MOD_NOREPEAT, (uint)Keys.F11);
        Native.RegisterHotKey(Handle, 8, Native.MOD_NOREPEAT, (uint)Keys.F8);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY)
        {
            switch (m.WParam.ToInt32())
            {
                case 1: _context.ToggleVisibility(); break;
                case 2: _context.MarkDone(); break;
                case 3: _context.Move(-1); break;
                case 4: _context.Move(1); break;
                case 5: _context.OpenSettings(); break;
                case 6: _context.Move(-1); break;
                case 7: _context.Move(1); break;
                case 8: _context.ToggleVisibility(); break;
            }
        }
        base.WndProc(ref m);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (CloseBox.Contains(e.Location) || HideButton.Contains(e.Location)) { _context.ToggleVisibility(); return; }
        if (ListBox.Contains(e.Location)) { _context.OpenGuideList(); return; }
        if (PrevButton.Contains(e.Location)) { _context.Move(-1); return; }
        if (DoneButton.Contains(e.Location)) { _context.MarkDone(); return; }
        if (NextButton.Contains(e.Location)) { _context.Move(1); return; }
        if (MapButton.Contains(e.Location)) { _context.OpenMap(); return; }
    }

    // Bố cục HUD thu gọn 380×292; vùng bấm và vùng vẽ dùng chung các hình chữ nhật này.
    private const int Pad = 16, ButtonTop = 250, ButtonHeight = 28;
    private static readonly Rectangle ListBox = new(318, 4, 28, 28), CloseBox = new(348, 2, 30, 30);
    private static readonly Rectangle PrevButton = new(Pad, ButtonTop, 44, ButtonHeight), DoneButton = new(64, ButtonTop, 100, ButtonHeight),
        NextButton = new(168, ButtonTop, 44, ButtonHeight), MapButton = new(216, ButtonTop, 74, ButtonHeight), HideButton = new(294, ButtonTop, 70, ButtonHeight);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(_background);
        using var accent = new SolidBrush(_gold);
        g.FillRectangle(accent, 0, 0, 4, Height);
        var w = Width - Pad * 2;
        var text = Color.FromArgb(232, 236, 239);
        Draw(g, "☰", _body, new Rectangle(ListBox.X + 6, ListBox.Y + 4, 22, 22), _muted);
        Draw(g, "×", _icon, new Rectangle(CloseBox.X + 7, CloseBox.Y + 1, 24, 28), _muted);
        var step = _context.CurrentStep;
        if (step == null)
        {
            Draw(g, "LA BÀN NHIỆM VỤ · ELDEN RING", _eyebrow, new Rectangle(Pad, 11, 296, 16), _gold);
            Draw(g, "Đang chờ dữ liệu nhiệm vụ", _title, new Rectangle(Pad, 38, w, 26), Color.White);
            Draw(g, _context.Status, _body, new Rectangle(Pad, 68, w, 150), _muted);
        }
        else
        {
            Draw(g, $"{step.Index + 1:N0}/{_context.StepCount:N0} · {step.Phase.ToUpperInvariant()}", _eyebrow, new Rectangle(Pad, 11, 296, 16), _gold);
            Draw(g, $"{step.Kind} · {_context.Evidence(step)}", _small, new Rectangle(Pad, 32, w, 16), Color.FromArgb(134, 211, 177));
            if (step.Id.StartsWith("base:") || step.Id.StartsWith("dlc:"))
            {
                // Bước theo nguồn: tiêu đề chỉ là đoạn đầu của nội dung, nên hiện thẳng nội dung để tiết kiệm chỗ.
                Draw(g, step.Actions.FirstOrDefault() ?? "", _body, new Rectangle(Pad, 52, w, 166), text);
            }
            else
            {
                Draw(g, step.Title, _title, new Rectangle(Pad, 50, w, 24), Color.White);
                var y = 78;
                for (var i = 0; i < Math.Min(3, step.Actions.Count); i++)
                {
                    Draw(g, $"{i + 1}", _eyebrow, new Rectangle(Pad, y + 2, 14, 16), _gold);
                    Draw(g, step.Actions[i], _small, new Rectangle(Pad + 16, y, w - 16, 44), text);
                    y += 46;
                }
            }
        }
        g.DrawLine(new Pen(Color.FromArgb(63, 70, 80)), Pad, 222, Width - Pad, 222);
        var next = _context.Upcoming(1).FirstOrDefault();
        var nextText = next == null ? "Không còn mốc tiếp theo" : $"TIẾP: {next.Title}";
        Draw(g, nextText, _small, new Rectangle(Pad, 227, w, 16), _gold);
        DrawButton(g, "◀", PrevButton, false);
        DrawButton(g, "✓ HOÀN TẤT", DoneButton, true);
        DrawButton(g, "▶", NextButton, false);
        DrawButton(g, "BẢN ĐỒ", MapButton, false);
        DrawButton(g, "ẨN (F8)", HideButton, false);
    }

    private void DrawButton(Graphics g, string label, Rectangle rect, bool primary)
    {
        using var fill = new SolidBrush(primary ? _gold : Color.FromArgb(49, 57, 68));
        using var path = Rounded(rect, 7);
        g.FillPath(fill, path);
        TextRenderer.DrawText(g, label, _eyebrow, rect, primary ? _background : Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    private static void Draw(Graphics g, string text, Font font, Rectangle bounds, Color color)
    {
        TextRenderer.DrawText(g, text, font, bounds, color, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Top);
    }

    private static GraphicsPath Rounded(Rectangle rect, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure(); return path;
    }
}

internal sealed class CompactForm : Form
{
    private readonly OverlayContext _context;
    private readonly Font _font = new("Segoe UI", 10, FontStyle.Bold);
    public CompactForm(OverlayContext context)
    {
        _context = context;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Width = 150; Height = 34;
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(area.Right - Width - 28, area.Top + 28);
        BackColor = Color.FromArgb(29, 34, 41);
        DoubleBuffered = true;
    }
    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x00000080; return cp; }
    }
    protected override bool ShowWithoutActivation => true;
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left) _context.ToggleVisibility();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(BackColor);
        using var gold = new SolidBrush(Color.FromArgb(224, 189, 111));
        e.Graphics.FillRectangle(gold, 0, 0, 5, Height);
        TextRenderer.DrawText(e.Graphics, "☷  NHIỆM VỤ · F8", _font,
            new Rectangle(11, 0, Width - 14, Height), Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

internal sealed class GuideForm : Form
{
    private readonly OverlayContext _context;
    private readonly TextBox _search = new() { Width = 410, BorderStyle = BorderStyle.FixedSingle };
    private readonly ComboBox _sections = new() { Width = 310, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
    private readonly ListView _list = new() { View = View.Details, FullRowSelect = true, HideSelection = false, BorderStyle = BorderStyle.None };
    private readonly TextBox _detail = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None };
    private readonly Label _count = new() { AutoSize = false, Width = 520, Height = 24 };
    private readonly Label _where = new() { AutoSize = false, Width = 520, Height = 62 };
    private int _loadedCount = -1;
    private string _loadedRevision = "";
    private bool _loading;

    public GuideForm(OverlayContext context)
    {
        _context = context;
        Text = "Elden Ring · Walkthrough đầy đủ";
        Size = new Size(1180, 790);
        MinimumSize = new Size(1000, 690);
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        BackColor = Color.FromArgb(17, 21, 28);
        ForeColor = Color.FromArgb(240, 242, 238);
        Font = new Font("Segoe UI", 10);
        FormClosing += (_, e) => { e.Cancel = true; Hide(); };
        AddLabel("WALKTHROUGH 100% · NGUỒN REDMAW", 22, 15, 720, 29, 13, Color.FromArgb(226, 191, 112), true);
        AddLabel("Mỗi dòng là một ô checklist theo đúng thứ tự nguồn. Chọn dòng để đặt làm nhiệm vụ trên HUD.", 22, 48, 1080, 27, 9, ForeColor);
        _search.Location = new Point(22, 84); _search.Width = 410; _search.BackColor = Color.FromArgb(37, 46, 59); _search.ForeColor = ForeColor;
        _search.PlaceholderText = "Tìm boss, vật phẩm, địa điểm, đoạn chỉ dẫn…"; _search.TextChanged += (_, _) => Filter(); Controls.Add(_search);
        _sections.Location = new Point(446, 83); _sections.BackColor = Color.FromArgb(37, 46, 59); _sections.ForeColor = ForeColor;
        _sections.SelectedIndexChanged += (_, _) => { if (!_loading) Filter(); }; Controls.Add(_sections);
        AddButton("Đồng bộ nguồn", 776, 82, 145, 32, async (_, _) => await _context.SyncSourceAsync(true));
        AddButton("Đóng", 1033, 82, 120, 32, (_, _) => Hide());
        _count.Location = new Point(22, 121); _count.ForeColor = Color.FromArgb(160, 173, 186); Controls.Add(_count);
        _list.Location = new Point(22, 151); _list.Size = new Size(545, 550); _list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        _list.BackColor = Color.FromArgb(28, 35, 46); _list.ForeColor = ForeColor;
        _list.Columns.Add("#", 75); _list.Columns.Add("Bước theo nguồn", 447);
        _list.SelectedIndexChanged += (_, _) => ShowSelection();
        _list.DoubleClick += (_, _) => Jump();
        Controls.Add(_list);
        AddLabel("CHI TIẾT BƯỚC", 589, 151, 526, 27, 9, Color.FromArgb(226, 191, 112), true);
        _where.Location = new Point(589, 180); _where.ForeColor = Color.FromArgb(160, 173, 186); Controls.Add(_where);
        _detail.Location = new Point(589, 246); _detail.Size = new Size(563, 329);
        _detail.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _detail.BackColor = Color.FromArgb(28, 35, 46); _detail.ForeColor = ForeColor; _detail.Font = new Font("Segoe UI", 11);
        Controls.Add(_detail);
        AddButton("ĐẶT LÀM NHIỆM VỤ", 589, 590, 220, 45, (_, _) => Jump(), true);
        AddButton("MỞ BẢN ĐỒ", 822, 590, 157, 45, (_, _) => { Jump(); _context.OpenMap(); });
        AddButton("XEM NGUỒN", 990, 590, 162, 45, (_, _) => { Jump(); _context.OpenGuide(); });
        RefreshFromContext();
    }

    private void AddLabel(string text, int x, int y, int width, int height, float size, Color color, bool bold = false)
        => Controls.Add(new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height), ForeColor = color,
            BackColor = BackColor, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular) });

    private void AddButton(string text, int x, int y, int width, int height, EventHandler click, bool primary = false)
    {
        var button = new Button { Text = text, Location = new Point(x, y), Size = new Size(width, height), FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Color.FromArgb(226, 191, 112) : Color.FromArgb(43, 52, 65),
            ForeColor = primary ? Color.FromArgb(17, 21, 28) : ForeColor, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        button.FlatAppearance.BorderSize = 0; button.Click += click; Controls.Add(button);
    }

    public void RefreshFromContext()
    {
        if (IsDisposed) return;
        if (_loadedCount != _context.ActiveSteps.Count || _loadedRevision != _context.SourceRevision)
        {
            _loadedCount = _context.ActiveSteps.Count;
            _loadedRevision = _context.SourceRevision;
            _loading = true; _sections.Items.Clear(); _sections.Items.Add("Tất cả khu vực · game gốc + DLC");
            foreach (var section in _context.SourceSections) _sections.Items.Add(section);
            _sections.SelectedIndex = 0; _loading = false;
            Filter();
        }
        else _count.Text = $"{_context.ActiveSteps.Count:N0} bước · đang theo dõi #{_context.Cursor + 1:N0}";
    }

    private void Filter()
    {
        var query = _search.Text.Trim();
        var section = _sections.SelectedItem as SourceSection;
        _list.BeginUpdate(); _list.Items.Clear();
        foreach (var step in _context.ActiveSteps)
        {
            if (section != null && (step.Index < section.StartIndex || step.Index >= section.StartIndex + section.Count)) continue;
            if (query.Length > 0 && !($"{step.Title} {step.Actions.FirstOrDefault()} {step.SourceText} {step.Phase}").Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
            var item = new ListViewItem($"{step.Index + 1:N0}"); item.SubItems.Add(step.Title); item.Tag = step.Index;
            if (_context.IsDone(step)) item.ForeColor = Color.FromArgb(129, 181, 153);
            _list.Items.Add(item);
        }
        _list.EndUpdate();
        _count.Text = $"Hiển thị {_list.Items.Count:N0}/{_context.ActiveSteps.Count:N0} bước · đang theo dõi #{_context.Cursor + 1:N0}";
    }

    private void ShowSelection()
    {
        if (_list.SelectedItems.Count == 0) return;
        if (_list.SelectedItems[0].Tag is not int index) return;
        var step = _context.ActiveSteps[index];
        _where.Text = $"{step.Phase}\n{step.Kind} · {( _context.IsDone(step) ? "Đã xác nhận" : "Chưa xác nhận")}";
        _detail.Text = step.Actions.FirstOrDefault() ?? step.Title;
    }

    private void Jump()
    {
        if (_list.SelectedItems.Count == 0) return;
        if (_list.SelectedItems[0].Tag is int index) _context.JumpTo(index);
    }
}

internal sealed class SettingsForm : Form
{
    private readonly OverlayContext _context;
    private readonly ComboBox _slots = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 475, FlatStyle = FlatStyle.Flat };
    private readonly Label _save = new() { AutoSize = false, Width = 484, Height = 28, AutoEllipsis = true };
    private readonly Label _status = new() { AutoSize = false, Width = 505, Height = 30 };
    private readonly Label _quest = new() { AutoSize = false, Width = 625, Height = 54 };
    private readonly Label _backlog = new() { AutoSize = false, Width = 625, Height = 25 };
    private readonly CheckBox _onlyGame = new() { Text = "Chỉ hiện HUD khi Elden Ring đang được chọn", AutoSize = true };
    private readonly ToolTip _tips = new();
    private static readonly Color Background = Color.FromArgb(14, 18, 24);
    private static readonly Color Header = Color.FromArgb(22, 28, 37);
    private static readonly Color Card = Color.FromArgb(28, 35, 46);
    private static readonly Color Field = Color.FromArgb(38, 47, 59);
    private static readonly Color Gold = Color.FromArgb(226, 191, 112);
    private static readonly Color White = Color.FromArgb(244, 245, 243);
    private static readonly Color Muted = Color.FromArgb(160, 173, 186);
    private bool _updating;

    public SettingsForm(OverlayContext context)
    {
        _context = context;
        Text = "Elden Ring · La bàn nhiệm vụ";
        Width = 720; Height = 730;
        MinimumSize = new Size(720, 730);
        MaximumSize = new Size(720, 730);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        Font = new Font("Segoe UI", 10);
        BackColor = Background;
        ForeColor = White;
        TopMost = true;
        FormClosing += (_, e) => { e.Cancel = true; Hide(); };

        var header = new Panel { Location = new Point(0, 0), Size = new Size(720, 92), BackColor = Header };
        header.Paint += (_, e) => { using var brush = new SolidBrush(Gold); e.Graphics.FillRectangle(brush, 0, 0, 5, 92); };
        header.MouseDown += (_, e) => Drag(e);
        Controls.Add(header);
        AddLabel(header, "✧  THE LANDS BETWEEN", 27, 14, 350, 22, 9, Gold, true);
        AddLabel(header, "La bàn nhiệm vụ", 27, 35, 460, 39, 21, White, true);
        AddLabel(header, "BẢN DEMO", 488, 43, 137, 24, 9, Gold, true);
        AddButton(header, "—", 628, 23, 36, 37, (_, _) => WindowState = FormWindowState.Minimized, false);
        AddButton(header, "×", 670, 23, 36, 37, (_, _) => Hide(), false);

        var source = AddCard(24, 110, 672, 177);
        AddLabel(source, "DỮ LIỆU NHÂN VẬT", 21, 14, 400, 22, 9, Gold, true);
        AddLabel(source, "FILE SAVE", 21, 43, 200, 19, 8.5f, Muted, true);
        _save.Location = new Point(21, 64); _save.BackColor = Card; _save.ForeColor = White; _save.Font = new Font("Segoe UI", 9.5f); source.Controls.Add(_save);
        AddButton(source, "ĐỔI FILE", 535, 59, 115, 34, (_, _) => Browse(), false);
        AddLabel(source, "NHÂN VẬT ĐANG CHƠI", 21, 105, 260, 19, 8.5f, Muted, true);
        _slots.Location = new Point(21, 129); _slots.Height = 34; _slots.BackColor = Field; _slots.ForeColor = White;
        _slots.SelectedIndexChanged += (_, _) => { if (!_updating && _slots.SelectedItem is SlotInfo slot) _context.SelectSlot(slot.Index); };
        source.Controls.Add(_slots);
        AddButton(source, "ĐỌC LẠI", 535, 127, 115, 36, (_, _) => _ = _context.RefreshSaveAsync(true), false);

        _onlyGame.Location = new Point(29, 301); _onlyGame.ForeColor = White; _onlyGame.BackColor = Background;
        _onlyGame.CheckedChanged += (_, _) => { if (!_updating) _context.SetOnlyInGame(_onlyGame.Checked); };
        Controls.Add(_onlyGame);
        AddLabel(this, "HUD sẽ nằm góc trên bên phải và không chiếm màn hình khi bạn Alt+Tab.", 29, 325, 655, 23, 9, Muted);

        var quest = AddCard(24, 360, 672, 208);
        AddLabel(quest, "NHIỆM VỤ HIỆN TẠI", 21, 15, 440, 22, 9, Gold, true);
        _quest.Location = new Point(21, 42); _quest.BackColor = Card; _quest.ForeColor = White;
        _quest.Font = new Font("Segoe UI", 16, FontStyle.Bold); quest.Controls.Add(_quest);
        _backlog.Location = new Point(21, 101); _backlog.BackColor = Card; _backlog.ForeColor = Muted;
        _backlog.Font = new Font("Segoe UI", 9.5f); quest.Controls.Add(_backlog);
        AddButton(quest, "◀  TRƯỚC", 21, 146, 105, 42, (_, _) => _context.Move(-1), false);
        AddButton(quest, "✓  HOÀN TẤT", 134, 146, 137, 42, (_, _) => _context.MarkDone(), true);
        AddButton(quest, "TIẾP  ▶", 279, 146, 105, 42, (_, _) => _context.Move(1), false);
        AddButton(quest, "BẢN ĐỒ", 392, 146, 124, 42, (_, _) => _context.OpenMap(), false);
        AddButton(quest, "TẤT CẢ", 524, 146, 126, 42, (_, _) => _context.OpenGuideList(), false);

        var help = AddCard(24, 584, 672, 80);
        AddLabel(help, "ĐIỀU KHIỂN NHANH", 20, 12, 350, 22, 9, Gold, true);
        AddLabel(help, "Tay cầm: Home = tiếp · Home ×2 = lùi · giữ Home = ẩn/hiện   |   Phím: F8 ẩn/hiện · F10/F11 đổi bước", 20, 37, 628, 38, 9, White);

        _status.Location = new Point(27, 684); _status.ForeColor = Color.FromArgb(135, 206, 172);
        _status.BackColor = Background; _status.Font = new Font("Segoe UI", 9);
        Controls.Add(_status);
        AddButton(this, "THOÁT ỨNG DỤNG", 542, 676, 153, 36, (_, _) => _context.Exit(), false);
        RefreshFromContext();
    }

    private Panel AddCard(int x, int y, int width, int height)
    {
        var card = new Panel { Location = new Point(x, y), Size = new Size(width, height), BackColor = Card };
        card.Paint += (_, e) => { using var pen = new Pen(Color.FromArgb(57, 66, 78)); e.Graphics.DrawRectangle(pen, 0, 0, width - 1, height - 1); };
        Controls.Add(card);
        return card;
    }

    private static void AddLabel(Control parent, string text, int x, int y, int width, int height, float size, Color color, bool bold = false)
    {
        parent.Controls.Add(new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height), BackColor = parent.BackColor,
            ForeColor = color, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular) });
    }

    private static void AddButton(Control parent, string text, int x, int y, int width, int height, EventHandler click, bool primary)
    {
        var button = new Button { Text = text, Location = new Point(x, y), Size = new Size(width, height), FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Gold : Field, ForeColor = primary ? Background : White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold), UseVisualStyleBackColor = false };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(242, 209, 139) : Color.FromArgb(54, 65, 79);
        button.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(194, 156, 79) : Color.FromArgb(72, 82, 94);
        button.Click += click; parent.Controls.Add(button);
    }

    private void Drag(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        Native.ReleaseCapture();
        Native.SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
    }

    private void Browse()
    {
        using var dialog = new OpenFileDialog { Title = "Chọn ER0000.sl2", Filter = "Elden Ring save (*.sl2)|*.sl2|Tất cả file (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) _context.SelectSave(dialog.FileName);
    }

    public void RefreshFromContext()
    {
        if (IsDisposed) return;
        _updating = true;
        _save.Text = string.IsNullOrWhiteSpace(_context.SavePath) ? "Chưa có file save" : _context.SavePath;
        _tips.SetToolTip(_save, _context.SavePath);
        _slots.BeginUpdate(); _slots.Items.Clear();
        foreach (var slot in _context.Slots) _slots.Items.Add(slot);
        if (_slots.Items.Count > 0) _slots.SelectedItem = _slots.Items.Cast<SlotInfo>().FirstOrDefault(slot => slot.Index == _context.SlotIndex);
        _slots.EndUpdate();
        _onlyGame.Checked = _context.ShowOnlyInGame;
        _quest.Text = _context.CurrentStep is { } step ? $"{step.Index + 1:00}/{_context.StepCount:00}  ·  {step.Title}" : "Đang chờ dữ liệu save…";
        _backlog.Text = _context.CurrentStep is { } ? $"{_context.BacklogCount} mốc trước đây cần rà soát  ·  Dùng nút Trước để xem lại" : "Chọn file save và nhân vật để bắt đầu.";
        _status.Text = _context.Status;
        _status.ForeColor = _context.Status.Contains("lỗi", StringComparison.OrdinalIgnoreCase) || _context.Status.Contains("Không", StringComparison.OrdinalIgnoreCase)
            ? Color.FromArgb(235, 144, 137) : Color.FromArgb(135, 206, 172);
        _updating = false;
    }
}
