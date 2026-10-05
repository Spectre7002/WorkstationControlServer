using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using WorkstationControlServer.Sensors;
using WorkstationControlServer.Models;
using WorkstationControlServer.Audio;
using WorkstationControlServer.SystemActions;
using WorkstationControlServer.ProcessManagement;
using System.IO;

namespace WorkstationControlServer
{
    public partial class Form1 : Form
    {
        private TelemetryService _telemetryService;
        private AudioController _audioController;
        private NetworkMonitor _networkMonitor;
        private TrafficMonitor _trafficMonitor;
        private ActiveWindowTracker _windowTracker;

        private SystemController _sysController;
        private BrightnessController _brightnessController;
        private TaskManager _taskManager;
        private MediaController _mediaController;
        private GsmTcTrackMonitor _trackMonitor;

        private System.Windows.Forms.Timer _timer;

        private TextBox _txtProfile;
        private TextBox _txtLog;
        private TextBox _txtTasks;
        private TextBox _txtNetwork;
        private Button _btnStart;

        private TrackBar _tbBrightness;
        private TrackBar _tbVolume;
        private CheckBox _chkMute;
        private CheckBox _chkMicMute;

        private FlowLayoutPanel _pnlApps;
        private Dictionary<int, Panel> _appPanels;

        private Label _lblTrackTitle;
        private Label _lblTrackArtist;
        private PictureBox _picAlbumArt;

        private bool _isUpdatingFromSystem = false;
        private bool _isTelemetryActive = false;

        private delegate void SyncAudioUIDelegate(float volume, bool isMuted);
        private delegate void SyncMicUIDelegate(bool isMuted);
        private delegate void UpdateNetworkUIDelegate(string appName, string ip, int ping, string country);

        private string _lastPingStr = "Очікування мережевої активності...";

        public Form1()
        {
            _appPanels = new Dictionary<int, Panel>();

            _sysController = new SystemController();
            _brightnessController = new BrightnessController();
            _taskManager = new TaskManager();
            _mediaController = new MediaController();
            _trafficMonitor = new TrafficMonitor();
            _windowTracker = new ActiveWindowTracker();

            SetupUI();
            this.Shown += new EventHandler(Form1_Shown);
            _telemetryService = new TelemetryService();
            _telemetryService.Start();

            _audioController = new AudioController();
            _audioController.AudioStateChanged += new AudioStateChangedHandler(AudioController_AudioStateChanged);
            _audioController.MicStateChanged += new MicStateChangedHandler(AudioController_MicStateChanged);

            _networkMonitor = new NetworkMonitor();
            _networkMonitor.NetworkStatsUpdated += new NetworkStatsUpdatedHandler(NetworkMonitor_NetworkStatsUpdated);
            _networkMonitor.Start();

            _isUpdatingFromSystem = true;

            _tbVolume.Value = (int)_audioController.GetMasterVolume();
            _chkMute.Checked = _audioController.GetMute();
            _chkMicMute.Checked = _audioController.GetMicMute();

            int currentBrightness = _brightnessController.GetBrightness();
            if (currentBrightness >= 0 && currentBrightness <= 100)
            {
                _tbBrightness.Value = currentBrightness;
            }

            _isUpdatingFromSystem = false;

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 1000;
            _timer.Tick += new EventHandler(Timer_Tick);
            _timer.Start();
        }

