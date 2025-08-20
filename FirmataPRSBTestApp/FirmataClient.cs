using System;
using System.IO.Ports;
using System.Threading;

namespace FirmataPRSBTestApp
{
    /// <summary>
    /// Represents a connection to an Arduino board running the Firmata protocol.
    /// Provides methods to control digital, PWM, and servo pins, and to query the firmware version.
    /// </summary>
    public class FirmataClient : IDisposable
    {
        private SerialPort _port;
        private bool _firmataDetected;
        public string PortName { get; }
        public bool IsConnected => _port?.IsOpen ?? false;
        public string Version { get; private set; } = "Unknown";

        // Firmata command bytes
        private const byte DIGITAL_MESSAGE = 0x90;
        private const byte ANALOG_MESSAGE = 0xE0;
        private const byte SET_PIN_MODE = 0xF4;
        private const byte REPORT_VERSION = 0xF9;
        private const byte PIN_MODE_OUTPUT = 0x01;
        private const byte PIN_MODE_PWM = 0x03;
        private const byte PIN_MODE_SERVO = 0x04;

        public FirmataClient(string portName)
        {
            PortName = portName;
        }

        //public bool Connect()
        //{
        //    try
        //    {
        //        Console.WriteLine($"Testing {PortName}: [Connect] Releasing port if stuck...");
        //        ForceReleasePort(PortName);

        //        Console.WriteLine($"Testing {PortName}: [Connect] Creating SerialPort instance...");
        //        _port = new SerialPort(PortName, 115200, Parity.None, 8, StopBits.One)
        //        {
        //            DtrEnable = true,
        //            RtsEnable = true
        //        };
        //        _port.DataReceived += Port_DataReceived;

        //        Console.WriteLine($"Testing {PortName}: [Connect] Opening serial port...");
        //        _port.Open();

        //        Console.WriteLine($"Testing {PortName}: [Connect] Waiting for Arduino to boot (3s)...");
        //        Thread.Sleep(3000);

        //        Console.WriteLine($"Testing {PortName}: [Connect] Discarding any startup data...");
        //        _port.DiscardInBuffer();

        //        _firmataDetected = false;
        //        Console.WriteLine($"Testing {PortName}: [Connect] Sending Firmata version request: F9");
        //        _port.Write(new byte[] { REPORT_VERSION }, 0, 1);

        //        for (int i = 0; i < 20; i++)
        //        {
        //            Console.WriteLine($"Testing {PortName}: [Connect] Waiting for Firmata reply... ({i + 1}/20)");
        //            Thread.Sleep(250);
        //            if (_firmataDetected)
        //            {
        //                Console.WriteLine($"Testing {PortName}: [Connect] Firmata version reply received.");
        //                break;
        //            }
        //            if (i % 4 == 0)
        //            {
        //                Console.WriteLine($"Testing {PortName}: [Connect] Re-sending version request: F9");
        //                _port.Write(new byte[] { REPORT_VERSION }, 0, 1);
        //            }
        //        }

        //        if (!_firmataDetected)
        //        {
        //            Console.WriteLine($"Testing {PortName}: [Connect] No Firmata reply received after waiting.");
        //        }

        //        return _firmataDetected;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Testing {PortName}: [Connect] Exception: {ex.GetType().Name}: {ex.Message}");
        //        Close();
        //        return false;
        //    }
        //}

        public bool Connect()
        {
            const int maxAttempts = 10;
            const int retryDelayMs = 500;
            int attempt = 0;

            // Helper to check if port exists
            static bool PortExists(string portName)
                => Array.Exists(SerialPort.GetPortNames(), p => p == portName);

            while (attempt < maxAttempts)
            {
                try
                {
                    Console.WriteLine($"Testing {PortName}: [Connect] Attempt {attempt + 1}/{maxAttempts} - Releasing port if stuck...");
                    ForceReleasePort(PortName);

                    Console.WriteLine($"Testing {PortName}: [Connect] Creating SerialPort instance...");
                    _port = new SerialPort(PortName, 115200, Parity.None, 8, StopBits.One)
                    {
                        DtrEnable = true,
                        RtsEnable = true
                    };
                    _port.DataReceived += Port_DataReceived;

                    Console.WriteLine($"Testing {PortName}: [Connect] Opening serial port...");
                    _port.Open();

                    // Wait briefly to see if the port disappears (Leonardo/Micro)
                    bool portDisappeared = false;
                    int waited = 0;
                    for (; waited < 2000; waited += 100)
                    {
                        Thread.Sleep(100);
                        if (!PortExists(PortName))
                        {
                            portDisappeared = true;
                            break;
                        }
                    }

                    if (portDisappeared)
                    {
                        Console.WriteLine($"Testing {PortName}: [Connect] Port disappeared, waiting for it to reappear...");
                        waited = 0;
                        while (!PortExists(PortName) && waited < 5000)
                        {
                            Thread.Sleep(100);
                            waited += 100;
                        }
                        // After reappear, close and reopen the port
                        try { _port.Close(); } catch { }
                        _port.Dispose();
                        _port = new SerialPort(PortName, 115200, Parity.None, 8, StopBits.One)
                        {
                            DtrEnable = true,
                            RtsEnable = true
                        };
                        _port.DataReceived += Port_DataReceived;
                        _port.Open();
                    }

                    break; // Success!
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine($"Testing {PortName}: [Connect] Access denied (port in use or not ready). Retrying in {retryDelayMs}ms...");
                    Thread.Sleep(retryDelayMs);
                    attempt++;
                    if (_port != null)
                    {
                        try { _port.Dispose(); } catch { }
                        _port = null;
                    }
                    continue;
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"Testing {PortName}: [Connect] IO exception: {ex.Message}. Retrying in {retryDelayMs}ms...");
                    Thread.Sleep(retryDelayMs);
                    attempt++;
                    if (_port != null)
                    {
                        try { _port.Dispose(); } catch { }
                        _port = null;
                    }
                    continue;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Testing {PortName}: [Connect] Exception: {ex.GetType().Name}: {ex.Message}");
                    Close();
                    return false;
                }
            }

