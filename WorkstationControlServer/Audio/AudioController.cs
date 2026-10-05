using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace WorkstationControlServer.Audio
{
    public delegate void AudioStateChangedHandler(float volume, bool isMuted);
    public delegate void MicStateChangedHandler(bool isMuted);

    // Клас для зберігання інформації про аудіосесію (окрему програму)
    public class AudioSessionInfo
    {
        public int ProcessId;
        public string ProcessName;
        public float Volume;
        public bool IsMuted;
    }

    public class AudioController : IDisposable
    {
        public event AudioStateChangedHandler AudioStateChanged;
        public event MicStateChangedHandler MicStateChanged;

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject { }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int NotImpl1();
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, out IntPtr ppInterface);
        }

        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            int RegisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
            int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
            int GetChannelCount(out uint pnChannelCount);
            int SetMasterVolumeLevel(float fLevelDB, IntPtr pguidEventContext);
            int SetMasterVolumeLevelScalar(float fLevel, IntPtr pguidEventContext);
            int GetMasterVolumeLevel(out float pfLevelDB);
            int GetMasterVolumeLevelScalar(out float pfLevel);
            int SetChannelVolumeLevel(uint nChannel, float fLevelDB, IntPtr pguidEventContext);
            int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, IntPtr pguidEventContext);
            int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
            int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, IntPtr pguidEventContext);
            int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        }

        [Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolumeCallback
        {
            void OnNotify(IntPtr pNotify);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AUDIO_VOLUME_NOTIFICATION_DATA
        {
            public Guid guidEventContext;
            public int bMuted;
            public float fMasterVolume;
            public uint nChannels;
            public float afChannelVolumes;
        }

        [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionManager2
        {
            int NotImpl1();
            int NotImpl2();
            int GetSessionEnumerator(out IAudioSessionEnumerator SessionEnum);
        }

        [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionEnumerator
        {
            int GetCount(out int SessionCount);
            int GetSession(int SessionCount, out IAudioSessionControl Session);
        }

        [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl
        {
            int NotImpl1(); int NotImpl2(); int NotImpl3(); int NotImpl4();
            int NotImpl5(); int NotImpl6(); int NotImpl7(); int NotImpl8(); int NotImpl9();
        }

        [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl2
        {
            int NotImpl1(); int NotImpl2(); int NotImpl3(); int NotImpl4(); int NotImpl5();
            int NotImpl6(); int NotImpl7(); int NotImpl8(); int NotImpl9();
            int GetSessionIdentifier(out IntPtr retVal);
            int GetSessionInstanceIdentifier(out IntPtr retVal);
            int GetProcessId(out uint retVal);
            int IsSystemSoundsSession();
            int SetDuckingPreference(bool optOut);
        }

        [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ISimpleAudioVolume
        {
            int SetMasterVolume(float fLevel, IntPtr EventContext);
            int GetMasterVolume(out float pfLevel);
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, IntPtr EventContext);
            int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        }

        private class AudioVolumeCallback : IAudioEndpointVolumeCallback
        {
            private AudioController _parent;
            private bool _isMic;

            public AudioVolumeCallback(AudioController parent, bool isMic)
            {
                _parent = parent;
                _isMic = isMic;
            }

            public void OnNotify(IntPtr pNotify)
            {
                if (pNotify != IntPtr.Zero)
                {
                    AUDIO_VOLUME_NOTIFICATION_DATA data = (AUDIO_VOLUME_NOTIFICATION_DATA)Marshal.PtrToStructure(pNotify, typeof(AUDIO_VOLUME_NOTIFICATION_DATA));
                    if (_isMic)
                    {
                        _parent.RaiseMicStateChanged(data.bMuted != 0);
                    }
                    else
                    {
                        _parent.RaiseAudioStateChanged(data.fMasterVolume * 100f, data.bMuted != 0);
                    }
                }
            }
        }

        private IAudioEndpointVolume _renderVolumeObject;
        private IAudioEndpointVolume _captureVolumeObject;
        private AudioVolumeCallback _renderCallback;
        private AudioVolumeCallback _captureCallback;

        public AudioController()
        {
            _renderVolumeObject = GetEndpointVolume(0);
            if (_renderVolumeObject != null)
            {
                _renderCallback = new AudioVolumeCallback(this, false);
                _renderVolumeObject.RegisterControlChangeNotify(_renderCallback);
            }

            _captureVolumeObject = GetEndpointVolume(1);
            if (_captureVolumeObject != null)
            {
                _captureCallback = new AudioVolumeCallback(this, true);
                _captureVolumeObject.RegisterControlChangeNotify(_captureCallback);
            }
        }

        private IAudioEndpointVolume GetEndpointVolume(int dataFlow)
        {
            try
            {
                Type enumType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
                object enumeratorObject = Activator.CreateInstance(enumType);
                IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)enumeratorObject;

                IMMDevice device = null;
                enumerator.GetDefaultAudioEndpoint(dataFlow, 1, out device);

                Guid iidVolume = typeof(IAudioEndpointVolume).GUID;
                IntPtr ptrVolume = IntPtr.Zero;
                device.Activate(ref iidVolume, 23, IntPtr.Zero, out ptrVolume);

                return (IAudioEndpointVolume)Marshal.GetObjectForIUnknown(ptrVolume);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Помилка доступу до аудіо: " + ex.Message);
                return null;
            }
        }

        internal void RaiseAudioStateChanged(float volume, bool isMuted)
        {
            if (AudioStateChanged != null) AudioStateChanged(volume, isMuted);
        }

        internal void RaiseMicStateChanged(bool isMuted)
        {
            if (MicStateChanged != null) MicStateChanged(isMuted);
        }

        // --- Загальна гучність та мікрофон ---

        public void SetMasterVolume(float levelPercent)
        {
            if (levelPercent < 0f) levelPercent = 0f;
            if (levelPercent > 100f) levelPercent = 100f;
            try
            {
                if (_renderVolumeObject != null)
                {
                    _renderVolumeObject.SetMasterVolumeLevelScalar(levelPercent / 100f, IntPtr.Zero);
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }

        public float GetMasterVolume()
        {
            try
            {
                if (_renderVolumeObject != null)
                {
                    float scalar;
                    _renderVolumeObject.GetMasterVolumeLevelScalar(out scalar);
                    return scalar * 100f;
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
            return 0f;
        }

        public void SetMute(bool mute)
        {
            try
            {
                if (_renderVolumeObject != null) _renderVolumeObject.SetMute(mute, IntPtr.Zero);
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }

        public bool GetMute()
        {
            try
            {
                if (_renderVolumeObject != null)
                {
                    bool isMuted;
                    _renderVolumeObject.GetMute(out isMuted);
                    return isMuted;
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
            return false;
        }

        public void SetMicMute(bool mute)
        {
            try
            {
                if (_captureVolumeObject != null)
                {
                    _captureVolumeObject.SetMute(mute, IntPtr.Zero);
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }

        public bool GetMicMute()
        {
            try
            {
                if (_captureVolumeObject != null)
                {
                    bool isMuted;
                    _captureVolumeObject.GetMute(out isMuted);
                    return isMuted;
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
            return false;
        }

        // --- Керування звуком окремих програм ---

        // Отримання списку всіх активних програм, що видають звук
        public List<AudioSessionInfo> GetAudioSessions()
        {
            List<AudioSessionInfo> list = new List<AudioSessionInfo>();
            try
            {
                Type enumType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
                object enumeratorObject = Activator.CreateInstance(enumType);
                IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)enumeratorObject;

                IMMDevice device = null;
                enumerator.GetDefaultAudioEndpoint(0, 1, out device);

                Guid iidManager = typeof(IAudioSessionManager2).GUID;
                IntPtr ptrManager = IntPtr.Zero;
                device.Activate(ref iidManager, 23, IntPtr.Zero, out ptrManager);
                IAudioSessionManager2 manager = (IAudioSessionManager2)Marshal.GetObjectForIUnknown(ptrManager);

                IAudioSessionEnumerator sessionEnum;
                manager.GetSessionEnumerator(out sessionEnum);

                int count;
                sessionEnum.GetCount(out count);

                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl control;
                    sessionEnum.GetSession(i, out control);
                    IAudioSessionControl2 control2 = control as IAudioSessionControl2;

                    if (control2 != null)
                    {
                        uint pid;
                        control2.GetProcessId(out pid);

                        if (pid > 0)
                        {
                            try
                            {
                                Process p = Process.GetProcessById((int)pid);
                                ISimpleAudioVolume volume = control2 as ISimpleAudioVolume;
                                if (volume != null)
                                {
                                    float vol;
                                    volume.GetMasterVolume(out vol);
                                    bool isMuted;
                                    volume.GetMute(out isMuted);

                                    AudioSessionInfo info = new AudioSessionInfo();
                                    info.ProcessId = (int)pid;
                                    info.ProcessName = p.ProcessName;
                                    info.Volume = vol * 100f;
                                    info.IsMuted = isMuted;

                                    list.Add(info);
                                }
                            }
                            catch
                            {
                                // Процес міг завершитись
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine("Помилка отримання сесій: " + ex.Message); }
            return list;
        }

        // Встановити гучність за ID процесу (надійніше ніж за назвою)
        public void SetApplicationVolume(int processId, float levelPercent)
        {
            if (levelPercent < 0f) levelPercent = 0f;
            if (levelPercent > 100f) levelPercent = 100f;

            try
            {
                Type enumType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
                object enumeratorObject = Activator.CreateInstance(enumType);
                IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)enumeratorObject;

                IMMDevice device = null;
                enumerator.GetDefaultAudioEndpoint(0, 1, out device);

                Guid iidManager = typeof(IAudioSessionManager2).GUID;
                IntPtr ptrManager = IntPtr.Zero;
                device.Activate(ref iidManager, 23, IntPtr.Zero, out ptrManager);
                IAudioSessionManager2 manager = (IAudioSessionManager2)Marshal.GetObjectForIUnknown(ptrManager);

                IAudioSessionEnumerator sessionEnum;
                manager.GetSessionEnumerator(out sessionEnum);

                int count;
                sessionEnum.GetCount(out count);

                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl control;
                    sessionEnum.GetSession(i, out control);
                    IAudioSessionControl2 control2 = control as IAudioSessionControl2;

                    if (control2 != null)
                    {
                        uint pid;
                        control2.GetProcessId(out pid);

                        if (pid == processId)
                        {
                            ISimpleAudioVolume volume = control2 as ISimpleAudioVolume;
                            if (volume != null)
                            {
                                volume.SetMasterVolume(levelPercent / 100f, IntPtr.Zero);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine("Помилка встановлення гучності App: " + ex.Message); }
        }

        // Встановити Mute за ID процесу
        public void SetApplicationMute(int processId, bool mute)
        {
            try
            {
                Type enumType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
                object enumeratorObject = Activator.CreateInstance(enumType);
                IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)enumeratorObject;

                IMMDevice device = null;
                enumerator.GetDefaultAudioEndpoint(0, 1, out device);

                Guid iidManager = typeof(IAudioSessionManager2).GUID;
                IntPtr ptrManager = IntPtr.Zero;
                device.Activate(ref iidManager, 23, IntPtr.Zero, out ptrManager);
                IAudioSessionManager2 manager = (IAudioSessionManager2)Marshal.GetObjectForIUnknown(ptrManager);

                IAudioSessionEnumerator sessionEnum;
                manager.GetSessionEnumerator(out sessionEnum);

                int count;
                sessionEnum.GetCount(out count);

                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl control;
                    sessionEnum.GetSession(i, out control);
                    IAudioSessionControl2 control2 = control as IAudioSessionControl2;

                    if (control2 != null)
                    {
                        uint pid;
                        control2.GetProcessId(out pid);

                        if (pid == processId)
                        {
                            ISimpleAudioVolume volume = control2 as ISimpleAudioVolume;
                            if (volume != null)
                            {
                                volume.SetMute(mute, IntPtr.Zero);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine("Помилка встановлення Mute App: " + ex.Message); }
        }

        public void Dispose()
        {
            if (_renderVolumeObject != null)
            {
                if (_renderCallback != null)
                {
                    _renderVolumeObject.UnregisterControlChangeNotify(_renderCallback);
                    _renderCallback = null;
                }
                Marshal.ReleaseComObject(_renderVolumeObject);
                _renderVolumeObject = null;
            }

            if (_captureVolumeObject != null)
            {
                if (_captureCallback != null)
                {
                    _captureVolumeObject.UnregisterControlChangeNotify(_captureCallback);
                    _captureCallback = null;
                }
                Marshal.ReleaseComObject(_captureVolumeObject);
                _captureVolumeObject = null;
            }
        }
    }
}