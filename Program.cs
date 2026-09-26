using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace HwndViewer;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public class MainForm : Form
{
    private DataGridView grid = null!;
    private TextBox txtFilter = null!;
    private CheckBox chkOnlyVisible = null!;
    private CheckBox chkOnlyPopups = null!;
    private Label lblStatus = null!;
    private TextBox txtDetail = null!;
    private List<WindowInfo> _currentList = new();

    public MainForm()
    {
        Text = "HWND Viewer v2 - Parent vs Owner (GW_OWNER) + Flash & Focus";
        Width = 1520;
        Height = 860;
        StartPosition = FormStartPosition.CenterScreen;

        var topPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 75, Padding = new Padding(8), AutoSize = false, WrapContents = true };
        
        var lbl = new Label { Text = "Filtro (UUID / título / clase / proceso / HWND):", AutoSize = true, Margin = new Padding(0,8,5,0) };
        topPanel.Controls.Add(lbl);
        txtFilter = new TextBox { Width = 320, PlaceholderText = "Ej: UUID de tu popup, Chrome, 0x123..." };
        txtFilter.TextChanged += (_, _) => RefreshWindows();
        topPanel.Controls.Add(txtFilter);

        chkOnlyVisible = new CheckBox { Text = "Solo visibles", Checked = true, AutoSize = true, Margin = new Padding(15, 5, 0, 0) };
        chkOnlyVisible.CheckedChanged += (_, _) => RefreshWindows();
        topPanel.Controls.Add(chkOnlyVisible);

        chkOnlyPopups = new CheckBox { Text = "Solo popups con Owner", AutoSize = true, Margin = new Padding(15, 5, 0, 0) };
        chkOnlyPopups.CheckedChanged += (_, _) => RefreshWindows();
        topPanel.Controls.Add(chkOnlyPopups);

        var btnRefresh = new Button { Text = "🔄 Refrescar (F5)", AutoSize = true, Margin = new Padding(15, 0, 0, 0) };
        btnRefresh.Click += (_, _) => RefreshWindows();
        topPanel.Controls.Add(btnRefresh);

        // Botones de acción
        var sep = new Label { Text = " | Acciones sobre selección:", AutoSize = true, Margin = new Padding(15, 8, 5, 0), Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold) };
        topPanel.Controls.Add(sep);

        var btnFlash = new Button { Text = "⚡ Flashear", AutoSize = true, BackColor = System.Drawing.Color.LightGoldenrodYellow };
        btnFlash.Click += (_, _) => FlashSelected();
        topPanel.Controls.Add(btnFlash);

        var btnFront = new Button { Text = "🎯 Traer al frente", AutoSize = true, BackColor = System.Drawing.Color.LightGreen };
        btnFront.Click += (_, _) => BringSelectedToFront();
        topPanel.Controls.Add(btnFront);

        var btnCopy = new Button { Text = "📋 Copiar HWND", AutoSize = true };
        btnCopy.Click += (_, _) => CopySelectedHwnd();
        topPanel.Controls.Add(btnCopy);

        var btnCopyHex = new Button { Text = "📋 Copiar HEX", AutoSize = true };
        btnCopyHex.Click += (_, _) => CopySelectedHwndHex();
        topPanel.Controls.Add(btnCopyHex);

        Controls.Add(topPanel);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 520 };
        Controls.Add(split);
        split.BringToFront();

        grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false
        };
        grid.SelectionChanged += Grid_SelectionChanged;
        grid.CellDoubleClick += (s, e) => FlashSelected(); // doble click ahora flashea
        split.Panel1.Controls.Add(grid);

        txtDetail = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new System.Drawing.Font("Consolas", 10), ReadOnly = true };
        split.Panel2.Controls.Add(txtDetail);

        lblStatus = new Label { Dock = DockStyle.Bottom, Height = 28, Text = "Listo", Padding = new Padding(8, 4, 0, 0), BackColor = System.Drawing.Color.FromArgb(240,240,240) };
        Controls.Add(lblStatus);

        KeyPreview = true;
        KeyDown += (s, e) => {
            if (e.KeyCode == Keys.F5) RefreshWindows();
            if (e.KeyCode == Keys.F && e.Control) txtFilter.Focus();
        };

        RefreshWindows();
    }

    private WindowInfo? GetSelectedWindow()
    {
        if (grid.SelectedRows.Count == 0) return null;
        int idx = grid.SelectedRows[0].Index;
        if (idx < 0 || idx >= _currentList.Count) return null;
        return _currentList[idx];
    }

    private void FlashSelected()
    {
        var w = GetSelectedWindow();
        if (w == null) { MessageBox.Show("Selecciona una ventana primero."); return; }
        bool ok = WindowEnumerator.FlashWindow(w.Hwnd);
        lblStatus.Text = ok ? $"Flasheando 0x{w.Hwnd.ToString("X")} - {w.Title}" : $"No se pudo flashear {w.Hwnd}";
    }

    private void BringSelectedToFront()
    {
        var w = GetSelectedWindow();
        if (w == null) { MessageBox.Show("Selecciona una ventana primero."); return; }
        bool ok = WindowEnumerator.BringToFront(w.Hwnd);
        lblStatus.Text = ok ? $"Ventana 0x{w.Hwnd.ToString("X")} traída al frente" : $"No se pudo traer al frente (puede ser privilegios)";
    }

    private void CopySelectedHwnd()
    {
        var w = GetSelectedWindow();
        if (w == null) return;
        Clipboard.SetText(w.Hwnd.ToString());
        lblStatus.Text = $"HWND decimal {w.Hwnd} copiado";
    }
    private void CopySelectedHwndHex()
    {
        var w = GetSelectedWindow();
        if (w == null) return;
        string hex = $"0x{w.Hwnd.ToString("X")}";
        Clipboard.SetText(hex);
        lblStatus.Text = $"HWND hex {hex} copiado";
    }

    private void Grid_SelectionChanged(object? sender, EventArgs e)
    {
        var w = GetSelectedWindow();
        if (w == null) return;

        string ownerTitle = WindowEnumerator.GetWindowTitle(w.Owner);
        string parentTitle = WindowEnumerator.GetWindowTitle(w.Parent);

        txtDetail.Text = $@"--- VENTANA SELECCIONADA ---
HWND: {w.Hwnd} (0x{w.Hwnd.ToString("X")})  [Decimal para C#: {w.Hwnd} | Hex para C++: 0x{w.Hwnd.ToString("X")}]
Título: {w.Title}
Clase: {w.ClassName}
PID: {w.ProcessId} | Proceso: {w.ProcessName}
Visible: {w.IsVisible}
Style: 0x{w.Style:X8} (WS_CHILD={w.IsChild} WS_POPUP={w.IsPopup}) | ExStyle: 0x{w.ExStyle:X8}

[GetParent()]  -> Devuelve PADRE si es WS_CHILD, o dueño si es WS_POPUP (poco fiable)
  HWND: {w.Parent} (0x{w.Parent.ToString("X")}) -> ""{parentTitle}""

[GetWindow(GW_OWNER=4)]  <- MÉTODO RECOMENDADO PARA TU CASO (Chrome extension popup)
  HWND: {w.Owner} (0x{w.Owner.ToString("X")}) -> ""{ownerTitle}""
  Proceso Owner: {WindowEnumerator.GetProcessNameForHwnd(w.Owner)}

[GetAncestor(GA_ROOTOWNER=3)] Root Owner:
  HWND: {w.RootOwner} (0x{w.RootOwner.ToString("X")}) -> ""{WindowEnumerator.GetWindowTitle(w.RootOwner)}""

CADENA DE OWNERS (útil para debug):
{WindowEnumerator.BuildOwnerChain(w.Hwnd)}

CÓDIGO PARA TU COMPANION:
--------------------------------
IntPtr popup = new IntPtr(0x{w.Hwnd.ToString("X")}); // {w.Title}
IntPtr owner = GetWindow(popup, 4); // GW_OWNER -> 0x{w.Owner.ToString("X")}
if (owner != IntPtr.Zero) {{ /* owner es la ventana principal de Chrome */ }}

ATAJOS:
- Doble click en grilla = Flashear
- F5 = Refrescar
";
    }

    private void RefreshWindows()
    {
        var filter = txtFilter.Text.Trim().ToLowerInvariant();
        var allWindows = WindowEnumerator.GetAllWindows();

        var hwndToTitle = new Dictionary<IntPtr, string>();
        foreach (var w in allWindows) hwndToTitle[w.Hwnd] = w.Title;

        _currentList = new List<WindowInfo>();

        foreach (var w in allWindows)
        {
            if (chkOnlyVisible.Checked && !w.IsVisible) continue;
            if (chkOnlyPopups.Checked && w.Owner == IntPtr.Zero) continue;

            string searchBlob = $"{w.Hwnd} 0x{w.Hwnd.ToString("X")} {w.Title} {w.ClassName} {w.ProcessName} {w.ProcessId}".ToLowerInvariant();
            if (!string.IsNullOrEmpty(filter) && !searchBlob.Contains(filter)) continue;

            _currentList.Add(w);
        }

        grid.SuspendLayout();
        var table = new System.Data.DataTable();
        table.Columns.Add("HWND", typeof(string));
        table.Columns.Add("HWND Hex", typeof(string));
        table.Columns.Add("Título", typeof(string));
        table.Columns.Add("Clase", typeof(string));
        table.Columns.Add("PID", typeof(int));
        table.Columns.Add("Proceso", typeof(string));
        table.Columns.Add("Visible", typeof(bool));
        table.Columns.Add("Es Popup?", typeof(bool));
        table.Columns.Add("Es Child?", typeof(bool));
        table.Columns.Add("GetParent Hex", typeof(string));
        table.Columns.Add("Owner (GW_OWNER) Dec", typeof(string));
        table.Columns.Add("Owner Hex", typeof(string));
        table.Columns.Add("Owner Título", typeof(string));
        table.Columns.Add("Root Owner Hex", typeof(string));

        foreach (var w in _currentList)
        {
            string parentHex = w.Parent == IntPtr.Zero ? "-" : $"0x{w.Parent.ToString("X")}";
            string ownerDec = w.Owner == IntPtr.Zero ? "-" : w.Owner.ToString();
            string ownerHex = w.Owner == IntPtr.Zero ? "-" : $"0x{w.Owner.ToString("X")}";
            string ownerTitle = w.Owner != IntPtr.Zero ? WindowEnumerator.GetWindowTitle(w.Owner) : "";
            string rootHex = w.RootOwner == IntPtr.Zero ? "-" : $"0x{w.RootOwner.ToString("X")}";

            table.Rows.Add(
                w.Hwnd.ToString(),
                $"0x{w.Hwnd.ToString("X")}",
                w.Title,
                w.ClassName,
                w.ProcessId,
                w.ProcessName,
                w.IsVisible,
                w.IsPopup,
                w.IsChild,
                parentHex,
                ownerDec,
                ownerHex,
                ownerTitle,
                rootHex
            );
        }

        grid.DataSource = table;
        grid.ResumeLayout();
        lblStatus.Text = $"Encontradas {_currentList.Count} ventanas (de {allWindows.Count} totales) | Doble click = Flashear | F5 = Refrescar";
    }
}