            if (_port == null || !_port.IsOpen)
            {
                Console.WriteLine($"Testing {PortName}: [Connect] Could not open port after {maxAttempts} attempts.");
                return false;
            }

            try
            {
                Console.WriteLine($"Testing {PortName}: [Connect] Waiting for Arduino to boot (3s)...");
                Thread.Sleep(3000);

                Console.WriteLine($"Testing {PortName}: [Connect] Discarding any startup data...");
                _port.DiscardInBuffer();

                _firmataDetected = false;
                Console.WriteLine($"Testing {PortName}: [Connect] Sending Firmata version request: F9");
                _port.Write(new byte[] { REPORT_VERSION }, 0, 1);

                for (int i = 0; i < 20; i++)
                {
                    Console.WriteLine($"Testing {PortName}: [Connect] Waiting for Firmata reply... ({i + 1}/20)");
                    Thread.Sleep(250);
                    if (_firmataDetected)
                    {
                        Console.WriteLine($"Testing {PortName}: [Connect] Firmata version reply received.");
                        break;
                    }
                    if (i % 4 == 0)
                    {
                        Console.WriteLine($"Testing {PortName}: [Connect] Re-sending version request: F9");
                        _port.Write(new byte[] { REPORT_VERSION }, 0, 1);
                    }
                }

                if (!_firmataDetected)
                {
                    Console.WriteLine($"Testing {PortName}: [Connect] No Firmata reply received after waiting.");
                }

                return _firmataDetected;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Testing {PortName}: [Connect] Exception after open: {ex.GetType().Name}: {ex.Message}");
                Close();
                return false;
            }
        }
        public void SetDigitalPinOutput(int pin, int value)
        {
            _port.Write(new byte[] { SET_PIN_MODE, (byte)pin, PIN_MODE_OUTPUT }, 0, 3);
            Thread.Sleep(100);
            byte port = (byte)(pin / 8);
            byte pinMask = (byte)(1 << (pin % 8));
            byte portValue = (byte)(value == 1 ? pinMask : 0);
            _port.Write(new byte[] { (byte)(DIGITAL_MESSAGE | port), (byte)(portValue & 0x7F), (byte)(portValue >> 7) }, 0, 3);
        }

        public void SetPwmOutput(int pin, int value)
        {
            _port.Write(new byte[] { SET_PIN_MODE, (byte)pin, PIN_MODE_PWM }, 0, 3);
            Thread.Sleep(100);
            _port.Write(new byte[] { (byte)(ANALOG_MESSAGE | pin), (byte)(value & 0x7F), (byte)(value >> 7) }, 0, 3);
        }

        private HashSet<int> _servoPins = new HashSet<int>();
        public void SetServoPosition(int pin, int angle)
        {
            if (!_servoPins.Contains(pin))
            {
                _port.Write(new byte[] { SET_PIN_MODE, (byte)pin, PIN_MODE_SERVO }, 0, 3);
                Thread.Sleep(100);
                _servoPins.Add(pin);
            }
            _port.Write(new byte[] {
                (byte)(ANALOG_MESSAGE | (pin & 0x0F)),
                (byte)(angle & 0x7F),
                (byte)(angle >> 7)
            }, 0, 3);
        }

        public void ReadVersion()
        {
            _port.Write(new byte[] { REPORT_VERSION }, 0, 1);
        }

        public void Close()
        {
            if (_port != null)
            {
                try
                {
                    if (_port.IsOpen)
                        _port.Close();
                    _port.Dispose();
                    _port = null;
                }
                catch { }
            }
        }

        public void Dispose() => Close();

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                int bytesToRead = _port.BytesToRead;
                if (bytesToRead > 0)
                {
                    byte[] buffer = new byte[bytesToRead];
                    _port.Read(buffer, 0, bytesToRead);

                    Console.WriteLine($"Testing {PortName}: [DataReceived] Received {bytesToRead} bytes: {BitConverter.ToString(buffer)}");

                    for (int i = 0; i < buffer.Length; i++)
                    {
                        byte b = buffer[i];
                        if (b == REPORT_VERSION && i + 2 < buffer.Length)
                        {
                            byte major = buffer[i + 1];
                            byte minor = buffer[i + 2];
                            Version = $"{major}.{minor}";
                            _firmataDetected = true;
                            Console.WriteLine($"Testing {PortName}: [DataReceived] Parsed Firmata version: {Version}");
                            i += 2;
                        }
                        else
                        {
                            Console.WriteLine($"Testing {PortName}: [DataReceived] Unhandled byte: {b:X2}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Testing {PortName}: [DataReceived] No data received.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Testing {PortName}: [DataReceived] Exception: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void ForceReleasePort(string portName)
        {
            try
            {
                using (SerialPort testPort = new SerialPort(portName, 9600))
                {
                    testPort.Open();
                    testPort.Close();
                }
            }
            catch { }
        }
    }
}