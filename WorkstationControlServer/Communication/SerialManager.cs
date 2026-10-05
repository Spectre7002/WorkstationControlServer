using System;
using System.IO.Ports;

namespace WorkstationControlServer.Communication
{
    public delegate void VolumeCommandReceivedHandler(float volume);
    public delegate void MuteCommandReceivedHandler(bool isMuted);

    public class SerialManager : IDisposable
    {
        private SerialPort _serialPort;

        public event VolumeCommandReceivedHandler VolumeCommandReceived;
        public event MuteCommandReceivedHandler MuteCommandReceived;

        public void Connect(string portName, int baudRate)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
            }

            _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
            _serialPort.DataReceived += SerialPort_DataReceived;
            _serialPort.Open();
        }

        public void Disconnect()
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }

        public bool IsConnected()
        {
            if (_serialPort != null)
            {
                return _serialPort.IsOpen;
            }
            return false;
        }

        public void SendTelemetryPacket(float cpuTemp, float cpuLoad, float gpuTemp, float gpuLoad, float ramUsage, float currentVolume, bool isMuted)
        {
            if (!IsConnected())
            {
                return;
            }

            try
            {
                int muteState = 0;
                if (isMuted)
                {
                    muteState = 1;
                }

                string packet = string.Format("T|{0}|{1}|{2}|{3}|{4}|{5}|{6}\n",
                    Math.Round(cpuTemp, 1),
                    Math.Round(cpuLoad, 1),
                    Math.Round(gpuTemp, 1),
                    Math.Round(gpuLoad, 1),
                    Math.Round(ramUsage, 1),
                    Math.Round(currentVolume, 0),
                    muteState);

                _serialPort.Write(packet);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Помилка відправки у COM-порт: " + ex.Message);
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = _serialPort.ReadLine();
                ParseCommand(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Помилка читання з COM-порту: " + ex.Message);
            }
        }

        private void ParseCommand(string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            data = data.Trim();

            if (data.StartsWith("CMD:VOL:"))
            {
                string valStr = data.Substring(8);
                float volume;
                if (float.TryParse(valStr, out volume))
                {
                    if (VolumeCommandReceived != null)
                    {
                        VolumeCommandReceived(volume);
                    }
                }
            }
            else if (data.StartsWith("CMD:MUTE:"))
            {
                string valStr = data.Substring(9);
                int muteState;
                if (int.TryParse(valStr, out muteState))
                {
                    bool isMuted = false;
                    if (muteState == 1)
                    {
                        isMuted = true;
                    }
                    if (MuteCommandReceived != null)
                    {
                        MuteCommandReceived(isMuted);
                    }
                }
            }
        }

        public void Dispose()
        {
            Disconnect();
            if (_serialPort != null)
            {
                _serialPort.Dispose();
                _serialPort = null;
            }
        }
    }
}