        private void SetupUI()
        {
            this.Font = new Font("Segoe UI", 12F, FontStyle.Regular);
            this.Width = 1200;
            this.Height = 1150;
            this.Text = "Workstation Control Server";
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            _btnStart = new Button();
            _btnStart.Text = "Почати моніторинг заліза";
            _btnStart.Top = 20;
            _btnStart.Left = 20;
            _btnStart.Width = 1140;
            _btnStart.Height = 50;
            _btnStart.Click += new EventHandler(BtnStart_Click);
            this.Controls.Add(_btnStart);

            // ================= ЛІВА КОЛОНКА =================

            _txtProfile = new TextBox();
            _txtProfile.Top = 90;
            _txtProfile.Left = 20;
            _txtProfile.Width = 550;
            _txtProfile.Height = 30;
            _txtProfile.ReadOnly = true;
            _txtProfile.Text = "Профіль ESP32: Завантаження...";
            this.Controls.Add(_txtProfile);

            _txtLog = new TextBox();
            _txtLog.Multiline = true;
            _txtLog.Top = 135;
            _txtLog.Left = 20;
            _txtLog.Width = 550;
            _txtLog.Height = 350;
            _txtLog.ReadOnly = true;
            _txtLog.Text = "Телеметрія на паузі. Аудіо та Мережа працюють фоново.";
            this.Controls.Add(_txtLog);

            _txtTasks = new TextBox();
            _txtTasks.Multiline = true;
            _txtTasks.Top = 505;
            _txtTasks.Left = 20;
            _txtTasks.Width = 550;
            _txtTasks.Height = 280;
            _txtTasks.ReadOnly = true;
            _txtTasks.Text = "Очікування даних диспетчера задач...";
            this.Controls.Add(_txtTasks);

            Button btnLock = new Button() { Text = "Блокувати", Top = 805, Left = 20, Width = 130, Height = 45 };
            btnLock.Click += new EventHandler(BtnLock_Click);
            this.Controls.Add(btnLock);

            Button btnScreenOff = new Button() { Text = "Вимк. Екран", Top = 805, Left = 160, Width = 130, Height = 45 };
            btnScreenOff.Click += new EventHandler(BtnScreenOff_Click);
            this.Controls.Add(btnScreenOff);

            Button btnSleep = new Button() { Text = "Сон", Top = 805, Left = 300, Width = 130, Height = 45 };
            btnSleep.Click += new EventHandler(BtnSleep_Click);
            this.Controls.Add(btnSleep);

            Button btnShutdown = new Button() { Text = "Вимкнути", Top = 805, Left = 440, Width = 130, Height = 45 };
            btnShutdown.Click += new EventHandler(BtnShutdown_Click);
            this.Controls.Add(btnShutdown);

            Label lblBrightness = new Label();
            lblBrightness.Text = "Яскравість екрану:";
            lblBrightness.Top = 875;
            lblBrightness.Left = 20;
            lblBrightness.Width = 160;
            lblBrightness.Height = 30;
            this.Controls.Add(lblBrightness);

            _tbBrightness = new TrackBar();
            _tbBrightness.Minimum = 0;
            _tbBrightness.Maximum = 100;
            _tbBrightness.TickFrequency = 10;
            _tbBrightness.Top = 870;
            _tbBrightness.Left = 180;
            _tbBrightness.Width = 390;
            _tbBrightness.Scroll += new EventHandler(TbBrightness_Scroll);
            this.Controls.Add(_tbBrightness);

            Label lblSys = new Label();
            lblSys.Text = "Головний звук:";
            lblSys.Top = 935;
            lblSys.Left = 20;
            lblSys.Width = 140;
            lblSys.Height = 30;
            this.Controls.Add(lblSys);

            _tbVolume = new TrackBar();
            _tbVolume.Minimum = 0;
            _tbVolume.Maximum = 100;
            _tbVolume.TickFrequency = 10;
            _tbVolume.Top = 930;
            _tbVolume.Left = 160;
            _tbVolume.Width = 230;
            _tbVolume.Scroll += new EventHandler(TbVolume_Scroll);
            this.Controls.Add(_tbVolume);

            _chkMute = new CheckBox();
            _chkMute.Text = "Mute";
            _chkMute.Top = 935;
            _chkMute.Left = 390;
            _chkMute.Width = 80;
            _chkMute.Height = 30;
            _chkMute.CheckedChanged += new EventHandler(ChkMute_CheckedChanged);
            this.Controls.Add(_chkMute);

            _chkMicMute = new CheckBox();
            _chkMicMute.Text = "Mic Mute";
            _chkMicMute.Top = 935;
            _chkMicMute.Left = 470;
            _chkMicMute.Width = 100;
            _chkMicMute.Height = 30;
            _chkMicMute.CheckedChanged += new EventHandler(ChkMicMute_CheckedChanged);
            this.Controls.Add(_chkMicMute);

            Button btnPrev = new Button() { Text = "⏪ Попередня", Top = 985, Left = 20, Width = 150, Height = 45 };
            btnPrev.Click += new EventHandler(BtnPrev_Click);
            this.Controls.Add(btnPrev);

            Button btnPlay = new Button() { Text = "⏯ Play / Pause", Top = 985, Left = 190, Width = 200, Height = 45 };
            btnPlay.Click += new EventHandler(BtnPlay_Click);
            this.Controls.Add(btnPlay);

            Button btnNext = new Button() { Text = "Наступна ⏩", Top = 985, Left = 410, Width = 160, Height = 45 };
            btnNext.Click += new EventHandler(BtnNext_Click);
            this.Controls.Add(btnNext);

            // ================= ПРАВА КОЛОНКА =================

            _txtNetwork = new TextBox();
            _txtNetwork.Multiline = true;
            _txtNetwork.Top = 90;
            _txtNetwork.Left = 590;
            _txtNetwork.Width = 570;
            _txtNetwork.Height = 160;
            _txtNetwork.ReadOnly = true;
            _txtNetwork.Text = "Очікування мережевої активності...";
            _txtNetwork.TextAlign = HorizontalAlignment.Center;
            this.Controls.Add(_txtNetwork);

            _picAlbumArt = new PictureBox();
            _picAlbumArt.Top = 265;
            _picAlbumArt.Left = 590;
            _picAlbumArt.Width = 80;
            _picAlbumArt.Height = 80;
            _picAlbumArt.SizeMode = PictureBoxSizeMode.Zoom;
            _picAlbumArt.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(_picAlbumArt);

            _lblTrackTitle = new Label();
            _lblTrackTitle.Text = "Трек не відтворюється";
            _lblTrackTitle.Top = 270;
            _lblTrackTitle.Left = 685;
            _lblTrackTitle.Width = 475;
            _lblTrackTitle.Height = 30;
            _lblTrackTitle.AutoEllipsis = true;
            this.Controls.Add(_lblTrackTitle);

            _lblTrackArtist = new Label();
            _lblTrackArtist.Text = string.Empty;
            _lblTrackArtist.Top = 310;
            _lblTrackArtist.Left = 685;
            _lblTrackArtist.Width = 475;
            _lblTrackArtist.Height = 30;
            _lblTrackArtist.AutoEllipsis = true;
            this.Controls.Add(_lblTrackArtist);

            Label lblAppsHeader = new Label();
            lblAppsHeader.Text = "Гучність запущених програм:";
            lblAppsHeader.Top = 365;
            lblAppsHeader.Left = 590;
            lblAppsHeader.Width = 570;
            lblAppsHeader.Height = 30;
            this.Controls.Add(lblAppsHeader);

            _pnlApps = new FlowLayoutPanel();
            _pnlApps.Top = 400;
            _pnlApps.Left = 590;
            _pnlApps.Width = 570;
            _pnlApps.Height = 630;
            _pnlApps.AutoScroll = true;
            _pnlApps.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(_pnlApps);
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            _isTelemetryActive = !_isTelemetryActive;

            if (_isTelemetryActive)
            {
                _btnStart.Text = "Зупинити моніторинг заліза";
            }
            else
            {
                _btnStart.Text = "Почати моніторинг заліза";
                _txtLog.Text = "Телеметрія на паузі. Аудіо та Мережа працюють фоново.";
                _txtTasks.Text = "Моніторинг процесів на паузі.";
            }
        }

