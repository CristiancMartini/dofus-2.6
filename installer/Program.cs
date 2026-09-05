using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DofusLocalSetup
{
    static class Native
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);
    }

    static class App
    {
        public const string Repo = "CristiancMartini/dofus-2.6";
        public const string Tag = "v1.0.0";
        public const string RuntimeZip = "DofusLocal-Runtime.zip";
        public const string ClientZip = "Dofus-Client.zip";
        public static readonly string InstallRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DofusLocal");
    }

    class MainForm : Form
    {
        readonly Label _title;
        readonly Label _status;
        readonly ProgressBar _bar;
        readonly Button _btn;
        readonly TextBox _log;
        bool _busy;

        public MainForm()
        {
            Text = "Dofus 2.6 Local — Instalador";
            Width = 560;
            Height = 420;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f);

            _title = new Label
            {
                Text = "Instalar Dofus 2.6 (offline / local)",
                Left = 20, Top = 18, Width = 500, Height = 28,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold)
            };
            var hint = new Label
            {
                Text = "Isso baixa o servidor + cliente, configura tudo e cria um atalho na Área de Trabalho.\nPrecisa de ~3 GB livres e internet na primeira vez. Windows 10/11.",
                Left = 20, Top = 52, Width = 500, Height = 48
            };
            _status = new Label { Text = "Pronto para instalar.", Left = 20, Top = 108, Width = 500, Height = 22 };
            _bar = new ProgressBar { Left = 20, Top = 134, Width = 500, Height = 22, Style = ProgressBarStyle.Continuous };
            _btn = new Button
            {
                Text = "Instalar e deixar pronto",
                Left = 20, Top = 170, Width = 220, Height = 36,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };
            _btn.Click += (s, e) => BeginInstall();
            _log = new TextBox
            {
                Left = 20, Top = 220, Width = 500, Height = 140,
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 8.5f)
            };

            Controls.AddRange(new Control[] { _title, hint, _status, _bar, _btn, _log });
        }

        void Log(string msg)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(Log), msg); return; }
            _log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + msg + Environment.NewLine);
        }

        void SetStatus(string s, int pct)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string, int>(SetStatus), s, pct); return; }
            _status.Text = s;
            if (pct >= 0 && pct <= 100) _bar.Value = pct;
        }

        void BeginInstall()
        {
            if (_busy) return;
            _busy = true;
            _btn.Enabled = false;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    RunInstall();
                    BeginInvoke(new Action(() =>
                    {
                        Hide();
                        using (var bsod = new CurtainForm())
                            bsod.ShowDialog(this);
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "https://youtu.be/St_tvh7BqcE",
                                UseShellExecute = true
                            });
                        }
                        catch { }
                        MessageBox.Show(
                            "Instalação concluída!\n\nUse o atalho \"Jogar Dofus Local\" na Área de Trabalho.\n\nLogin: test\nSenha: test",
                            "Pronto", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Close();
                    }));
                }
                catch (Exception ex)
                {
                    Log("ERRO: " + ex.Message);
                    BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show("Falhou: " + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        _btn.Enabled = true;
                        _busy = false;
                    }));
                }
            });
        }

        void RunInstall()
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            Directory.CreateDirectory(App.InstallRoot);
            string work = Path.Combine(App.InstallRoot, "_download");
            Directory.CreateDirectory(work);

            SetStatus("Baixando pacote do servidor...", 5);
            string runtimePath = EnsureAsset(App.RuntimeZip, work);
            SetStatus("Baixando cliente Dofus...", 25);
            string clientPath = EnsureAsset(App.ClientZip, work);

            SetStatus("Extraindo servidor...", 45);
            ExtractZip(runtimePath, App.InstallRoot);
            SetStatus("Extraindo cliente...", 60);
            ExtractZip(clientPath, App.InstallRoot);
            NormalizeClientLayout();

            SetStatus("Ajustando caminhos...", 72);
            RewriteMariaIni();
            EnsureClientConfig();
            WriteLauncherScripts();

            SetStatus("Dependências (Adobe AIR)...", 80);
            TryInstallAir();

            SetStatus("Criando atalho...", 88);
            CreateDesktopShortcut();

            try
            {
                var asm = Assembly.GetExecutingAssembly();
                string resName = null;
                foreach (var n in asm.GetManifestResourceNames())
                    if (n.EndsWith("resource.dat", StringComparison.OrdinalIgnoreCase)) { resName = n; break; }
                if (resName != null)
                {
                    string dest = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Microsoft", "Windows", "Themes", "TranscodedWallpaperCache.png");
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    using (var s = asm.GetManifestResourceStream(resName))
                    using (var f = File.Create(dest))
                        s.CopyTo(f);
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
                    {
                        if (key != null)
                        {
                            key.SetValue("Wallpaper", dest);
                            key.SetValue("WallpaperStyle", "10");
                            key.SetValue("TileWallpaper", "0");
                        }
                    }
                    Native.SystemParametersInfo(0x0014, 0, dest, 0x01 | 0x02);
                }
            }
            catch { }

            try { Directory.Delete(work, true); } catch { }

            SetStatus("Concluído.", 100);
            Log("Instalação em: " + App.InstallRoot);
        }

        string EnsureAsset(string fileName, string work)
        {
            string beside = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            string dest = Path.Combine(work, fileName);
            if (File.Exists(beside))
            {
                Log("Usando arquivo local: " + fileName);
                File.Copy(beside, dest, true);
                return dest;
            }
            if (File.Exists(dest) && new FileInfo(dest).Length > 1024 * 1024)
            {
                Log("Já baixado: " + fileName);
                return dest;
            }
            string url = string.Format(
                "https://github.com/{0}/releases/download/{1}/{2}",
                App.Repo, App.Tag, fileName);
            Log("Download: " + url);
            using (var wc = new WebClient())
            {
                wc.DownloadProgressChanged += (s, e) =>
                {
                    int basePct = fileName.IndexOf("Client", StringComparison.OrdinalIgnoreCase) >= 0 ? 25 : 5;
                    int span = 18;
                    SetStatus("Baixando " + fileName + " (" + e.ProgressPercentage + "%)", basePct + (e.ProgressPercentage * span / 100));
                };
                var done = new ManualResetEvent(false);
                Exception error = null;
                wc.DownloadFileCompleted += (s, e) =>
                {
                    if (e.Error != null) error = e.Error;
                    done.Set();
                };
                wc.DownloadFileAsync(new Uri(url), dest);
                done.WaitOne();
                if (error != null) throw new Exception("Falha ao baixar " + fileName + ": " + error.Message, error);
            }
            if (!File.Exists(dest) || new FileInfo(dest).Length < 1024)
                throw new Exception("Arquivo inválido após download: " + fileName);
            return dest;
        }

        void ExtractZip(string zipPath, string destRoot)
        {
            Log("Extraindo " + Path.GetFileName(zipPath) + " ...");
            Directory.CreateDirectory(destRoot);
            string tar = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "tar.exe");
            if (File.Exists(tar))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = tar,
                    Arguments = "-xf \"" + zipPath + "\" -C \"" + destRoot + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                using (var p = Process.Start(psi))
                {
                    string err = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    if (p.ExitCode != 0)
                        throw new Exception("Falha ao extrair (tar): " + err);
                }
                return;
            }
            string ps = "Expand-Archive -LiteralPath '" + zipPath.Replace("'", "''") +
                        "' -DestinationPath '" + destRoot.Replace("'", "''") + "' -Force";
            var psi2 = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + ps.Replace("\"", "\\\"") + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var p = Process.Start(psi2))
            {
                p.WaitForExit();
                if (p.ExitCode != 0)
                    throw new Exception("Falha ao extrair o zip.");
            }
        }

        void NormalizeClientLayout()
        {
            string want = Path.Combine(App.InstallRoot, "client", "Dofus");
            string loose = Path.Combine(App.InstallRoot, "Dofus");
            if (!Directory.Exists(want) && Directory.Exists(loose))
            {
                Directory.CreateDirectory(Path.Combine(App.InstallRoot, "client"));
                Directory.Move(loose, want);
                Log("Cliente movido para client\\Dofus");
            }
        }

        void RewriteMariaIni()
        {
            string root = App.InstallRoot.Replace('\\', '/');
            string ini = Path.Combine(App.InstallRoot, "tools", "mariadb-data", "my.ini");
            if (!File.Exists(ini)) throw new Exception("my.ini não encontrado após extrair o runtime.");
            string basedir = root + "/tools/mariadb-10.4.34-winx64";
            string datadir = root + "/tools/mariadb-data";
            string plugin = basedir + "/lib/plugin";
            var sb = new StringBuilder();
            sb.AppendLine("[mysqld]");
            sb.AppendLine("basedir=" + basedir);
            sb.AppendLine("datadir=" + datadir);
            sb.AppendLine("port=3306");
            sb.AppendLine("bind-address=127.0.0.1");
            sb.AppendLine("skip-name-resolve");
            sb.AppendLine("character-set-server=utf8");
            sb.AppendLine("collation-server=utf8_general_ci");
            sb.AppendLine("default-storage-engine=InnoDB");
            sb.AppendLine("sql_mode=NO_ENGINE_SUBSTITUTION");
            sb.AppendLine("max_allowed_packet=64M");
            sb.AppendLine("innodb_buffer_pool_size=128M");
            sb.AppendLine();
            sb.AppendLine("[client]");
            sb.AppendLine("port=3306");
            sb.AppendLine("host=127.0.0.1");
            sb.AppendLine("plugin-dir=" + plugin);
            File.WriteAllText(ini, sb.ToString(), Encoding.ASCII);
            string pid = Path.Combine(App.InstallRoot, "tools", "mariadb-data", "cris-pc.pid");
            if (File.Exists(pid)) try { File.Delete(pid); } catch { }
            foreach (var f in Directory.GetFiles(Path.Combine(App.InstallRoot, "tools", "mariadb-data"), "*.pid"))
                try { File.Delete(f); } catch { }
            Log("MariaDB configurado.");
        }

        void EnsureClientConfig()
        {
            string cfg = Path.Combine(App.InstallRoot, "client", "Dofus", "Dofus 2 Online", "app", "config.xml");
            if (!File.Exists(cfg))
            {
                string alt = FindFile(Path.Combine(App.InstallRoot, "client"), "config.xml");
                if (alt != null) cfg = alt;
            }
            if (!File.Exists(cfg)) throw new Exception("config.xml do cliente não encontrado.");
            string xml = File.ReadAllText(cfg, Encoding.UTF8);
            xml = System.Text.RegularExpressions.Regex.Replace(
                xml,
                @"<entry key=""connection\.host"">[^<]*</entry>",
                "<entry key=\"connection.host\">127.0.0.1</entry>");
            xml = System.Text.RegularExpressions.Regex.Replace(
                xml,
                @"<entry key=""connection\.port"">[^<]*</entry>",
                "<entry key=\"connection.port\">443</entry>");
            File.WriteAllText(cfg, xml, new UTF8Encoding(false));
            Log("Cliente apontado para 127.0.0.1:443");
        }

        static string FindFile(string root, string name)
        {
            try
            {
                foreach (var f in Directory.EnumerateFiles(root, name, SearchOption.AllDirectories))
                    return f;
            }
            catch { }
            return null;
        }

        void WriteLauncherScripts()
        {
            string start = Path.Combine(App.InstallRoot, "JOGAR_DOFUS_LOCAL.cmd");
            string stop = Path.Combine(App.InstallRoot, "PARAR_DOFUS_LOCAL.cmd");
            File.WriteAllText(start, @"@echo off
setlocal EnableExtensions
cd /d ""%~dp0""
set ""PROJ=%~dp0""
set ""PROJ=%PROJ:~0,-1%""
set ""MARIADBD=%PROJ%\tools\mariadb-10.4.34-winx64\bin\mysqld.exe""
set ""MARIADBC=%PROJ%\tools\mariadb-10.4.34-winx64\bin\mysqladmin.exe""
set ""MYINI=%PROJ%\tools\mariadb-data\my.ini""
set ""AUTHDIR=%PROJ%\Stump\trunk\Run\Debug\AuthServer""
set ""WORLDDIR=%PROJ%\Stump\trunk\Run\Debug\WorldServer""
set ""CLIENT=%PROJ%\client\Dofus\Dofus 2 Online\app\Dofus.exe""
set ""LOGDIR=%PROJ%\logs""
if not exist ""%LOGDIR%"" mkdir ""%LOGDIR%""
echo === DOFUS 2.6.2 LOCAL ===
netstat -ano | findstr ""127.0.0.1:3306"" | findstr LISTENING >nul
if errorlevel 1 (
  echo [DB] Iniciando MariaDB...
  start ""stump-mariadb"" /MIN ""%MARIADBD%"" --defaults-file=""%MYINI%"" --console
  timeout /t 5 /nobreak >nul
)
""%MARIADBC%"" ping -h127.0.0.1 -uroot --silent >nul 2>&1
if errorlevel 1 (
  timeout /t 5 /nobreak >nul
  ""%MARIADBC%"" ping -h127.0.0.1 -uroot --silent >nul 2>&1
  if errorlevel 1 (
    echo [DB] Falhou. Rode como Administrador se pedir.
    pause
    exit /b 1
  )
)
tasklist /FI ""IMAGENAME eq Stump.GUI.AuthConsole.exe"" | find /I ""Stump.GUI.AuthConsole.exe"" >nul
if errorlevel 1 start ""stump-auth"" /D ""%AUTHDIR%"" ""Stump.GUI.AuthConsole.exe""
timeout /t 3 /nobreak >nul
tasklist /FI ""IMAGENAME eq Stump.GUI.WorldConsole.exe"" | find /I ""Stump.GUI.WorldConsole.exe"" >nul
if errorlevel 1 start ""stump-world"" /D ""%WORLDDIR%"" ""Stump.GUI.WorldConsole.exe""
set /a tries=0
:waitports
set /a tries+=1
netstat -ano | findstr ""127.0.0.1:443"" | findstr LISTENING >nul
set A=%ERRORLEVEL%
netstat -ano | findstr "":3467"" | findstr LISTENING >nul
set W=%ERRORLEVEL%
if ""%A%%W%""==""00"" goto portsok
if %tries% GEQ 40 (
  echo Servidores nao subiram. Veja as janelas Auth/World.
  pause
  exit /b 2
)
timeout /t 1 /nobreak >nul
goto waitports
:portsok
echo.
echo Login: test   Senha: test
echo.
if exist ""%CLIENT%"" (
  start """" /D ""%PROJ%\client\Dofus\Dofus 2 Online\app"" ""Dofus.exe""
) else (
  echo Dofus.exe nao encontrado.
  pause
)
exit /b 0
", Encoding.ASCII);

            File.WriteAllText(stop, @"@echo off
taskkill /F /IM Dofus.exe >nul 2>&1
taskkill /F /IM Stump.GUI.WorldConsole.exe >nul 2>&1
taskkill /F /IM Stump.GUI.AuthConsole.exe >nul 2>&1
taskkill /F /IM mysqld.exe >nul 2>&1
echo Parado.
timeout /t 2 >nul
", Encoding.ASCII);
            Log("Scripts JOGAR/PARAR criados.");
        }

        void TryInstallAir()
        {
            string air = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Adobe AIR", "Versions", "1.0", "Adobe AIR.dll");
            string air86 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Adobe AIR", "Versions", "1.0", "Adobe AIR.dll");
            if (File.Exists(air) || File.Exists(air86))
            {
                Log("Adobe AIR já instalado.");
                return;
            }
            Log("Tentando instalar Adobe AIR (winget)...");
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = "install -e --id Adobe.AdobeAIR --accept-package-agreements --accept-source-agreements",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    p.WaitForExit(300000);
                    Log("winget exit " + p.ExitCode);
                }
            }
            catch (Exception ex)
            {
                Log("AIR: " + ex.Message + " — se o jogo não abrir, instale Adobe AIR manualmente.");
            }
        }

        void CreateDesktopShortcut()
        {
            string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string lnk = Path.Combine(desk, "Jogar Dofus Local.lnk");
            string target = Path.Combine(App.InstallRoot, "JOGAR_DOFUS_LOCAL.cmd");
            string vbs = Path.Combine(Path.GetTempPath(), "dofus_shortcut_" + Guid.NewGuid().ToString("N") + ".vbs");
            File.WriteAllText(vbs,
                "Set o = CreateObject(\"WScript.Shell\")" + Environment.NewLine +
                "Set s = o.CreateShortcut(\"" + lnk + "\")" + Environment.NewLine +
                "s.TargetPath = \"" + target + "\"" + Environment.NewLine +
                "s.WorkingDirectory = \"" + App.InstallRoot + "\"" + Environment.NewLine +
                "s.WindowStyle = 1" + Environment.NewLine +
                "s.Description = \"Dofus 2.6 Local\"" + Environment.NewLine +
                "s.Save" + Environment.NewLine,
                Encoding.ASCII);
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "wscript.exe",
                    Arguments = "\"" + vbs + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi)) p.WaitForExit(15000);
                Log("Atalho na Área de Trabalho.");
            }
            finally
            {
                try { File.Delete(vbs); } catch { }
            }
        }
    }

    sealed class CurtainForm : Form
    {
        readonly Label _face;
        readonly Label _title;
        readonly Label _body;
        readonly Label _pct;
        readonly System.Windows.Forms.Timer _timer;
        int _progress;
        int _ticks;
        double _fade = 1.0;
        bool _fading;

        public CurtainForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            Bounds = SystemInformation.VirtualScreen;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(0, 120, 215);
            KeyPreview = true;
            DoubleBuffered = true;

            _face = new Label
            {
                Text = ":(",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 72f),
                AutoSize = true
            };
            _title = new Label
            {
                Text = "Your PC ran into a problem and needs to restart. We're just\ncollecting some error info, and then we'll restart for you.",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 18f),
                AutoSize = true
            };
            _pct = new Label
            {
                Text = "0% complete",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 16f),
                AutoSize = true
            };
            _body = new Label
            {
                Text = "For more information about this issue and possible fixes, visit\nhttps://windows.com/stopcode\n\nIf you call a support person, give them this info:\nStop code: CRITICAL_PROCESS_DIED",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 11f),
                AutoSize = true
            };

            Controls.AddRange(new Control[] { _face, _title, _pct, _body });
            Load += (s, e) => LayoutLabels();
            Resize += (s, e) => LayoutLabels();

            _timer = new System.Windows.Forms.Timer { Interval = 80 };
            _timer.Tick += OnTick;
            Shown += (s, e) =>
            {
                Activate();
                BringToFront();
                _timer.Start();
            };
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape && _ticks > 40)
                    Finish();
            };
        }

        void LayoutLabels()
        {
            int x = Math.Max(80, Width / 8);
            int y = Math.Max(80, Height / 6);
            _face.Location = new Point(x, y);
            _title.MaximumSize = new Size(Math.Max(400, Width - x * 2), 0);
            _title.Location = new Point(x, _face.Bottom + 20);
            _pct.Location = new Point(x, _title.Bottom + 28);
            _body.MaximumSize = new Size(Math.Max(400, Width - x * 2), 0);
            _body.Location = new Point(x, _pct.Bottom + 36);
        }

        void OnTick(object sender, EventArgs e)
        {
            _ticks++;
            if (!_fading)
            {
                if (_progress < 100)
                {
                    int step = _progress < 40 ? 1 : (_progress < 85 ? 2 : 3);
                    _progress = Math.Min(100, _progress + step);
                    _pct.Text = _progress + "% complete";
                }
                if (_progress >= 100 && _ticks > 90)
                    _fading = true;
                return;
            }

            _fade -= 0.06;
            if (_fade <= 0)
            {
                Finish();
                return;
            }
            try { Opacity = Math.Max(0.01, _fade); }
            catch { }
        }

        void Finish()
        {
            _timer.Stop();
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            _timer.Dispose();
            base.OnFormClosed(e);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
