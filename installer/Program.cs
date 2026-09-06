using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace DofusLocalSetup
{
    static class App
    {
        public const string Repo = "CristiancMartini/dofus-2.6";
        public const string Tag = "v1.3.0";
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
                        MessageBox.Show(
                            "Instalação concluída!\n\nUse o atalho \"Jogar Dofus Local\" na Área de Trabalho.\n\nLogin: test\nSenha: test\n\nIdioma: português. Monstros, NPCs e zaaps já estão no mundo.",
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
            xml = System.Text.RegularExpressions.Regex.Replace(
                xml,
                @"<entry key=""lang\.current"">[^<]*</entry>",
                "<entry key=\"lang.current\">pt</entry>");
            xml = System.Text.RegularExpressions.Regex.Replace(
                xml,
                @"<entry key=""binds\.current"">[^<]*</entry>",
                "<entry key=\"binds.current\">ptBR</entry>");
            File.WriteAllText(cfg, xml, new UTF8Encoding(false));
            Log("Cliente: 127.0.0.1:443 + idioma pt");
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
set ""CLIENTDIR=%PROJ%\client\Dofus\Dofus 2 Online\app""
set ""CLIENT=%CLIENTDIR%\Dofus.exe""
set ""LOGDIR=%PROJ%\logs""
if not exist ""%LOGDIR%"" mkdir ""%LOGDIR%""
echo === DOFUS 2.6.2 LOCAL PT ===
echo Janelas: 1-MariaDB  2-Auth  3-World  4-Cliente
netstat -ano | findstr ""127.0.0.1:3306"" | findstr LISTENING >nul
if errorlevel 1 goto start_db
echo [1/4 DB] Ja escutando
goto db_ping
:start_db
echo [1/4 DB] Iniciando MariaDB...
start ""1-MariaDB"" ""%MARIADBD%"" --defaults-file=""%MYINI%"" --console
ping -n 5 127.0.0.1 >nul
:db_ping
""%MARIADBC%"" ping -h127.0.0.1 -uroot --silent >nul 2>&1
if not errorlevel 1 goto db_ok
echo [DB] Aguardando...
ping -n 6 127.0.0.1 >nul
""%MARIADBC%"" ping -h127.0.0.1 -uroot --silent >nul 2>&1
if errorlevel 1 (
  echo [DB] Falhou. Rode como Administrador se pedir.
  pause
  exit /b 1
)
:db_ok
echo [1/4 DB] OK
tasklist /FI ""IMAGENAME eq Stump.GUI.AuthConsole.exe"" | find /I ""Stump.GUI.AuthConsole.exe"" >nul
if errorlevel 1 goto start_auth
echo [2/4 AUTH] Ja em execucao
goto after_auth
:start_auth
echo [2/4 AUTH] Iniciando AuthServer...
start ""2-Auth"" /D ""%AUTHDIR%"" ""Stump.GUI.AuthConsole.exe""
:after_auth
ping -n 4 127.0.0.1 >nul
tasklist /FI ""IMAGENAME eq Stump.GUI.WorldConsole.exe"" | find /I ""Stump.GUI.WorldConsole.exe"" >nul
if errorlevel 1 goto start_world
echo [3/4 WORLD] Ja em execucao
goto after_world
:start_world
echo [3/4 WORLD] Iniciando WorldServer...
start ""3-World"" /D ""%WORLDDIR%"" ""Stump.GUI.WorldConsole.exe""
:after_world
echo Aguardando portas...
set /a tries=0
:waitports
set /a tries+=1
netstat -ano | findstr ""127.0.0.1:443"" | findstr LISTENING >nul
set A=%ERRORLEVEL%
netstat -ano | findstr "":3467"" | findstr LISTENING >nul
set W=%ERRORLEVEL%
if ""%A%%W%""==""00"" goto portsok
if %tries% GEQ 50 (
  echo Servidores nao subiram. Veja as janelas 2-Auth / 3-World.
  pause
  exit /b 2
)
ping -n 2 127.0.0.1 >nul
goto waitports
:portsok
echo [2/4 AUTH] OK
echo [3/4 WORLD] OK
echo.
echo Login: test   Senha: test
echo.
if not exist ""%CLIENT%"" (
  echo [4/4 CLIENT] Dofus.exe nao encontrado.
  pause
  exit /b 3
)
echo [4/4 CLIENT] Abrindo Dofus...
start ""4-Cliente"" /D ""%CLIENTDIR%"" ""Dofus.exe""
exit /b 0
", Encoding.ASCII);

            File.WriteAllText(stop, @"@echo off
taskkill /F /IM Dofus.exe >nul 2>&1
taskkill /F /IM Stump.GUI.WorldConsole.exe >nul 2>&1
taskkill /F /IM Stump.GUI.AuthConsole.exe >nul 2>&1
set ""MYSQLADMIN=%~dp0tools\mariadb-10.4.34-winx64\bin\mysqladmin.exe""
if exist ""%MYSQLADMIN%"" ""%MYSQLADMIN%"" -h127.0.0.1 -uroot shutdown >nul 2>&1
taskkill /F /IM mysqld.exe >nul 2>&1
echo Parado.
ping -n 3 127.0.0.1 >nul
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