        private void BtnPrev_Click(object sender, EventArgs e)
        {
            _mediaController.PrevTrack();
        }

        private void BtnPlay_Click(object sender, EventArgs e)
        {
            _mediaController.PlayPause();
        }

        private void BtnNext_Click(object sender, EventArgs e)
        {
            _mediaController.NextTrack();
        }

        private void TbBrightness_Scroll(object sender, EventArgs e)
        {
            if (_isUpdatingFromSystem) return;
            _brightnessController.SetBrightness(_tbBrightness.Value);
        }

        private void BtnLock_Click(object sender, EventArgs e)
        {
            _sysController.LockPC();
        }

        private void BtnScreenOff_Click(object sender, EventArgs e)
        {
            _sysController.TurnOffMonitor(this.Handle);
        }

        private void BtnSleep_Click(object sender, EventArgs e)
        {
            _sysController.SleepPC();
        }

        private void BtnShutdown_Click(object sender, EventArgs e)
        {
            DialogResult dialogResult = MessageBox.Show("Ви дійсно хочете вимкнути комп'ютер?", "Підтвердження", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (dialogResult == DialogResult.Yes)
            {
                _sysController.ShutdownPC();
            }
        }

        private void TbVolume_Scroll(object sender, EventArgs e)
        {
            if (_isUpdatingFromSystem) return;
            _audioController.SetMasterVolume(_tbVolume.Value);
        }

        private void ChkMute_CheckedChanged(object sender, EventArgs e)
        {
            if (_isUpdatingFromSystem) return;
            _audioController.SetMute(_chkMute.Checked);
        }

        private void ChkMicMute_CheckedChanged(object sender, EventArgs e)
        {
            if (_isUpdatingFromSystem) return;
            _audioController.SetMicMute(_chkMicMute.Checked);
        }

        private void AudioController_AudioStateChanged(float volume, bool isMuted)
        {
            SyncAudioUI(volume, isMuted);
        }

        private void AudioController_MicStateChanged(bool isMuted)
        {
            SyncMicUI(isMuted);
        }

        private void NetworkMonitor_NetworkStatsUpdated(string appName, string ip, int ping, string country)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            if (this.InvokeRequired)
            {
                this.Invoke(new UpdateNetworkUIDelegate(NetworkMonitor_NetworkStatsUpdated), new object[] { appName, ip, ping, country });
                return;
            }

            if (appName == "Немає активних програм")
            {
                _lastPingStr = "Очікування ігрової активності...";
            }
            else
            {
                _lastPingStr = string.Format("[{0}] Сервер: {1} | Ping: {2}ms | {3}", appName.ToUpper(), ip, ping, country);
            }
        }

