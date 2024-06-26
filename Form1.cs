using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Drawing.Imaging;

namespace Invisible_Streamer
{
    public partial class Form1 : Form
    {
        private NotifyIcon notifyIcon;
        private CancellationTokenSource cts;

        private readonly RtspServer _rtspServer;
        private readonly ILogger _logger;

        private readonly int _width = Screen.PrimaryScreen.Bounds.Width;
        private readonly int _height = Screen.PrimaryScreen.Bounds.Height;
        private readonly uint _fps = 30;

        private readonly int port = 8554;
        private readonly string username = "user";      // or use NUL if there is no username
        private readonly string password = "password";  // or use NUL if there is no password
        private readonly string _ipAddress;

        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public Form1(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Form1>();
            _rtspServer = new RtspServer(port, username, password, loggerFactory);
            InitializeComponent();
            InitializeTrayIcon();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Скрыть главное окно
            this.Visible = false;
            this.ShowInTaskbar = false;

            _rtspServer.StartListen();
            cts = new CancellationTokenSource();
            Task.Run(() => DoBackgroundWork(cts.Token), cts.Token);
        }

        private void InitializeTrayIcon()
        {
            notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                ContextMenuStrip = new ContextMenuStrip()
            };

            notifyIcon.ContextMenuStrip.Items.Add("Exit", null, OnExit);
        }

        private void OnExit(object sender, EventArgs e)
        {
            _rtspServer.StopListen();
            cts.Cancel();
            notifyIcon.Visible = false;
            Application.Exit();
        }

        private async Task DoBackgroundWork(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using (Bitmap bitmap = new Bitmap(_width, _height))
                    {
                        using (Graphics g = Graphics.FromImage(bitmap))
                        {
                            g.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
                        }

                        using (MemoryStream ms = new MemoryStream())
                        {
                            bitmap.Save(ms, ImageFormat.Jpeg);
                            byte[] jpegData = ms.ToArray();

                            // Send the frame to all clients
                            _rtspServer.FeedInRawJPEG((uint)_stopwatch.ElapsedMilliseconds, jpegData, _width, _height);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error: ", ex.Message);
                }

                await Task.Delay(1000 / (int)_fps, token);
            }

            _stopwatch.Stop();
        }
    }
}
