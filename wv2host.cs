using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;

enum DockHideSide
{
    None,
    Left,
    Right
}

public class BookmarkItem
{
    public string Title { get; set; }
    public string Url { get; set; }
}

public class BookmarkFolder
{
    public string CategoryName { get; set; }
    public List<BookmarkItem> Items { get; set; }
    public BookmarkFolder()
    {
        Items = new List<BookmarkItem>();
    }
}

class Native
{
    public const int HK_ESC = 1;
    public const int HK_CTRL_L = 2;
    public const int HK_F5 = 3;
    public const int HK_DOCKTOGGLE = 4;
    public const int MOD_NONE = 0x0000;
    public const int MOD_CTRL = 0x0002;
    public const int MOD_SHIFT = 0x0004;
    public const int VK_ESCAPE = 0x1B;
    public const int VK_L = 0x4C;
    public const int VK_F5 = 0x74;
    public const int VK_T = 0x54;
    public const int HK_GRAYSCALE = 6;
    public const int HK_OPACITY = 7;
    public const int VK_F7 = 0x76;
    public const int VK_F8 = 0x77;
    public const int MOD_ALT = 0x0001;
    public const int WM_HOTKEY = 0x0312;
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool ReleaseCapture();
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_LAYERED = 0x80000;
    public const int WS_EX_TRANSPARENT = 0x20;
    public const int HK_CLICKTHROUGH = 5;
    public const int VK_F9 = 0x78;
}

class EscFilter : IMessageFilter
{
    Form _form;
    public EscFilter(Form form) { _form = form; }
    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == Native.HK_ESC) { _form.Close(); return true; }
        return false;
    }
}

class ClickThroughFilter : IMessageFilter
{
    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == Native.HK_CLICKTHROUGH)
        {
            Program.ToggleClickThrough();
            return true;
        }
        return false;
    }
}

class GrayScaleFilter : IMessageFilter
{
    private WebView2 _wv;
    public GrayScaleFilter(WebView2 wv)
    {
        _wv = wv;
    }
    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == Native.HK_GRAYSCALE)
        {
            if (_wv.CoreWebView2 != null)
            {
                _wv.CoreWebView2.ExecuteScriptAsync("uiToggleGrayscale();");
            }
            return true;
        }
        return false;
    }
}

class OpacityHotkeyFilter : IMessageFilter
{
    Action _opacityToggle;
    public OpacityHotkeyFilter(Action opacityToggle)
    {
        _opacityToggle = opacityToggle;
    }
    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == Native.HK_OPACITY)
        {
            if(_opacityToggle != null)
            {
                _opacityToggle.Invoke();
            }
            return true;
        }
        return false;
    }
}

class CtrlLFilter : IMessageFilter
{
    Panel _toolbar;
    TextBox _addr;
    WebView2 _wv;
    Button _btnBack;
    public CtrlLFilter(Panel toolbar, TextBox addr, WebView2 wv, Button btnBack)
    { _toolbar = toolbar; _addr = addr; _wv = wv; _btnBack = btnBack; }
    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == Native.HK_CTRL_L)
        {
            bool vis = !_toolbar.Visible;
            _toolbar.Visible = vis;
            if (vis)
            {
                try { _addr.Text = _wv.CoreWebView2 != null && _wv.CoreWebView2.Source != null ? _wv.CoreWebView2.Source : ""; } catch { _addr.Text = ""; }
                _btnBack.Enabled = _wv.CoreWebView2 != null && _wv.CoreWebView2.CanGoBack;
                _addr.Focus(); _addr.SelectAll();
            }
            else { _wv.Focus(); }
            return true;
        }
        return false;
    }
}

class F5Filter : IMessageFilter
{
    WebView2 _wv;
    public F5Filter(WebView2 wv) { _wv = wv; }
    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == Native.HK_F5)
        { if (_wv.CoreWebView2 != null) _wv.CoreWebView2.Reload(); return true; }
        return false;
    }
}

class DockToggleFilter : IMessageFilter
{
    Action _dockToggleAction;
    public DockToggleFilter(Action dockToggleAction)
    {
        _dockToggleAction = dockToggleAction;
    }
    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == Native.HK_DOCKTOGGLE)
        {
            if (_dockToggleAction != null) _dockToggleAction.Invoke();
            return true;
        }
        return false;
    }
}

class Program
{
    private static DockHideSide _dockSide = DockHideSide.None;
    private static Rectangle _savedOrigBounds;
    private static System.Windows.Forms.Timer monitorTimer;
    private static System.Windows.Forms.Timer leaveTimer;
    private static ContextMenuStrip formRightMenu;
    static Action DoDockToggle;
    static string _jsonFavPath;
    static string _oldTxtFavPath;
    static List<BookmarkFolder> _bookmarkData = new List<BookmarkFolder>();
    static Form mainForm;
    static WebView2 _currentWv;   // ← 新增
    static TextBox _addressBar;   // ← 新增
    //透明三档；刷新页面会丢失，需要重新点菜单
    private static double[] _opacityLevels = { 1.0d, 0.70d, 0.45d };
    private static int _opacityIndex = 0;
    private static bool _clickThroughActive = false;
    private static bool _savedTopMost;

static bool IsWebView2Installed()
{
    try
    {
        string ver = CoreWebView2Environment.GetAvailableBrowserVersionString();
        return !string.IsNullOrEmpty(ver);
    }
    catch
    {
        return false;
    }
}

static int EnsureWebView2Runtime()
{
    if (IsWebView2Installed()) return 0;

    // 老系统/老.NET 默认可能不是 TLS1.2，微软下载站需要 TLS1.2
    try
    {
        System.Net.ServicePointManager.SecurityProtocol =
            System.Net.SecurityProtocolType.Tls12 |
            (System.Net.SecurityProtocolType)768 |
            (System.Net.SecurityProtocolType)192;
    }
    catch { }

    MessageBox.Show(
        "未检测到 WebView2 运行时，程序将自动下载并安装（需联网）。\n安装完成后会继续启动。",
        "index",
        MessageBoxButtons.OK,
        MessageBoxIcon.Information);

    string setupExe = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");

    try
    {
        // 如果之前失败残留了空/不完整文件，先删
        if (File.Exists(setupExe))
        {
            try { File.Delete(setupExe); } catch { }
        }

        using (var wc = new System.Net.WebClient())
        {
            wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            // go.microsoft.com/fwlink 会跳转到 msedge.sf.dl.delivery.mp.microsoft.com
            // WebClient 会自动跟随 HTTP 重定向；TLS 上面已设
            wc.DownloadFile("https://go.microsoft.com/fwlink/p/?LinkId=2124703", setupExe);
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine("WebView2 下载失败: " + ex);
        return 1; // 你会看到“WebView2 安装包下载失败，请检查网络/TLS/系统补丁”
    }

    if (!File.Exists(setupExe) || new FileInfo(setupExe).Length == 0)
        return 1;

    try
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = setupExe,
            Arguments = "/silent /install",
            UseShellExecute = true,
            Verb = "runas"
        };

        using (var p = System.Diagnostics.Process.Start(psi))
        {
            p.WaitForExit();
        }

        try { File.Delete(setupExe); } catch { }
    }
    catch
    {
        return 2;
    }

    return IsWebView2Installed() ? 0 : 2;
}

public static void ToggleClickThrough()
{
    if (mainForm == null) return;
    //最大化/全屏/kiosk禁止开启穿透
    if (mainForm.WindowState != FormWindowState.Normal)
        return;
    IntPtr hwnd = mainForm.Handle;
    int style = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
    style |= Native.WS_EX_LAYERED;
    if (!_clickThroughActive)
    {
        _savedTopMost = mainForm.TopMost;
        mainForm.TopMost = true;
        style |= Native.WS_EX_TRANSPARENT;
        _clickThroughActive = true;
    }
    else
    {
        style &= ~Native.WS_EX_TRANSPARENT;
        mainForm.TopMost = _savedTopMost;
        _clickThroughActive = false;
    }
    Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, style);
}