        private void SyncAudioUI(float volume, bool isMuted)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            if (this.InvokeRequired)
            {
                this.Invoke(new SyncAudioUIDelegate(SyncAudioUI), new object[] { volume, isMuted });
                return;
            }
            _isUpdatingFromSystem = true;
            _tbVolume.Value = (int)volume;
            _chkMute.Checked = isMuted;
            _isUpdatingFromSystem = false;
        }

        private void SyncMicUI(bool isMuted)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            if (this.InvokeRequired)
            {
                this.Invoke(new SyncMicUIDelegate(SyncMicUI), new object[] { isMuted });
                return;
            }
            _isUpdatingFromSystem = true;
            _chkMicMute.Checked = isMuted;
            _isUpdatingFromSystem = false;
        }

        private void AppVolume_Scroll(object sender, EventArgs e)
        {
            if (_isUpdatingFromSystem) return;
            TrackBar tb = (TrackBar)sender;
            int processId = (int)tb.Tag;
            _audioController.SetApplicationVolume(processId, tb.Value);
        }

        private void AppMute_CheckedChanged(object sender, EventArgs e)
        {
            if (_isUpdatingFromSystem) return;
            CheckBox chk = (CheckBox)sender;
            int processId = (int)chk.Tag;
            _audioController.SetApplicationMute(processId, chk.Checked);
        }

        private Panel CreateAppControlPanel(AudioSessionInfo info)
        {
            Panel p = new Panel();
            p.Width = 540;
            p.Height = 85;
            p.Name = "pnl_" + info.ProcessId;

            Label lbl = new Label();
            lbl.Text = info.ProcessName + " (PID: " + info.ProcessId + ")";
            lbl.Top = 5;
            lbl.Left = 10;
            lbl.Width = 450;
            p.Controls.Add(lbl);

            TrackBar tb = new TrackBar();
            tb.Name = "tb_" + info.ProcessId;
            tb.Tag = info.ProcessId;
            tb.Minimum = 0;
            tb.Maximum = 100;
            tb.TickFrequency = 10;
            tb.Value = (int)info.Volume;
            tb.Top = 35;
            tb.Left = 5;
            tb.Width = 430;
            tb.Scroll += new EventHandler(AppVolume_Scroll);
            p.Controls.Add(tb);

            CheckBox chk = new CheckBox();
            chk.Name = "chk_" + info.ProcessId;
            chk.Tag = info.ProcessId;
            chk.Text = "Mute";
            chk.Checked = info.IsMuted;
            chk.Top = 40;
            chk.Left = 440;
            chk.Width = 90;
            chk.CheckedChanged += new EventHandler(AppMute_CheckedChanged);
            p.Controls.Add(chk);

            return p;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            string currentWindow = _windowTracker.GetActiveWindowTitle();
            string profile = _windowTracker.DetermineProfile(currentWindow);
            _txtProfile.Text = string.Format("Вікно: {0}  |  Профіль ESP32: {1}", currentWindow, profile);

            _trafficMonitor.UpdateStats(out double downloadMBps, out double uploadMBps);
            string netStr = _lastPingStr + string.Format("\r\n\r\nЗавантаження: {0:0.0} MB/s | Віддача: {1:0.0} MB/s", downloadMBps, uploadMBps);
            _txtNetwork.Text = netStr;

            if (_isTelemetryActive)
            {
                TelemetryData data = _telemetryService.GetCurrentTelemetry();

                string log = "=== Показники заліза ===\r\n\r\n";
                log += string.Format("CPU Температура:\t{0} °C\r\n", data.CpuTemperature);
                log += string.Format("CPU Навантаження:\t{0} %\r\n", data.CpuLoad);
                log += string.Format("CPU Частота:\t\t{0} MHz\r\n\r\n", data.CpuClock);

                log += string.Format("GPU Температура:\t{0} °C\r\n", data.GpuTemperature);
                log += string.Format("GPU Навантаження:\t{0} %\r\n", data.GpuLoad);
                log += string.Format("GPU Частота:\t\t{0} MHz\r\n\r\n", data.GpuClock);

                log += string.Format("ОЗП Використання:\t{0} %  ({1:0.0} / {2:0.0} GB)\r\n", data.RamUsagePercent, data.RamUsedGB, data.RamTotalGB);
                log += string.Format("ОЗП Частота:\t\t{0} MT/s\r\n", data.RamSpeedMts);

                _txtLog.Text = log;

                List<ProcessInfo> topProcs = _taskManager.GetTopProcesses(5);
                string taskLog = "=== Топ-5 процесів (ОЗП) ===\r\n\r\n";
                foreach (ProcessInfo pInfo in topProcs)
                {
                    taskLog += pInfo.Name + ": \t\t" + (pInfo.RamBytes / 1024 / 1024) + " MB\r\n";
                }
                _txtTasks.Text = taskLog;
            }

            List<AudioSessionInfo> currentSessions = _audioController.GetAudioSessions();

            List<int> pidsToRemove = new List<int>();
            foreach (int existingPid in _appPanels.Keys)
            {
                bool found = false;
                foreach (AudioSessionInfo session in currentSessions)
                {
                    if (session.ProcessId == existingPid)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) pidsToRemove.Add(existingPid);
            }

            foreach (int pid in pidsToRemove)
            {
                _pnlApps.Controls.Remove(_appPanels[pid]);
                _appPanels.Remove(pid);
            }

            foreach (AudioSessionInfo session in currentSessions)
            {
                if (_appPanels.ContainsKey(session.ProcessId))
                {
                    Panel p = _appPanels[session.ProcessId];
                    TrackBar tb = (TrackBar)p.Controls["tb_" + session.ProcessId];
                    CheckBox chk = (CheckBox)p.Controls["chk_" + session.ProcessId];

                    if (tb != null && chk != null)
                    {
                        if (tb.Value != (int)session.Volume || chk.Checked != session.IsMuted)
                        {
                            _isUpdatingFromSystem = true;
                            if (tb.Value != (int)session.Volume) tb.Value = (int)session.Volume;
                            if (chk.Checked != session.IsMuted) chk.Checked = session.IsMuted;
                            _isUpdatingFromSystem = false;
                        }
                    }
                }
                else
                {
                    Panel newPanel = CreateAppControlPanel(session);
                    _appPanels.Add(session.ProcessId, newPanel);
                    _pnlApps.Controls.Add(newPanel);
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _timer.Stop();
            _telemetryService.Stop();
            _networkMonitor.Stop();

            if (_audioController != null)
            {
                _audioController.Dispose();
            }
            _trackMonitor?.Dispose();
            _picAlbumArt?.Image?.Dispose();
            base.OnFormClosing(e);
        }

        private async void Form1_Shown(object sender, EventArgs e)
        {
            _trackMonitor = new GsmTcTrackMonitor();
            _trackMonitor.MetadataChanged += TrackMonitor_MetadataChanged;
            _trackMonitor.ErrorOccurred += TrackMonitor_ErrorOccurred;

            try
            {
                await _trackMonitor.StartAsync();
            }
            catch (Exception exception)
            {
                _lblTrackTitle.Text = "Не вдалося отримати медіасесію";
                _lblTrackArtist.Text = exception.Message;
            }
        }

        private void TrackMonitor_ErrorOccurred(Exception exception)
        {
            if (this.IsDisposed || !this.IsHandleCreated)
            {
                return;
            }

            if (this.InvokeRequired)
            {
                this.Invoke(new Action<Exception>(TrackMonitor_ErrorOccurred), new object[] { exception });
                return;
            }

            _lblTrackArtist.Text = "Помилка: " + exception.GetType().Name;
        }

        private void TrackMonitor_MetadataChanged(TrackMetadata metadata)
        {
            if (this.IsDisposed || !this.IsHandleCreated)
            {
                return;
            }

            if (this.InvokeRequired)
            {
                this.Invoke(new Action<TrackMetadata>(TrackMonitor_MetadataChanged), new object[] { metadata });
                return;
            }

            _lblTrackTitle.Text = string.IsNullOrWhiteSpace(metadata.Title)
                ? "Трек не відтворюється"
                : metadata.Title;

            _lblTrackArtist.Text = metadata.Artist ?? string.Empty;

            Image newImage = null;

            if (metadata.AlbumArt != null && metadata.AlbumArt.Length > 0)
            {
                try
                {
                    using (MemoryStream stream = new MemoryStream(metadata.AlbumArt))
                    using (Image image = Image.FromStream(stream))
                    {
                        newImage = new Bitmap(image);
                    }
                }
                catch
                {
                    // Дані не є зображенням або пошкоджені
                }
            }

            Image oldImage = _picAlbumArt.Image;
            _picAlbumArt.Image = newImage;
            oldImage?.Dispose();
        }
    }
}