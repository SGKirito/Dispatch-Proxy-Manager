using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

public class DispatchGUI : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [DllImport("wininet.dll")]
    private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

    private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
    private const int INTERNET_OPTION_REFRESH = 37;

    private TextBox txtIp, txtPort, txtProxy;
    private RichTextBox txtConsole;
    private DataGridView dgvAdapters;
    private Button btnStart, btnStop, btnRefresh, btnBrowse;
    private Process dispatchProcess;
    private Process proxyProcess;

    private string extractedDispatchPath = "dispatch"; // Defaults to system PATH if not embedded

    // Modern Dark Theme Palette
    private Color bgApp = Color.FromArgb(32, 32, 32);
    private Color bgPanel = Color.FromArgb(40, 40, 40);
    private Color bgInput = Color.FromArgb(24, 24, 24);
    private Color textPrimary = Color.FromArgb(240, 240, 240);
    private Color textSecondary = Color.FromArgb(160, 160, 160);
    private Color borderLine = Color.FromArgb(70, 70, 70);
    
    // Accent Colors
    private Color accentBlue = Color.FromArgb(0, 120, 212);
    private Color accentBlueHover = Color.FromArgb(20, 140, 232);
    private Color accentBlueActive = Color.FromArgb(0, 100, 192);
    private Color accentRed = Color.FromArgb(200, 50, 50);
    private Color accentRedHover = Color.FromArgb(220, 70, 70);
    private Color accentRedActive = Color.FromArgb(180, 30, 30);
    private Color btnDisabled = Color.FromArgb(50, 50, 50);

    // Console Colors
    private Color logSuccess = Color.FromArgb(78, 201, 176); 
    private Color logError = Color.FromArgb(244, 71, 71);    
    private Color logWarn = Color.FromArgb(206, 145, 120);   
    private Color logDefault = Color.FromArgb(212, 212, 212);

    public DispatchGUI()
    {
        Text = "Dispatch Proxy Manager";
        Size = new Size(1024, 576);
        MinimumSize = new Size(900, 500);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = bgApp;
        ForeColor = textPrimary;

        // --- Settings Section ---
        Label lblSettingsHeader = new Label { Text = "Proxy Settings", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI Semibold", 11) };
        Panel pnlSettings = CreatePanel(new Point(20, 45), new Size(460, 100));
        pnlSettings.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        
        Label lblIp = new Label { Text = "Listen IP:", Location = new Point(15, 20), AutoSize = true, ForeColor = textSecondary };
        txtIp = CreateInputBox("127.0.0.1", new Point(85, 17), 130);
        
        Label lblPort = new Label { Text = "Listen Port:", Location = new Point(230, 20), AutoSize = true, ForeColor = textSecondary };
        txtPort = CreateInputBox("1080", new Point(315, 17), 130);

        Label lblProxy = new Label { Text = "Proxy Path:", Location = new Point(15, 60), AutoSize = true, ForeColor = textSecondary };
        txtProxy = CreateInputBox("", new Point(95, 57), 265);
        
        btnBrowse = CreateFlatButton("Browse", new Point(370, 56), new Size(75, 26), bgPanel, borderLine);
        btnBrowse.Click += BrowseProxy;

        pnlSettings.Controls.AddRange(new Control[] { lblIp, txtIp, lblPort, txtPort, lblProxy, txtProxy, btnBrowse });

        // --- Adapters Section ---
        Label lblAdaptersHeader = new Label { Text = "Network Adapters", Location = new Point(20, 160), AutoSize = true, Font = new Font("Segoe UI Semibold", 11) };
        Panel pnlAdapters = CreatePanel(new Point(20, 190), new Size(460, 290));
        pnlAdapters.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        
        InitializeDataGridView();
        dgvAdapters.Location = new Point(15, 15);
        dgvAdapters.Size = new Size(430, 220);
        dgvAdapters.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        
        btnRefresh = CreateFlatButton("Refresh", new Point(350, 245), new Size(95, 30), bgPanel, borderLine);
        btnRefresh.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnRefresh.Click += (s, e) => LoadAdapters();

        pnlAdapters.Controls.AddRange(new Control[] { dgvAdapters, btnRefresh });

        // --- Controls Section ---
        btnStart = CreateBeautifulButton("Start Dispatch", new Point(20, 495), new Size(220, 45), accentBlue, accentBlueHover, accentBlueActive);
        btnStart.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        btnStart.Click += Start;

        btnStop = CreateBeautifulButton("Stop Dispatch", new Point(260, 495), new Size(220, 45), accentRed, accentRedHover, accentRedActive);
        btnStop.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        btnStop.Enabled = false;
        btnStop.Click += Stop;

        // --- Console Output Section ---
        Label lblConsoleHeader = new Label { Text = "Output Console", Location = new Point(500, 15), AutoSize = true, Font = new Font("Segoe UI Semibold", 11) };
        lblConsoleHeader.Anchor = AnchorStyles.Top | AnchorStyles.Left;

        Panel pnlConsole = CreatePanel(new Point(500, 45), new Size(490, 495));
        pnlConsole.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        pnlConsole.BackColor = bgInput; 
        pnlConsole.Padding = new Padding(12);

        txtConsole = new RichTextBox
        {
            BackColor = bgInput, ForeColor = logDefault,
            Font = new Font("Consolas", 10.5f),
            BorderStyle = BorderStyle.None, ReadOnly = true,
            Dock = DockStyle.Fill, ScrollBars = RichTextBoxScrollBars.Vertical
        };
        pnlConsole.Controls.Add(txtConsole);

        Controls.AddRange(new Control[] { lblSettingsHeader, pnlSettings, lblAdaptersHeader, pnlAdapters, btnStart, btnStop, lblConsoleHeader, pnlConsole });
        
        FormClosing += (s, e) => Stop(null, null);

        // Extract embedded dispatch.exe on startup
        PrepareDispatchExe();
        LoadAdapters();
    }

    private void PrepareDispatchExe()
    {
        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = null;
            
            // Search for dispatch.exe in the embedded resources
            foreach (string res in assembly.GetManifestResourceNames())
            {
                if (res.EndsWith("dispatch.exe", StringComparison.OrdinalIgnoreCase))
                {
                    resourceName = res;
                    break;
                }
            }

            if (resourceName != null)
            {
                string tempPath = Path.Combine(Path.GetTempPath(), "dispatch_embedded_engine.exe");
                
                // Write the embedded exe to the temp directory
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                using (FileStream fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fileStream);
                }
                
                extractedDispatchPath = tempPath;
                Log("[+] Embedded dispatch.exe found and extracted successfully.");
            }
            else
            {
                Log("[~] Embedded dispatch.exe not found. Falling back to global system PATH.");
            }
        }
        catch (Exception ex)
        {
            Log(string.Format("[-] Failed to extract embedded dispatch.exe: {0}", ex.Message));
        }
    }

    private void InitializeDataGridView()
    {
        dgvAdapters = new DataGridView {
            BackgroundColor = bgPanel, ForeColor = textPrimary, GridColor = borderLine, BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
            RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EnableHeadersVisualStyles = false,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            EditMode = DataGridViewEditMode.EditOnEnter
        };
        
        dgvAdapters.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { 
            BackColor = bgInput, ForeColor = textSecondary, SelectionBackColor = bgInput, 
            Font = new Font("Segoe UI", 9.5f), Alignment = DataGridViewContentAlignment.MiddleLeft 
        };
        dgvAdapters.DefaultCellStyle = new DataGridViewCellStyle { 
            BackColor = bgPanel, ForeColor = textPrimary, SelectionBackColor = Color.FromArgb(65, 65, 70), 
            SelectionForeColor = textPrimary, Alignment = DataGridViewContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10f)
        };
        dgvAdapters.RowTemplate.Height = 34;

        DataGridViewCheckBoxColumn colCheck = new DataGridViewCheckBoxColumn { HeaderText = "Use", Name = "Use", Width = 45, AutoSizeMode = DataGridViewAutoSizeColumnMode.None };
        DataGridViewTextBoxColumn colName = new DataGridViewTextBoxColumn { HeaderText = "Adapter Name", Name = "Adapter", ReadOnly = true };
        
        DataGridViewComboBoxColumn colMode = new DataGridViewComboBoxColumn { 
            HeaderText = "IP Mode", Name = "Mode", 
            FlatStyle = FlatStyle.Flat, Width = 130, 
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox 
        };
        colMode.Items.AddRange("IPv4 Only", "IPv4 & IPv6");

        dgvAdapters.Columns.AddRange(colCheck, colName, colMode);

        dgvAdapters.CurrentCellDirtyStateChanged += (s, e) => {
            if (dgvAdapters.IsCurrentCellDirty) dgvAdapters.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        dgvAdapters.CellClick += (s, e) => {
            if (e.RowIndex >= 0 && e.ColumnIndex == dgvAdapters.Columns["Mode"].Index)
            {
                dgvAdapters.BeginEdit(true);
                ComboBox cb = dgvAdapters.EditingControl as ComboBox;
                if (cb != null) cb.DroppedDown = true;
            }
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int useImmersiveDarkMode = 1;
            DwmSetWindowAttribute(this.Handle, 20, ref useImmersiveDarkMode, sizeof(int));
            int roundedCorners = 2; 
            DwmSetWindowAttribute(this.Handle, 33, ref roundedCorners, sizeof(int));
        }
        catch { }
    }

    private Panel CreatePanel(Point location, Size size)
    {
        return new Panel { Location = location, Size = size, BackColor = bgPanel, BorderStyle = BorderStyle.FixedSingle };
    }

    private TextBox CreateInputBox(string text, Point location, int width)
    {
        return new TextBox { Text = text, Location = location, Width = width, BackColor = bgInput, ForeColor = textPrimary, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10f) };
    }

    private Button CreateFlatButton(string text, Point location, Size size, Color backColor, Color borderColor)
    {
        Button btn = new Button { Text = text, Location = location, Size = size, BackColor = backColor, ForeColor = textPrimary, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9f) };
        btn.FlatAppearance.BorderColor = borderColor;
        btn.FlatAppearance.BorderSize = 1;
        return btn;
    }

    private Button CreateBeautifulButton(string text, Point location, Size size, Color normal, Color hover, Color active)
    {
        Button btn = new Button {
            Text = text, Location = location, Size = size, BackColor = normal, ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI Semibold", 11f), Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        
        btn.MouseEnter += (s, e) => { if (btn.Enabled) btn.BackColor = hover; };
        btn.MouseLeave += (s, e) => { if (btn.Enabled) btn.BackColor = normal; };
        btn.MouseDown += (s, e) => { if (btn.Enabled) btn.BackColor = active; };
        btn.MouseUp += (s, e) => { if (btn.Enabled) btn.BackColor = hover; };

        btn.EnabledChanged += (s, e) => {
            btn.BackColor = btn.Enabled ? normal : btnDisabled;
            btn.ForeColor = btn.Enabled ? Color.White : textSecondary;
        };
        return btn;
    }

    private void BrowseProxy(object sender, EventArgs e)
    {
        using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Executable Files (*.exe)|*.exe", Title = "Select Proxy Executable" })
        {
            if (ofd.ShowDialog() == DialogResult.OK) txtProxy.Text = ofd.FileName;
        }
    }

    private void LoadAdapters()
    {
        dgvAdapters.Rows.Clear();
        try
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    bool hasValidIp = false;
                    foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork || ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                        {
                            hasValidIp = true; break;
                        }
                    }
                    
                    if (hasValidIp)
                    {
                        dgvAdapters.Rows.Add(false, ni.Name, "IPv4 Only");
                    }
                }
            }
            if (dgvAdapters.Rows.Count == 0) Log("[-] No active adapters found.");
        }
        catch (Exception ex)
        {
            Log(string.Format("[-] Failed to load adapters: {0}", ex.Message));
        }
    }

    private void Start(object sender, EventArgs e)
    {
        List<string> selectedIps = new List<string>();

        foreach (DataGridViewRow row in dgvAdapters.Rows)
        {
            if (Convert.ToBoolean(row.Cells["Use"].Value))
            {
                string adapterName = row.Cells["Adapter"].Value.ToString();
                string mode = row.Cells["Mode"].Value.ToString();

                NetworkInterface matchedAdapter = null;
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.Name == adapterName) { matchedAdapter = ni; break; }
                }

                if (matchedAdapter != null)
                {
                    foreach (UnicastIPAddressInformation ip in matchedAdapter.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            selectedIps.Add(ip.Address.ToString());
                        }
                        else if (ip.Address.AddressFamily == AddressFamily.InterNetworkV6 && mode == "IPv4 & IPv6")
                        {
                            string ipv6 = ip.Address.ToString();
                            if (ipv6.Contains("%")) ipv6 = ipv6.Substring(0, ipv6.IndexOf("%"));
                            selectedIps.Add(ipv6);
                        }
                    }
                }
            }
        }

        if (selectedIps.Count == 0)
        {
            Log("[-] No valid IPs resolved. Please select an adapter and check its IP mode availability.");
            return;
        }

        string addrs = string.Join(" ", selectedIps.ToArray());
        string args = string.Format("start --ip {0} --port {1} {2}", txtIp.Text.Trim(), txtPort.Text.Trim(), addrs).Trim();
        
        dispatchProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = extractedDispatchPath, // <--- Using extracted embedded path
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        dispatchProcess.OutputDataReceived += (s, ev) => Log(ev.Data);
        dispatchProcess.ErrorDataReceived += (s, ev) => Log(ev.Data);

        try
        {
            dispatchProcess.Start();
            dispatchProcess.BeginOutputReadLine();
            dispatchProcess.BeginErrorReadLine();
            txtConsole.Clear();
            Log(string.Format("[+] Executing dispatch backend: {0}", args));
            
            ToggleUI(false);
            StartProxy();
        }
        catch (Exception ex)
        {
            Log(string.Format("[-] Error starting dispatch: {0}", ex.Message));
        }
    }

    private void StartProxy()
    {
        string proxyPath = txtProxy.Text.Trim();
        if (!string.IsNullOrEmpty(proxyPath) && File.Exists(proxyPath))
        {
            try
            {
                proxyProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = proxyPath,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Minimized,
                        WorkingDirectory = Path.GetDirectoryName(proxyPath)
                    }
                };
                proxyProcess.Start();
                Log("[+] Background proxy process started.");
            }
            catch (Exception ex)
            {
                Log(string.Format("[-] Failed to start proxy: {0}", ex.Message));
            }
        }
    }

    private void Stop(object sender, EventArgs e)
    {
        if (dispatchProcess != null && !dispatchProcess.HasExited)
        {
            try { dispatchProcess.Kill(); } catch { }
            Log("[+] Dispatch backend terminated.");
        }

        if (proxyProcess != null && !proxyProcess.HasExited)
        {
            Log("[~] Attempting to gracefully close the proxy...");
            try
            {
                bool gracefullyClosed = proxyProcess.CloseMainWindow();
                if (gracefullyClosed)
                {
                    proxyProcess.WaitForExit(3500);
                }

                if (!proxyProcess.HasExited)
                {
                    proxyProcess.Kill();
                    Log("[+] Proxy forcefully terminated (Did not close gracefully).");
                }
                else
                {
                    Log("[+] Proxy process closed gracefully.");
                }
            }
            catch 
            {
                try { proxyProcess.Kill(); } catch { } 
            }
        }

        ResetSystemProxyFailsafe();
        ToggleUI(true);
    }

    private void ResetSystemProxyFailsafe()
    {
        try
        {
            RegistryKey registry = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);
            if (registry != null)
            {
                registry.SetValue("ProxyEnable", 0);
                registry.Close();
                
                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
                
                Log("[+] System Proxy reset (Failsafe executed). Internet restored.");
            }
        }
        catch (Exception ex)
        {
            Log(string.Format("[-] Failed to run system proxy failsafe: {0}", ex.Message));
        }
    }

    private void ToggleUI(bool enable)
    {
        btnStart.Enabled = enable;
        btnStop.Enabled = !enable;
        dgvAdapters.Enabled = enable;
        btnRefresh.Enabled = enable;
        txtIp.Enabled = enable;
        txtPort.Enabled = enable;
        txtProxy.Enabled = enable;
        btnBrowse.Enabled = enable;
    }

    private void Log(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (InvokeRequired) 
        { 
            Invoke(new Action(() => Log(text))); 
            return; 
        }

        Color c = logDefault;
        string lowerText = text.ToLower();
        
        if (lowerText.Contains("[+]") || lowerText.Contains("start") || lowerText.Contains("listen") || lowerText.Contains("ready")) 
            c = logSuccess;
        else if (lowerText.Contains("[-]") || lowerText.Contains("error") || lowerText.Contains("fail")) 
            c = logError;
        else if (lowerText.Contains("warn") || lowerText.Contains("[~]")) 
            c = logWarn;

        txtConsole.SelectionStart = txtConsole.TextLength;
        txtConsole.SelectionLength = 0;
        txtConsole.SelectionColor = c;
        txtConsole.AppendText(text + Environment.NewLine);
        txtConsole.ScrollToCaret();
    }

    [STAThread]
    public static void Main()
    {
        SetProcessDPIAware();
        
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new DispatchGUI());
    }
}