static string SimpleInputBox(string prompt, string title, string defaultValue)
{
    using (Form dlg = new Form())
    {
        dlg.Text = title;
        dlg.Size = new Size(380,140);
        dlg.StartPosition = FormStartPosition.CenterParent;
        dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
        dlg.MaximizeBox = false;
        Label lbl = new Label{Text=prompt,Location=new Point(12,10),AutoSize=true};
        TextBox txt = new TextBox{Location=new Point(12,32),Width=340,Text=defaultValue};
        Button btnOk = new Button{Text="确定",Location=new Point(190,65),Width=85,DialogResult=DialogResult.OK};
        Button btnCancel = new Button{Text="取消",Location=new Point(280,65),Width=85,DialogResult=DialogResult.Cancel};
        dlg.Controls.AddRange(new Control[]{lbl,txt,btnOk,btnCancel});
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;
        if(dlg.ShowDialog() == DialogResult.OK)
        {
            return txt.Text;
        }
        return null;
    }
}

static void Wv_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
{
    string msg = e.WebMessageAsJson;
    msg = msg.Trim('"');
    if(mainForm == null) return;

    if (msg == "showCustomMenu" && formRightMenu != null)
    {
        mainForm.BeginInvoke((MethodInvoker)delegate
        {
            formRightMenu.Show(Cursor.Position);
        });
    }
    else if(msg == "addBookmark")
    {
        CoreWebView2 wvSender = sender as CoreWebView2;
        if(wvSender == null) return;
        mainForm.BeginInvoke((MethodInvoker)delegate
        {
            string titleText = wvSender.DocumentTitle;
            if (string.IsNullOrWhiteSpace(titleText)) titleText = "未命名";
            string selCat;
            if (ShowAddBookmarkDialog(titleText, wvSender.Source, out selCat))
            {
                var fd = _bookmarkData.First(folder => folder.CategoryName == selCat);
                BookmarkItem bmItem = new BookmarkItem();
                bmItem.Title = titleText;
                bmItem.Url = wvSender.Source;
                fd.Items.Add(bmItem);
                SaveBookmarkData();
                MessageBox.Show("已保存到分类：" + selCat, "index", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        });
    }
    else if(msg == "toggleAddressBar")
    {
        mainForm.BeginInvoke((MethodInvoker)delegate
        {
            foreach(Control c in mainForm.Controls)
            {
                Panel p = c as Panel;
                if(p != null && p.Dock == DockStyle.Top)
                {
                    p.Visible = !p.Visible;
                    break;
                }
            }
        });
    }
    else if (msg == "toggleDockHide")
    {
        mainForm.BeginInvoke((MethodInvoker)delegate
        {
            DoDockToggle();   // ✅ 直接调逻辑，不模拟点击
        });
    }
    else if(msg == "toggleOpacity")
    {
        mainForm.BeginInvoke((MethodInvoker)delegate
        {
            _opacityIndex++;
            if (_opacityIndex >= _opacityLevels.Length)
                _opacityIndex = 0;
            mainForm.Opacity = _opacityLevels[_opacityIndex];
        });
    }
    else if(msg == "toggleClickThrough")
    {
        ToggleClickThrough();
    }
    else if(msg == "openExternalBrowser")
    {
        CoreWebView2 wvSender = sender as CoreWebView2;
        if(wvSender == null || string.IsNullOrEmpty(wvSender.Source))
            return;
        mainForm.BeginInvoke((MethodInvoker)delegate
        {
            string url = wvSender.Source;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch(Exception ex)
            {
                MessageBox.Show("打开失败(仅限在线网站)："+ex.Message,"提示",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            }
        });
    }
    else if(msg == "requestExit")
    {
        mainForm.BeginInvoke((MethodInvoker)delegate
        {
            mainForm.Close();
        });
    }
}

    static void LoadBookmarkData()
    {
        _bookmarkData.Clear();
        bool jsonExists = File.Exists(_jsonFavPath);
        bool oldTxtExists = File.Exists(_oldTxtFavPath);
        if (jsonExists)
        {
            try
            {
                string text = File.ReadAllText(_jsonFavPath);
                _bookmarkData = JsonConvert.DeserializeObject<List<BookmarkFolder>>(text) ?? new List<BookmarkFolder>();
            }
            catch
            {
                _bookmarkData = new List<BookmarkFolder>();
            }
        }
        if (!jsonExists && oldTxtExists)
        {
            BookmarkFolder uncat = new BookmarkFolder();
            uncat.CategoryName = "未分类";
            var lines = File.ReadAllLines(_oldTxtFavPath);
            foreach (var ln in lines)
            {
                if (string.IsNullOrWhiteSpace(ln) || ln.StartsWith("#")) continue;
                var idx = ln.LastIndexOf('|');
                if (idx <= 0) continue;
                BookmarkItem bi = new BookmarkItem();
                bi.Title = ln.Substring(0, idx).Trim();
                bi.Url = ln.Substring(idx + 1).Trim();
                uncat.Items.Add(bi);
            }
            _bookmarkData.Add(uncat);
            SaveBookmarkData();
        }
        if (_bookmarkData.All(f => f.CategoryName != "未分类"))
        {
            BookmarkFolder f = new BookmarkFolder();
            f.CategoryName = "未分类";
            _bookmarkData.Add(f);
        }
    }
    static void SaveBookmarkData()
    {
        string json = JsonConvert.SerializeObject(_bookmarkData, Formatting.Indented);
        File.WriteAllText(_jsonFavPath, json);
    }
    static bool ShowAddBookmarkDialog(string pageTitle, string pageUrl, out string selCategory)
    {
        selCategory = null;
        using (Form dlg = new Form())
        {
            dlg.Text = "添加到收藏夹";
            dlg.Size = new Size(420, 220);
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.MaximizeBox = false;
            Label lblTitle = new Label { Text = "标题:", Location = new Point(12, 12), AutoSize = true };
            TextBox txtTitle = new TextBox { Location = new Point(12, 32), Width = 370, Text = pageTitle };
            Label lblCat = new Label { Text = "保存到分类:", Location = new Point(12, 65), AutoSize = true };
            ComboBox cboCat = new ComboBox { Location = new Point(12, 85), Width = 370, DropDownStyle = ComboBoxStyle.DropDownList };

            foreach (var f in _bookmarkData)
            {
                cboCat.Items.Add(f.CategoryName);
            }
            cboCat.SelectedIndex = 0;
            Button btnNewCat = new Button { Text = "新建分类", Location = new Point(12, 120), Width = 110 };
            Button btnOk = new Button { Text = "确定", Location = new Point(220, 120), Width = 85, DialogResult = DialogResult.OK };
            Button btnCancel = new Button { Text = "取消", Location = new Point(310, 120), Width = 85, DialogResult = DialogResult.Cancel };
            dlg.Controls.AddRange(new Control[] { lblTitle, txtTitle, lblCat, cboCat, btnNewCat, btnOk, btnCancel });
            dlg.AcceptButton = btnOk;
            dlg.CancelButton = btnCancel;
            btnNewCat.Click += (s, e) =>
            {
                string input = SimpleInputBox("输入新分类名称：", "新建分类", "");
                if (string.IsNullOrWhiteSpace(input)) return;
                if (_bookmarkData.Any(f => f.CategoryName == input))
                {
                    MessageBox.Show("该分类已存在");
                    return;
                }
                BookmarkFolder newFolder = new BookmarkFolder();
                newFolder.CategoryName = input;
                _bookmarkData.Add(newFolder);
                cboCat.Items.Add(input);
                cboCat.SelectedItem = input;
                SaveBookmarkData();
            };
            var res = dlg.ShowDialog();
            if (res != DialogResult.OK)
            {
                return false;
            }
            selCategory = cboCat.Text;
            return true;
        }
    }
    static void ShowBookmarkManagerDialog()
    {
        using (Form dlg = new Form())
        {
            dlg.Text = "管理收藏夹";
            dlg.Size = new Size(620, 440);
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.MaximizeBox = false;
            dlg.MinimizeBox = false;
            Label lblCat = new Label { Text = "分类：", Location = new Point(12, 10), AutoSize = true };
            ComboBox cboCat = new ComboBox { Location = new Point(60, 8), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
            ListBox lstBook = new ListBox();
            lstBook.Location = new Point(12, 40);
            lstBook.Size = new Size(440, 280);
            // ---- 右侧按钮列（5 个）----
            Button btnAdd      = new Button { Text = "新增",       Location = new Point(460, 45),  Width = 110 };
            Button btnEdit     = new Button { Text = "修改选中",   Location = new Point(460, 77),  Width = 110 };
            Button btnDelItem  = new Button { Text = "删除书签",   Location = new Point(460, 109), Width = 110 };
            Button btnDelCat   = new Button { Text = "删除本分类", Location = new Point(460, 141), Width = 110 };
            Button btnOpen     = new Button { Text = "打开",       Location = new Point(460, 195), Width = 110 };
            // ---- 底部一条水平线（Y = 375）：关于 / 重置浏览器 / 记事本打开JSON / 关闭 ----
            Button btnAbout      = new Button { Text = "关于",           Location = new Point(12, 375),  Width = 110 };
            Button btnReset     = new Button { Text = "重置浏览器",     Location = new Point(135, 375), Width = 110 };
            Button btnOpenRawJson = new Button { Text = "记事本打开JSON", Location = new Point(260, 375), Width = 130 };
            Button btnClose      = new Button { Text = "关闭",           Location = new Point(460, 375), Width = 110, DialogResult = DialogResult.OK };
            dlg.Controls.AddRange(new Control[]
            {
                lblCat, cboCat, lstBook,
                btnAdd, btnEdit, btnDelItem, btnDelCat, btnOpen,   // 右侧 5 个
                btnAbout, btnReset, btnOpenRawJson, btnClose        // 底部水平线（顺序：关于 / 重置 / JSON / 关闭）
            });
            dlg.AcceptButton = btnClose;

            Func<BookmarkItem> getSelected = () =>
            {
                if (lstBook.SelectedIndex < 0 || cboCat.SelectedItem == null) return null;
                var folder = _bookmarkData.FirstOrDefault(f => f.CategoryName == cboCat.SelectedItem.ToString());
                if (folder == null || lstBook.SelectedIndex >= folder.Items.Count) return null;
                return folder.Items[lstBook.SelectedIndex];
            };

            Action RefreshCatCombo = () =>
            {
                cboCat.Items.Clear();
                foreach (var f in _bookmarkData) cboCat.Items.Add(f.CategoryName);
                if (cboCat.Items.Count > 0) cboCat.SelectedIndex = 0;
            };
            Action RefreshBookList = () =>
            {
                lstBook.Items.Clear();
                if (cboCat.SelectedItem == null) return;
                string selCatName = cboCat.SelectedItem.ToString();
                var folder = _bookmarkData.FirstOrDefault(f => f.CategoryName == selCatName);
                if (folder == null) return;
                foreach (var bm in folder.Items)
                {
                    lstBook.Items.Add(string.Format("{0} | {1}", bm.Title, bm.Url));
                }
            };
            RefreshCatCombo();
            RefreshBookList();
            cboCat.SelectedIndexChanged += (s, e) => { RefreshBookList(); };

            // ===== 打开（不关闭管理器）=====
            btnOpen.Click += (s, e) =>
            {
                var bm = getSelected();
                if (bm == null) { MessageBox.Show("请先选中一条书签"); return; }
                if (string.IsNullOrWhiteSpace(bm.Url)) { MessageBox.Show("该书签没有有效网址"); return; }
                if (_currentWv == null || _currentWv.CoreWebView2 == null) { MessageBox.Show("浏览器尚未就绪"); return; }
                if (_addressBar != null) _addressBar.Text = bm.Url;
                _currentWv.CoreWebView2.Navigate(bm.Url);
                // 不关闭管理器
            };

            // ===== 重置浏览器（原工具栏 btnClean 逻辑）=====
            Action doReset = () =>
            {
                var res = MessageBox.Show("确定要清除缓存并重置窗口状态？程序将重启。", "确认重置",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2);
                if (res != DialogResult.Yes) return;
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string batPath = Path.Combine(baseDir, "reset_clean.bat");
                if (File.Exists(batPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = batPath,
                        UseShellExecute = true
                    });
                    if (mainForm != null) mainForm.Close();
                }
                else
                {
                    MessageBox.Show("找不到 reset_clean.bat，请放到程序同目录！", "index", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            btnReset.Click += (s, e) => { doReset(); };

            // ===== 关于 =====
            btnAbout.Click += (s, e) => { ShowAboutDialog(); };

            btnAdd.Click += (s, e) =>
            {
                if (cboCat.SelectedItem == null) return;
                string selCatName = cboCat.SelectedItem.ToString();
                var fd = _bookmarkData.First(f => f.CategoryName == selCatName);
                string title = SimpleInputBox("输入书签标题", "新增书签", "");
                if (string.IsNullOrWhiteSpace(title)) return;
                string url = SimpleInputBox("输入网址URL", "新增书签", "https://");
                if (string.IsNullOrWhiteSpace(url)) return;
                fd.Items.Add(new BookmarkItem { Title = title, Url = url });
                SaveBookmarkData();
                RefreshBookList();
            };
            btnEdit.Click += (s, e) =>
            {
                var bm = getSelected();
                if (bm == null) { MessageBox.Show("请先在列表选中一条书签"); return; }
                string newTitle = SimpleInputBox("修改标题", "编辑书签", bm.Title);
                if (string.IsNullOrWhiteSpace(newTitle)) return;
                string newUrl = SimpleInputBox("修改网址URL", "编辑书签", bm.Url);
                if (string.IsNullOrWhiteSpace(newUrl)) return;
                bm.Title = newTitle;
                bm.Url = newUrl;
                SaveBookmarkData();
                RefreshBookList();
            };
            btnDelItem.Click += (s, e) =>
            {
                var bm = getSelected();
                if (bm == null) { MessageBox.Show("请先选中要删除的书签"); return; }
                var res = MessageBox.Show("确定删除这条书签？该操作不可撤销！", "确认删除",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2);
                if (res != DialogResult.Yes) return;
                var folder = _bookmarkData.First(f => f.CategoryName == cboCat.SelectedItem.ToString());
                folder.Items.Remove(bm);
                SaveBookmarkData();
                RefreshBookList();
            };
            btnDelCat.Click += (s, e) =>
            {
                if (cboCat.SelectedItem == null) return;
                if (_bookmarkData.Count <= 1)
                {
                    MessageBox.Show("至少保留一个分类，禁止删除最后一个分类！");
                    return;
                }
                string delCat = cboCat.SelectedItem.ToString();
                var res = MessageBox.Show(string.Format("确定要删除整个分类【{0}】？分类下全部书签一起删除，不可撤销！", delCat),
                    "危险确认", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2);
                if (res != DialogResult.Yes) return;
                var fObj = _bookmarkData.First(f => f.CategoryName == delCat);
                _bookmarkData.Remove(fObj);
                SaveBookmarkData();
                RefreshCatCombo();
                RefreshBookList();
            };
            btnOpenRawJson.Click += (s, e) =>
            {
                try
                {
                    if (!File.Exists(_jsonFavPath)) SaveBookmarkData();
                    System.Diagnostics.Process.Start("notepad.exe", _jsonFavPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "打开失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            dlg.ShowDialog();
        }
    }
    static void ShowAboutDialog()
{
    using (Form dlg = new Form())
    {
        dlg.Text = "关于";
        dlg.Size = new Size(360, 230);
        dlg.StartPosition = FormStartPosition.CenterParent;
        dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
        dlg.MaximizeBox = false;
        dlg.MinimizeBox = false;

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string icoPath = Path.Combine(baseDir, "app.ico");

        var pic = new PictureBox
        {
            Location = new Point(20, 20),
            Size = new Size(48, 48),
            SizeMode = PictureBoxSizeMode.Zoom
        };
        if (File.Exists(icoPath))
        {
            try { pic.Image = Image.FromFile(icoPath); }
            catch { }
        }
        dlg.Controls.Add(pic);

        var asm = Assembly.GetExecutingAssembly();
        var ver = asm.GetName().Version;
        string verStr = (ver != null && ver.Major > 0)
            ? string.Format("{0}.{1}.{2}", ver.Major, ver.Minor, ver.Build)
            : "1.0.0";

        dlg.Controls.Add(new Label
        {
            Text = "index 浏览器宿主",
            Location = new Point(80, 22),
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            AutoSize = true
        });

        dlg.Controls.Add(new Label
        {
            Text = "版本 " + verStr,
            Location = new Point(80, 48),
            AutoSize = true
        });

        dlg.Controls.Add(new Label
        {
            Text = "基于 Microsoft Edge WebView2",
            Location = new Point(80, 70),
            AutoSize = true
        });

        dlg.Controls.Add(new Label
        {
            Text = "英都阀门",
            Location = new Point(80, 95),
            AutoSize = true
        });

        var link = new LinkLabel
        {
            Text = "https://www.iovalve.com",
            Location = new Point(80, 115),
            AutoSize = true
        };
        link.LinkClicked += (sender, e) =>
        {
            string url = "https://www.iovalve.com";
            if (_currentWv == null || _currentWv.CoreWebView2 == null)
            {
                MessageBox.Show("浏览器尚未就绪");
                return;
            }
            _currentWv.CoreWebView2.Navigate(url);
            if (_addressBar != null) _addressBar.Text = url;
        };
        dlg.Controls.Add(link);

        var btnOk = new Button
        {
            Text = "确定",
            Location = new Point(140, 160),
            Width = 85,
            DialogResult = DialogResult.OK
        };
        dlg.Controls.Add(btnOk);
        dlg.AcceptButton = btnOk;
        dlg.ShowDialog();
    }
}
    static string OriginOf(string u)
    {
        try
        {
            var uri = new Uri(u);
            return uri.Scheme + "://" + uri.Host +
                (uri.Port != 80 && uri.Port != 443 ? ":" + uri.Port : "");
        }
        catch { return u; }
    }
    [STAThread]
    static void Main(string[] args)
    {
    // ===== WebView2 Runtime 自检 =====
    int rv = EnsureWebView2Runtime();
    if (rv != 0)
    {
        MessageBox.Show(
            rv == 1
                ? "WebView2 安装包下载失败，请检查网络。"
                : "WebView2 运行时安装失败，请手动安装后重试。",
            "index",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        return;
    }
    // ================================
        monitorTimer = new System.Windows.Forms.Timer();
        monitorTimer.Interval = 50;
        leaveTimer = new System.Windows.Forms.Timer();
        leaveTimer.Interval = 1200;
        monitorTimer.Stop();
        leaveTimer.Stop();
        string url = args.Length > 0 ? args[0] : new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nav", "index.html")).AbsoluteUri;
        string mode = args.Length > 1 ? args[1].ToLower() : "normal";
        int autoRefreshSec = 0;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i].StartsWith("refresh=", StringComparison.OrdinalIgnoreCase))
                int.TryParse(args[i].Substring("refresh=".Length), out autoRefreshSec);
        }
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _jsonFavPath = Path.Combine(baseDir, "fav.json");
        _oldTxtFavPath = Path.Combine(baseDir, "favorites.txt");
        LoadBookmarkData();
        if (mode == "clean" || url == "clean")
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "indexCache");
            if (Directory.Exists(root)) { try { Directory.Delete(root, true); } catch { } }
            MessageBox.Show(Directory.Exists(root) ? "缓存已清除" : "没有缓存可清", "index", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        int w = 1280, h = 720, x = -1, y = -1;
        if (args.Length > 2) int.TryParse(args[2], out w);
        if (args.Length > 3) int.TryParse(args[3], out h);
        if (args.Length > 4) int.TryParse(args[4], out x);
        if (args.Length > 5) int.TryParse(args[5], out y);
        string origin = OriginOf(url);
        string cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "indexCache", Math.Abs(origin.GetHashCode()).ToString());
        Directory.CreateDirectory(cacheDir);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var form = new Form();
        mainForm = form; // 赋值全局静态变量
        form.Text = "index";
        string icoPath = Path.Combine(baseDir, "app.ico");
        if (File.Exists(icoPath)) form.Icon = new Icon(icoPath);
        var wv = new WebView2();
        _currentWv = wv;   // ← 新增
        wv.Dock = DockStyle.Fill;
        var pb = new ProgressBar();
        pb.Dock = DockStyle.Top; pb.Height = 3; pb.Style = ProgressBarStyle.Marquee; pb.MarqueeAnimationSpeed = 25; pb.Visible = false;
        form.Controls.Add(pb); form.Controls.SetChildIndex(pb, 0);
        var toolbar = new Panel();
        toolbar.Dock = DockStyle.Top; toolbar.Height = 34; toolbar.BackColor = Color.FromArgb(245, 245, 245);
        toolbar.Padding = new Padding(0, 0, 0, 1); toolbar.Visible = false;
        form.Controls.Add(toolbar); form.Controls.SetChildIndex(toolbar, 0);
        Action<Button> styleBtn = (btn) =>
        {
            btn.FlatStyle = FlatStyle.Flat; btn.FlatAppearance.BorderSize = 1; btn.FlatAppearance.BorderColor = Color.FromArgb(220, 220, 220);
            btn.BackColor = Color.White; btn.ForeColor = Color.FromArgb(60, 60, 60); btn.Font = new Font("Segoe UI", 9f);
            btn.Height = 26; btn.TabStop = false;
        };
        var btnDrag = new Button() { Text = "\u2630", Location = new Point(4, 4), Width = 30, Cursor = Cursors.SizeAll }; styleBtn(btnDrag);
        btnDrag.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { Native.ReleaseCapture(); Native.SendMessage(form.Handle, 0xA1, 0x2, 0); } };
        toolbar.Controls.Add(btnDrag);
        var btnBack = new Button() { Text = "<<", Location = new Point(36, 4), Width = 30 }; styleBtn(btnBack); toolbar.Controls.Add(btnBack);
        var btnForward = new Button() { Text = ">>", Location = new Point(68, 4), Width = 30 }; styleBtn(btnForward); toolbar.Controls.Add(btnForward);
        var btnRefreshBtn = new Button() { Text = "\u21BB", Location = new Point(100, 4), Width = 30 }; styleBtn(btnRefreshBtn);
        btnRefreshBtn.Click += (s, e) => { if (wv.CoreWebView2 != null) wv.CoreWebView2.Reload(); };
        toolbar.Controls.Add(btnRefreshBtn);
        var btnFav = new Button() { Text = "\u2605", Location = new Point(132, 4), Width = 30 }; styleBtn(btnFav);
        toolbar.Controls.Add(btnFav);
        var btnDockToggle = new Button() { Name = "btnDockToggle", Text = "\u21C4", Location = new Point(164, 4), Width = 30 }; styleBtn(btnDockToggle);
        toolbar.Controls.Add(btnDockToggle);
        var toolTipDock = new ToolTip();
        toolTipDock.SetToolTip(btnDockToggle, "贴边隐藏 / 恢复窗口 (Ctrl+Shift+T全局热键)");
		        var addressBar = new TextBox();
        _addressBar = addressBar;
		var btnHome = new Button() { Text = "主页", Location = new Point(196, 4), Width = 50 };
		styleBtn(btnHome);
		toolTipDock.SetToolTip(btnHome, "主页");
		btnHome.Click += (s, e) =>
		{
 		   string homePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nav", "index.html");
 		   if (File.Exists(homePath) && wv.CoreWebView2 != null)
 		   {
  		      string homeUrl = new Uri(homePath).AbsoluteUri;
  		      wv.CoreWebView2.Navigate(homeUrl);
  		      addressBar.Text = homeUrl;
  		  }
  		  else
  		  {
  		      MessageBox.Show("找不到 nav/index.html！", "index", MessageBoxButtons.OK, MessageBoxIcon.Warning);
  		  }
		};
		toolbar.Controls.Add(btnHome);

        addressBar.Location = new Point(250, 4); addressBar.Height = 26; addressBar.Font = new Font("Segoe UI", 10f); addressBar.BorderStyle = BorderStyle.FixedSingle;
        toolbar.Controls.Add(addressBar);
        var btnOpen = new Button() { Text = "\u2026", Width = 30 };
        styleBtn(btnOpen);
        btnOpen.Click += (s, e) =>
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "支持的文件 (*.pdf;*.docx;*.doc;*.xlsx;*.xls;*.html;*.htm;*.txt;*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.mp4)|*.pdf;*.docx;*.doc;*.xlsx;*.xls;*.html;*.htm;*.txt;*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.mp4|所有文件 (*.*)|*.*";
            dlg.Title = "index - 打开本地文件";
            if (dlg.ShowDialog() != DialogResult.OK) return;
            if (wv.CoreWebView2 == null) return;
            string filePath = dlg.FileName;
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".docx" || ext == ".doc" || ext == ".xlsx" || ext == ".xls")
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true });
                }
                catch
                {
                    MessageBox.Show("未检测到 Office/WPS，无法打开该文档。", "index", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                string uri = new Uri(filePath).AbsoluteUri;
                ext = Path.GetExtension(filePath).ToLowerInvariant();
                      if (ext == ".mp4")
                      {
                          string htmlContent = string.Format(@"
                      <!DOCTYPE html>
                      <html>
                      <head>
                      <meta charset='utf‑8'>
                      <style>
                      body {{ margin:0; padding:0; background:#000; }}
                      video {{ width:100%; height:100%; object‑fit:contain; }}
                      </style>
                      </head>
                      <body>
                      <video src=""{0}"" loop muted controls autoplay playsinline></video>
                      </body>
                      </html>", uri);

                      // 写入系统临时文件夹
                      string tempHtml = Path.Combine(Path.GetTempPath(), "wv2_temp_playmp4.html");
                      File.WriteAllText(tempHtml, htmlContent, System.Text.Encoding.UTF8);
                      string tempUri = new Uri(tempHtml).AbsoluteUri;
                      wv.CoreWebView2.Navigate(tempUri);
                      }
                else
                {
                    //其它文件沿用原来逻辑
                    wv.CoreWebView2.Navigate(uri);
                }
                toolbar.Visible = false;
                wv.Focus();
            }
        };
        toolbar.Controls.Add(btnOpen);
        var btnMin = new Button() { Text = "\u2500", Width = 30 }; styleBtn(btnMin);
        btnMin.Click += (s, e) => { form.WindowState = FormWindowState.Minimized; };
        toolbar.Controls.Add(btnMin);

        var btnMax = new Button() { Text = "\u25A1", Width = 30 }; styleBtn(btnMax);
        btnMax.Click += (s, e) =>
        {
            if (form.WindowState == FormWindowState.Maximized) { form.WindowState = FormWindowState.Normal; btnMax.Text = "\u25A1"; }
            else { form.WindowState = FormWindowState.Maximized; btnMax.Text = "\u2750"; }
        };
        toolbar.Controls.Add(btnMax);
        var btnClose = new Button() { Text = "\u00D7", Width = 30 }; styleBtn(btnClose);
        btnClose.Click += (s, e) => { form.Close(); };
        toolbar.Controls.Add(btnClose);
        Action doLayout = () =>
        {
            int right = toolbar.ClientSize.Width - 4;
            btnClose.Location = new Point(right - btnClose.Width, 4);
            btnMax.Location = new Point(right - btnClose.Width - btnMax.Width - 2, 4);
            btnMin.Location = new Point(right - btnClose.Width - btnMax.Width - btnMin.Width - 4, 4);
            btnOpen.Location = new Point(btnMin.Location.X - btnOpen.Width - 4, 4);
            addressBar.Width = btnOpen.Location.X - addressBar.Location.X - 6;
        };
        toolbar.Resize += (s, e) => doLayout();
        doLayout();
        addressBar.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                try { string navWav = Path.Combine(baseDir, "nav.wav"); if (File.Exists(navWav)) new SoundPlayer(navWav).Play(); } catch { }
                string input = addressBar.Text.Trim();
                if (!input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    input = "https://" + input;
                if (wv.CoreWebView2 != null) wv.CoreWebView2.Navigate(input);
                wv.Focus(); toolbar.Visible = false;
            }
            else if (e.KeyCode == Keys.Escape) { wv.Focus(); toolbar.Visible = false; }
        };
        addressBar.AllowDrop = true;
        addressBar.DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Link; };
        addressBar.DragDrop += (s, e) =>
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0)
            {
                string path = new Uri(files[0]).AbsoluteUri;
                addressBar.Text = path;
                if (wv.CoreWebView2 != null) wv.CoreWebView2.Navigate(path);
                toolbar.Visible = false;
                wv.Focus();
            }
        };
        btnFav.Click += (s, e) =>
        {
            var menu = new ContextMenuStrip();
            menu.Font = new Font("Segoe UI", 9f);
            foreach (var folder in _bookmarkData)
            {
                var catItem = new ToolStripMenuItem(folder.CategoryName);
                foreach (var bm in folder.Items)
                {
                    var linkItm = new ToolStripMenuItem(bm.Title);
                    linkItm.Click += (mi, me) => { if (wv.CoreWebView2 != null) wv.CoreWebView2.Navigate(bm.Url); };
                    catItem.DropDownItems.Add(linkItm);
                }
                menu.Items.Add(catItem);
            }
            menu.Items.Add(new ToolStripSeparator());
            var addCur = new ToolStripMenuItem("收藏当前页");
            addCur.Click += (mi, me) =>
            {
                if (wv.CoreWebView2 == null || string.IsNullOrEmpty(wv.CoreWebView2.Source)) return;
                string titleText = wv.CoreWebView2.DocumentTitle;
                if (string.IsNullOrWhiteSpace(titleText)) titleText = "未命名";
                string selCat;
                if (ShowAddBookmarkDialog(titleText, wv.CoreWebView2.Source, out selCat))
                {
                    var fd = _bookmarkData.First(folder => folder.CategoryName == selCat);
                    BookmarkItem bmItem = new BookmarkItem();
                    bmItem.Title = titleText;
                    bmItem.Url = wv.CoreWebView2.Source;
                    fd.Items.Add(bmItem);
                    SaveBookmarkData();
                    MessageBox.Show("已保存到分类：" + selCat, "index", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            menu.Items.Add(addCur);
            var mgrItem = new ToolStripMenuItem("管理收藏夹…");
            mgrItem.Click += (mi, me) =>
            {
                ShowBookmarkManagerDialog();
            };
            menu.Items.Add(mgrItem);
            menu.Show(btnFav, new Point(0, btnFav.Height));
        };
        bool useResizeEdges = (mode == "borderless");
        Panel edgeTop = null, edgeBottom = null, edgeLeft = null, edgeRight = null;
        if (useResizeEdges)
        {
            edgeTop = new Panel() { Height = 6, Dock = DockStyle.Top, Cursor = Cursors.SizeNS, BackColor = Color.Transparent, Visible = false };
            edgeBottom = new Panel() { Height = 6, Dock = DockStyle.Bottom, Cursor = Cursors.SizeNS, BackColor = Color.Transparent, Visible = false };
            edgeLeft = new Panel() { Width = 6, Dock = DockStyle.Left, Cursor = Cursors.SizeWE, BackColor = Color.Transparent, Visible = false };
            edgeRight = new Panel() { Width = 6, Dock = DockStyle.Right, Cursor = Cursors.SizeWE, BackColor = Color.Transparent, Visible = false };
            edgeTop.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { Native.ReleaseCapture(); Native.SendMessage(form.Handle, 0xA1, 12, 0); } };
            edgeBottom.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { Native.ReleaseCapture(); Native.SendMessage(form.Handle, 0xA1, 15, 0); } };
            edgeLeft.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { Native.ReleaseCapture(); Native.SendMessage(form.Handle, 0xA1, 10, 0); } };
            edgeRight.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { Native.ReleaseCapture(); Native.SendMessage(form.Handle, 0xA1, 11, 0); } };
            form.Controls.Add(edgeTop); form.Controls.Add(edgeBottom); form.Controls.Add(edgeLeft); form.Controls.Add(edgeRight);
            toolbar.VisibleChanged += (s, e) =>
            {
                bool v = toolbar.Visible;
                if (edgeTop != null) edgeTop.Visible = v;
                if (edgeBottom != null) edgeBottom.Visible = v;
                if (edgeLeft != null) edgeLeft.Visible = v;
                if (edgeRight != null) edgeRight.Visible = v;
            };
        }
        form.Controls.Add(wv);
        wv.MouseWheel += (s, e) =>
        {
            if (Control.ModifierKeys == Keys.Control && wv.CoreWebView2 != null)
            {
                double z = Math.Round(wv.ZoomFactor + (e.Delta > 0 ? 0.1 : -0.1), 1);
                wv.ZoomFactor = Math.Max(0.5, Math.Min(3.0, z));
            }
        };

        switch (mode)
        {
            case "max":
                form.WindowState = FormWindowState.Maximized; form.StartPosition = FormStartPosition.CenterScreen; break;
            case "full":
                form.FormBorderStyle = FormBorderStyle.None; form.StartPosition = FormStartPosition.CenterScreen; form.Bounds = Screen.PrimaryScreen.Bounds; form.TopMost = true; break;
            case "kiosk":
                form.FormBorderStyle = FormBorderStyle.None; form.StartPosition = FormStartPosition.CenterScreen; form.Bounds = Screen.PrimaryScreen.Bounds; break;
            case "fixed":
                form.FormBorderStyle = FormBorderStyle.FixedSingle; form.MaximizeBox = false; form.Width = w; form.Height = h; form.StartPosition = FormStartPosition.CenterScreen; break;
            case "borderless":
                form.FormBorderStyle = FormBorderStyle.None; form.Width = w; form.Height = h; form.StartPosition = FormStartPosition.CenterScreen; break;
            case "remember":
                form.Width = w; form.Height = h; form.StartPosition = FormStartPosition.CenterScreen; break;
            default:
                var stateFile = Path.Combine(baseDir, "state.txt");
                if (File.Exists(stateFile))
                {
                    var lines = File.ReadAllLines(stateFile);
                    if (lines.Length >= 4)
                    {
                        int sw, sh, sx, sy;
                        if (int.TryParse(lines[0], out sw) && int.TryParse(lines[1], out sh)) { form.Width = sw; form.Height = sh; }
                        if (int.TryParse(lines[2], out sx) && int.TryParse(lines[3], out sy)) { form.StartPosition = FormStartPosition.Manual; form.Location = new Point(sx, sy); }
                    }
                }
                else { form.Width = w; form.Height = h; form.StartPosition = FormStartPosition.CenterScreen; }
                form.FormClosing += (s2, e2) =>
                {
                    string appDir = Application.StartupPath;
                    string sf = Path.Combine(appDir, "state.txt");
                    int ww = form.Width;
                    int hh = form.Height;
                    int xx, yy;
                    if (_dockSide != DockHideSide.None && !_savedOrigBounds.IsEmpty)
                    {
                        xx = _savedOrigBounds.X;
                        yy = _savedOrigBounds.Y;
                    }
                    else
                    {
                        xx = form.Location.X;
                        yy = form.Location.Y;
                    }
                    File.WriteAllLines(sf, new string[] { ww.ToString(), hh.ToString(), xx.ToString(), yy.ToString() });
                    if (monitorTimer != null) monitorTimer.Stop();
                    if (leaveTimer != null) leaveTimer.Stop();
                    Native.UnregisterHotKey(form.Handle, Native.HK_ESC);
                    Native.UnregisterHotKey(form.Handle, Native.HK_CTRL_L);
                    Native.UnregisterHotKey(form.Handle, Native.HK_F5);
                    Native.UnregisterHotKey(form.Handle, Native.HK_DOCKTOGGLE);
                    Native.UnregisterHotKey(form.Handle, Native.HK_CLICKTHROUGH);
                    Native.UnregisterHotKey(form.Handle, Native.HK_GRAYSCALE);
                    Native.UnregisterHotKey(form.Handle, Native.HK_OPACITY);
                };
                break;
        }
        if (x >= 0 && y >= 0) { form.StartPosition = FormStartPosition.Manual; form.Location = new Point(x, y); }
        bool dockHideEnable = !(mode == "max" || mode == "full" || mode == "kiosk");
        int HOT_EDGE = (mode == "borderless") ? 6 : 16;

        Action UpdateDockButtonIcon = () =>
        {
            if (_dockSide == DockHideSide.None)
                btnDockToggle.Text = "\u21C4";
            else
                btnDockToggle.Text = "\u25C9";
        };
        Action ExitDockByButton = () =>
        {
            leaveTimer.Stop();
            monitorTimer.Stop();
            if (_dockSide == DockHideSide.None) return;
            _dockSide = DockHideSide.None;
            if (!_savedOrigBounds.IsEmpty) form.Bounds = _savedOrigBounds;
            _savedOrigBounds = Rectangle.Empty;
            UpdateDockButtonIcon();
        };
        Action ExitDockByClick = () =>
        {
            leaveTimer.Stop();
            monitorTimer.Stop();
            if (_dockSide == DockHideSide.None) return;
            _dockSide = DockHideSide.None;
            _savedOrigBounds = Rectangle.Empty;
            UpdateDockButtonIcon();
        };
        DoDockToggle = () =>
        {
            if (!dockHideEnable || form.WindowState != FormWindowState.Normal) return;
            if (_dockSide != DockHideSide.None)
            {
                ExitDockByButton();
                return;
            }
            _savedOrigBounds = form.Bounds;
            Rectangle scr = Screen.FromControl(form).WorkingArea;
            Rectangle cur = form.Bounds;

            int distLeft = Math.Abs(cur.Left - scr.Left);
            int distRight = Math.Abs(cur.Right - scr.Right);
            if (distLeft < distRight)
            {
                form.Bounds = new Rectangle(scr.Left, cur.Top, cur.Width, cur.Height);
                _dockSide = DockHideSide.Left;
                form.Left = scr.Left - form.Width + HOT_EDGE;
            }
            else
            {
                form.Bounds = new Rectangle(scr.Right - cur.Width, cur.Top, cur.Width, cur.Height);
                _dockSide = DockHideSide.Right;
                form.Left = scr.Right - HOT_EDGE;
            }
            monitorTimer.Start();
            UpdateDockButtonIcon();
        };
        formRightMenu = new ContextMenuStrip();
        ToolStripMenuItem miDockToggleMenu = new ToolStripMenuItem("贴边隐藏切换 Ctrl+Shift+T");
        miDockToggleMenu.Click += (s, e) => { DoDockToggle(); };
        ToolStripMenuItem miToggleToolbar = new ToolStripMenuItem("显隐地址栏 Ctrl+L");
        miToggleToolbar.Click += (s, e) => { toolbar.Visible = !toolbar.Visible; };
        ToolStripMenuItem miAddFavMenu = new ToolStripMenuItem("收藏当前页");
        miAddFavMenu.Click += (o, ev) =>
        {
            if (wv.CoreWebView2 == null) return;
            string titleText = wv.CoreWebView2.DocumentTitle;
            if (string.IsNullOrWhiteSpace(titleText)) titleText = "未命名";
            string selCat;
            if (ShowAddBookmarkDialog(titleText, wv.CoreWebView2.Source, out selCat))
            {
                var fd = _bookmarkData.First(folder => folder.CategoryName == selCat);
                BookmarkItem bmItem = new BookmarkItem();
                bmItem.Title = titleText;
                bmItem.Url = wv.CoreWebView2.Source;
                fd.Items.Add(bmItem);
                SaveBookmarkData();
                MessageBox.Show("已保存到分类：" + selCat, "index", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        ToolStripMenuItem miExitMenu = new ToolStripMenuItem("退出 ESC");
        miExitMenu.Click += (s, e) => { form.Close(); };
        formRightMenu.Items.Add(miDockToggleMenu);
        formRightMenu.Items.Add(miToggleToolbar);
        formRightMenu.Items.Add(miAddFavMenu);
        formRightMenu.Items.Add(new ToolStripSeparator());
        formRightMenu.Items.Add(miExitMenu);
        form.ContextMenuStrip = formRightMenu;
        btnDockToggle.Click += (s, e) => { DoDockToggle(); };
        form.SizeChanged += (s, e) => { if (form.WindowState != FormWindowState.Normal) ExitDockByButton(); };
        leaveTimer.Tick += (s, e) =>
        {
            leaveTimer.Stop();
            if (_dockSide == DockHideSide.None || form.WindowState != FormWindowState.Normal) return;
            var scr = Screen.FromControl(form).WorkingArea;
            if (_dockSide == DockHideSide.Left) form.Left = scr.Left - form.Width + HOT_EDGE;
            else if (_dockSide == DockHideSide.Right) form.Left = scr.Right - HOT_EDGE;
        };
        monitorTimer.Tick += (s, e) =>
        {
            if (_dockSide == DockHideSide.None || !dockHideEnable || form.WindowState != FormWindowState.Normal)
            {
                leaveTimer.Stop();
                return;
            }
            Point mp = Cursor.Position;
            Rectangle scrRect = Screen.FromControl(form).WorkingArea;
            bool mouseInForm = form.Bounds.Contains(mp);
            bool hotHit = false;
            if (_dockSide == DockHideSide.Left)
            {
                hotHit = mp.X >= scrRect.Left && mp.X <= scrRect.Left + HOT_EDGE
                    && mp.Y >= scrRect.Top && mp.Y <= scrRect.Bottom;
            }
            else if (_dockSide == DockHideSide.Right)
            {
                hotHit = mp.X >= scrRect.Right - HOT_EDGE && mp.X <= scrRect.Right
                    && mp.Y >= scrRect.Top && mp.Y <= scrRect.Bottom;
            }
            if (hotHit || mouseInForm)
            {
                leaveTimer.Stop();
                if (_dockSide == DockHideSide.Left) form.Left = scrRect.Left;
                else if (_dockSide == DockHideSide.Right) form.Left = scrRect.Right - form.Width;
            }
            else
            {
                if (!leaveTimer.Enabled) leaveTimer.Start();
            }
        };
        form.Load += (s, e) =>
        {
            UpdateDockButtonIcon();
            if (mode == "full" || mode == "kiosk" || mode == "fixed" || mode == "borderless")
            { Native.RegisterHotKey(form.Handle, Native.HK_ESC, 0, Native.VK_ESCAPE); Application.AddMessageFilter(new EscFilter(form)); }
            Native.RegisterHotKey(form.Handle, Native.HK_CTRL_L, Native.MOD_CTRL, Native.VK_L);
            Application.AddMessageFilter(new CtrlLFilter(toolbar, addressBar, wv, btnBack));
            Native.RegisterHotKey(form.Handle, Native.HK_F5, 0, Native.VK_F5);
            Application.AddMessageFilter(new F5Filter(wv));
            Native.RegisterHotKey(form.Handle, Native.HK_DOCKTOGGLE, Native.MOD_CTRL | Native.MOD_SHIFT, Native.VK_T);
            Application.AddMessageFilter(new DockToggleFilter(DoDockToggle));
            Native.RegisterHotKey(form.Handle, Native.HK_CLICKTHROUGH, Native.MOD_CTRL, Native.VK_F9);
            Application.AddMessageFilter(new ClickThroughFilter());
            Native.RegisterHotKey(form.Handle, Native.HK_GRAYSCALE, Native.MOD_ALT, Native.VK_F7);
            Application.AddMessageFilter(new GrayScaleFilter(wv));
            Native.RegisterHotKey(form.Handle, Native.HK_OPACITY, Native.MOD_ALT, Native.VK_F8);
            Application.AddMessageFilter(new OpacityHotkeyFilter(()=>
                {
                    _opacityIndex++;
                    if (_opacityIndex >= _opacityLevels.Length) _opacityIndex = 0;
                    mainForm.Opacity = _opacityLevels[_opacityIndex];
                }));
        };
        System.Windows.Forms.Timer refreshTimer = null;
        if (autoRefreshSec > 0)
        {
            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Interval = autoRefreshSec * 1000;
            refreshTimer.Tick += (s, e) => { if (wv.CoreWebView2 != null) wv.CoreWebView2.Reload(); };
        }
        var envTask = CoreWebView2Environment.CreateAsync(null, cacheDir);
        envTask.Wait();
        wv.EnsureCoreWebView2Async(envTask.Result).ContinueWith(t =>
        {
            if (t.IsFaulted || wv.CoreWebView2 == null) { Environment.Exit(1); return; }
            wv.CoreWebView2.WebMessageReceived += Wv_WebMessageReceived;
            wv.CoreWebView2.Settings.AreDevToolsEnabled = true;
            wv.CoreWebView2.NewWindowRequested += (s3, e3) => { e3.Handled = true; wv.CoreWebView2.Navigate(e3.Uri); };
string injectFloatBtnScript = @"(function(){
/* 灰度三档全部在网页JS内部维护 */
    const graySteps = [0.0, 0.5, 1.0];
    let grayIndex = parseInt(sessionStorage.getItem(""uiGrayIndex"")) || 0;
    // 给C#调用的全局函数
    window.uiToggleGrayscale = function()
    {
        grayIndex = (grayIndex + 1) % graySteps.length;
        sessionStorage.setItem(""uiGrayIndex"", grayIndex);
        const val = graySteps[grayIndex];
        const fsEl = document.fullscreenElement;
        if(fsEl && fsEl.tagName === ""VIDEO"")
        {
            fsEl.style.filter = val === 0 ? '' : ('grayscale(' + val + ')');
            document.documentElement.style.filter = '';
        }
        else
        {
            if(val === 0)
            {
                document.documentElement.style.filter = '';
            }
            else
            {
                document.documentElement.style.filter = 'grayscale(' + val + ')';
            }
            if(fsEl == null)
            {
                const allVideo = document.querySelectorAll('video');
                allVideo.forEach(v=>v.style.filter='');
            }
        }
    };
    document.addEventListener('fullscreenchange',function(){
        if(!document.fullscreenElement){
            document.querySelectorAll('video').forEach(v=>v.style.filter='');
        }
    });
    const floatBtnId=""my_host_float_btn"";
    let oldBtn=document.getElementById(floatBtnId);
    if(oldBtn && oldBtn.parentNode){
        oldBtn.parentNode.removeChild(oldBtn);
    }
    let oldMenu=document.querySelector("".host_float_menu"");
    if(oldMenu && oldMenu.parentNode){
        oldMenu.parentNode.removeChild(oldMenu);
    }
    if(!document.body){
        return;
    }
    var style=document.createElement('style');
style.textContent=
'::-webkit-scrollbar {width:4px;height:4px;}' +
'::-webkit-scrollbar-thumb {background:#bbbbbb;border-radius:3px;}' +
'::-webkit-scrollbar-track {background:transparent;}' +
'.host_float_menu{position:fixed;bottom:74px;right:24px;background:#ffffff !important;color:#000000 !important;box-shadow:0 2px 12px rgba(0,0,0,0.18);border-radius:8px;z-index:99999998;overflow:hidden;min-width:130px;}' +
'.host_float_menu div{padding:9px 14px;cursor:pointer;font-size:13px;user-select:none;background:#ffffff !important;color:#000000 !important;}' +
'.host_float_menu div:hover{background:#f0f2f5 !important;color:#000000 !important;}' +
'#my_host_float_btn{opacity:0.35 !important;transition:opacity 0.22s ease !important;}' +
'#my_host_float_btn:hover{opacity:1.0 !important;}';
    document.head.appendChild(style);
    let menuDom=null;
    function closeFloatMenu(){
        if(menuDom&&menuDom.parentNode){
            menuDom.parentNode.removeChild(menuDom);
        }
        menuDom=null;
    }
    function buildFloatMenu(){
        closeFloatMenu();
        menuDom=document.createElement('div');
        menuDom.className=""host_float_menu"";
        var itemTop=document.createElement('div');
        itemTop.innerText=""⏫ 回到页首"";
        itemTop.onclick=function(e){
            e.stopPropagation();
            window.scrollTo({top:0,behavior:'smooth'});
            closeFloatMenu();
        };
        var itemBot=document.createElement('div');
        itemBot.innerText=""⏬️ 滚到页尾"";
        itemBot.onclick=function(e){
            e.stopPropagation();
            window.scrollTo({top:document.documentElement.scrollHeight,behavior:'smooth'});
            closeFloatMenu();
        };
        var itemBookmark=document.createElement('div');
        itemBookmark.innerText=""⭐ 收藏此页"";
        itemBookmark.onclick=function(e){
            e.stopPropagation();
            chrome.webview.postMessage(""addBookmark"");
            closeFloatMenu();
        };
        var itemToggleAddr=document.createElement('div');
        itemToggleAddr.innerText=""✅ 显隐网址""+""\n""+""Ctrl+L"";
        itemToggleAddr.onclick=function(e){
            e.stopPropagation();
            chrome.webview.postMessage(""toggleAddressBar"");
            closeFloatMenu();
        };
        var itemToggleHide=document.createElement('div');
        itemToggleHide.innerText=""🔲 贴边显隐""+""\n""+""Ctrl+Shift+T"";
        itemToggleHide.onclick=function(e){
            e.stopPropagation();
            chrome.webview.postMessage(""toggleDockHide"");
            closeFloatMenu();
        };
        var itemGrayscale=document.createElement('div');
        itemGrayscale.innerText=""☪️ 灰度去色""+""\n""+""Alt+F7"";
        itemGrayscale.onclick=function(e){
            e.stopPropagation();
            grayIndex=(grayIndex+1)%graySteps.length;
            sessionStorage.setItem(""uiGrayIndex"", grayIndex);
            const val=graySteps[grayIndex];
            const fsEl = document.fullscreenElement;
            if(fsEl && fsEl.tagName === ""VIDEO"")
            {
                fsEl.style.filter = val === 0 ? '' : ('grayscale(' + val + ')');
            }
            else
            {
                if(val===0)
                {
                    document.documentElement.style.filter='';
                }
                else
                {
                    document.documentElement.style.filter='grayscale('+val+')';
                }
            }
            closeFloatMenu();
        };
        var itemOpacity=document.createElement('div');
        itemOpacity.innerText=""👓 窗口透明""+""\n""+""Alt+F8"";
        itemOpacity.onclick=function(e){
            e.stopPropagation();
            chrome.webview.postMessage(""toggleOpacity"");
            closeFloatMenu();
        };
        var itemTransparent=document.createElement('div');
        itemTransparent.innerText=""📌 鼠标穿透""+""\n""+""Ctrl+F9"";
        itemTransparent.onclick=function(e){
            e.stopPropagation();
            chrome.webview.postMessage(""toggleClickThrough"");
            closeFloatMenu();
        };
        var itemOpenExt=document.createElement('div');
        itemOpenExt.innerText=""🌐 外部浏览"";
        itemOpenExt.onclick=function(e){
            e.stopPropagation();
            chrome.webview.postMessage(""openExternalBrowser"");
            closeFloatMenu();
        };
        var itemExit=document.createElement('div');
        itemExit.innerText=""❌ 退出 Esc"";
        itemExit.onclick=function(e){
            e.stopPropagation();
            chrome.webview.postMessage(""requestExit"");
            closeFloatMenu();
        };
        menuDom.appendChild(itemTop);
        menuDom.appendChild(itemBot);
        menuDom.appendChild(itemBookmark);
        menuDom.appendChild(itemToggleAddr);
        menuDom.appendChild(itemToggleHide);
        menuDom.appendChild(itemGrayscale);
        menuDom.appendChild(itemOpacity);
        menuDom.appendChild(itemTransparent);
        menuDom.appendChild(itemOpenExt);
        menuDom.appendChild(itemExit);
        document.body.appendChild(menuDom);
    }
    document.addEventListener(""click"",function(){
        closeFloatMenu();
    });
    const btn=document.createElement('div');
    btn.id=floatBtnId;
    btn.style.position='fixed';
    btn.style.bottom='24px';
    btn.style.right='24px';
    btn.style.width='44px';
    btn.style.height='44px';
    btn.style.borderRadius='50%';
    btn.style.background='#2b78e4';
    btn.style.color='white';
    btn.style.display='flex';
    btn.style.alignItems='center';
    btn.style.justifyContent='center';
    btn.style.zIndex='99999999';
    btn.style.cursor='pointer';
    btn.style.boxShadow='0 2px 8px rgba(0,0,0,0.25)';
    btn.innerText=""⋮"";
    btn.addEventListener('click',function(e){
        e.stopPropagation();
        buildFloatMenu();
    });
    document.body.appendChild(btn);
(function restoreGray(){
    const saved = parseInt(sessionStorage.getItem(""uiGrayIndex"")) || 0;
    if(saved > 0)
    {
        const val = graySteps[saved];
        if(val > 0)
        {
            document.documentElement.style.filter = 'grayscale(' + val + ')';
        }
    }
})();
})();";

wv.CoreWebView2.NavigationStarting += (s4, e4) =>
{
    form.BeginInvoke(new Action(() => pb.Visible = true));
    string navUrl = e4.Uri;
    string navExt = "";
    try
    {
        Uri u = new Uri(navUrl);
        navExt = Path.GetExtension(u.AbsolutePath).ToLowerInvariant();
    }
    catch { }
    if (navExt == ".docx" || navExt == ".doc" || navExt == ".xlsx" || navExt == ".xls")
    {
        e4.Cancel = true;
        form.BeginInvoke(new Action(() =>
        {
            try
            {
                string scheme;
                if (navExt.StartsWith(".doc"))
                {
                    scheme = string.Format("ms-word:ofe|u|{0}", navUrl);
                }
                else
                {
                    scheme = string.Format("ms-excel:ofe|u|{0}", navUrl);
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(scheme) { UseShellExecute = true });
            }
            catch
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(navUrl) { UseShellExecute = true });
            }
        }));
    }
};
            wv.CoreWebView2.NavigationCompleted += async (s5, e5) =>
            {
                form.BeginInvoke(new Action(() => pb.Visible = false));
                await Task.Delay(200);
                await wv.CoreWebView2.ExecuteScriptAsync(injectFloatBtnScript);
            };
            wv.CoreWebView2.DocumentTitleChanged += (s6, e6) =>
            {
                form.BeginInvoke(new Action(() =>
                {
                    form.Text = string.IsNullOrEmpty(wv.CoreWebView2.DocumentTitle) ? "index" : wv.CoreWebView2.DocumentTitle + " - index";
                    addressBar.Text = wv.CoreWebView2.Source ?? "";
                    btnBack.Enabled = wv.CoreWebView2.CanGoBack;
                    btnForward.Enabled = wv.CoreWebView2.CanGoForward;
                }));
            };
            btnBack.Click += (s1, e1) => { if (wv.CoreWebView2 != null && wv.CoreWebView2.CanGoBack) wv.CoreWebView2.GoBack(); };
            btnForward.Click += (s2, e2) => { if (wv.CoreWebView2 != null && wv.CoreWebView2.CanGoForward) wv.CoreWebView2.GoForward(); };
            if (refreshTimer != null) form.BeginInvoke(new Action(() => refreshTimer.Start()));
            form.BeginInvoke(new Action(() => { wv.CoreWebView2.Navigate(url); }));
        }, TaskScheduler.FromCurrentSynchronizationContext());
        Application.Run(form);
    }
}