public class WindowInfo
{
    public IntPtr Hwnd;
    public string Title = "";
    public string ClassName = "";
    public int ProcessId;
    public string ProcessName = "";
    public bool IsVisible;
    public uint Style;
    public uint ExStyle;
    public bool IsChild;
    public bool IsPopup;
    public IntPtr Parent;
    public IntPtr Owner;
    public IntPtr RootOwner;
}

public static class WindowEnumerator
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

    // Nuevos para Flash y BringToFront
    [DllImport("user32.dll")] private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }
    private const uint FLASHW_ALL = 3;
    private const uint FLASHW_TIMERNOFG = 12;

    private const uint GW_OWNER = 4;
    private const uint GA_ROOTOWNER = 3;
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const uint WS_CHILD = 0x40000000;
    private const uint WS_POPUP = 0x80000000;
    private const int SW_RESTORE = 9;

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8) return GetWindowLongPtr64(hWnd, nIndex);
        return GetWindowLong32(hWnd, nIndex);
    }

    public static List<WindowInfo> GetAllWindows()
    {
        var list = new List<WindowInfo>();
        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindow(hWnd)) return true;
            list.Add(CreateInfo(hWnd));
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public static WindowInfo CreateInfo(IntPtr hWnd)
    {
        string title = GetWindowTitle(hWnd);
        string className = GetClassNameStr(hWnd);
        GetWindowThreadProcessId(hWnd, out uint pid);
        string procName = GetProcessName((int)pid);
        bool visible = IsWindowVisible(hWnd);

        IntPtr stylePtr = GetWindowLongPtr(hWnd, GWL_STYLE);
        IntPtr exStylePtr = GetWindowLongPtr(hWnd, GWL_EXSTYLE);
        uint style = (uint)stylePtr.ToInt64();
        uint exStyle = (uint)exStylePtr.ToInt64();

        bool isChild = (style & WS_CHILD) != 0;
        bool isPopup = (style & WS_POPUP) != 0;

        IntPtr parent = GetParent(hWnd);
        IntPtr owner = GetWindow(hWnd, GW_OWNER);
        IntPtr rootOwner = GetAncestor(hWnd, GA_ROOTOWNER);

        return new WindowInfo
        {
            Hwnd = hWnd,
            Title = title,
            ClassName = className,
            ProcessId = (int)pid,
            ProcessName = procName,
            IsVisible = visible,
            Style = style,
            ExStyle = exStyle,
            IsChild = isChild,
            IsPopup = isPopup,
            Parent = parent,
            Owner = owner,
            RootOwner = rootOwner
        };
    }

    public static bool FlashWindow(IntPtr hWnd)
    {
        var fInfo = new FLASHWINFO();
        fInfo.cbSize = (uint)Marshal.SizeOf(fInfo);
        fInfo.hwnd = hWnd;
        fInfo.dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG;
        fInfo.uCount = 5;
        fInfo.dwTimeout = 0;
        return FlashWindowEx(ref fInfo);
    }

    public static bool BringToFront(IntPtr hWnd)
    {
        if (IsIconic(hWnd))
            ShowWindow(hWnd, SW_RESTORE);
        BringWindowToTop(hWnd);
        return SetForegroundWindow(hWnd);
    }

    public static string GetWindowTitle(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return "";
        int len = GetWindowTextLength(hWnd);
        if (len == 0) return "";
        var sb = new StringBuilder(len + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetClassNameStr(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string GetProcessName(int pid)
    {
        try { return Process.GetProcessById(pid).ProcessName; }
        catch { return ""; }
    }

    public static string GetProcessNameForHwnd(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return "-";
        GetWindowThreadProcessId(hwnd, out uint pid);
        return GetProcessName((int)pid) + $" (PID {pid})";
    }

    public static string BuildOwnerChain(IntPtr startHwnd)
    {
        var sb = new StringBuilder();
        IntPtr current = startHwnd;
        int level = 0;
        var visited = new HashSet<IntPtr>();
        while (current != IntPtr.Zero && level < 10 && !visited.Contains(current))
        {
            visited.Add(current);
            string title = GetWindowTitle(current);
            sb.AppendLine($"  [{level}] 0x{current.ToString("X")} - \"{title}\" ({GetProcessNameForHwnd(current)})");
            IntPtr owner = GetWindow(current, GW_OWNER);
            if (owner == IntPtr.Zero) break;
            current = owner;
            level++;
        }
        if (level == 0) sb.AppendLine("  (sin owner - es ventana principal)");
        return sb.ToString();
    }
}
