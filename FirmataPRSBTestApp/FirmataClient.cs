using System;
using System.IO.Ports;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FirmataPRSBTestApp
{
    public class FirmataClient : IDisposable
    {
        private SerialPort _port;
        private bool _disposed = false;
        private bool _firmataDetected = false;
        private string _successfulProfile = null;

        public string PortName { get; }
        public bool IsConnected => _port?.IsOpen ?? false;
        public string Version { get; private set; } = "Unknown";
        public string DetectedProfile => _successfulProfile;
        public string DetectedDeviceType { get; private set; } = "Unknown";

        // Firmata command bytes
        private const byte REPORT_VERSION = 0xF9;
        private const byte SET_PIN_MODE = 0xF4;
        private const byte DIGITAL_MESSAGE = 0x90;
        private const byte ANALOG_MESSAGE = 0xE0;

        // Pin modes
        private const byte PIN_MODE_OUTPUT = 0x01;
        private const byte PIN_MODE_PWM = 0x03;
        private const byte PIN_MODE_SERVO = 0x04;

        private HashSet<int> _configuredPins = new HashSet<int>();

        public FirmataClient(string portName, string knownProfile = null)
        {
            PortName = portName;
            _successfulProfile = knownProfile; // Remember which profile worked before
        }

        public bool ConnectWithDeviceType(string deviceType)
        {
            DetectedDeviceType = deviceType;

            // Use device-specific connection strategy
            return deviceType.ToLower() switch
            {
                var t when t.Contains("mega") => ConnectWithProfile("Mega", 3),
                var t when t.Contains("leonardo") => ConnectWithProfile("Leonardo", 3),
                var t when t.Contains("esp8266") || t.Contains("esp32") => ConnectWithProfile("ESP8266", 3),
                var t when t.Contains("nano") => ConnectWithProfile("Standard", 2),
                var t when t.Contains("uno") => ConnectWithProfile("Standard", 2),
                _ => Connect(2) // Fallback to auto-detection
            };
        }

        private bool ConnectWithProfile(string profile, int attempts)
        {
            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    if (TryConnectWithProfile(profile))
                    {
                        _successfulProfile = profile;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Attempt {i + 1} failed: {ex.Message}");
                }

                if (i < attempts - 1)
                {
                    Thread.Sleep(1000);
                    Close();
                }
            }

            return false;
        }

        public bool Connect(int maxRounds = 2, string preferredProfile = null)
        {
            // If we already know which profile works, use it directly
            if (!string.IsNullOrEmpty(_successfulProfile))
            {
                Console.WriteLine($"Using known successful profile: {_successfulProfile}");
                return TryConnectWithProfile(_successfulProfile, 3);
            }

            // List all profiles
            var allProfiles = new List<string> { "Standard", "Mega", "Leonardo", "ESP8266" };

            // If a preferred profile is provided, move it to the front
            if (!string.IsNullOrEmpty(preferredProfile))
            {
                var match = allProfiles.FirstOrDefault(p => preferredProfile.ToLower().Contains(p.ToLower()));
                if (match != null)
                {
                    allProfiles.Remove(match);
                    allProfiles.Insert(0, match);
                }
            }

            for (int round = 1; round <= maxRounds; round++)
            {
                Console.WriteLine($"\n=== Connection Round {round}/{maxRounds} ===");

                foreach (var profile in allProfiles)
                {
                    try
                    {
                        Console.WriteLine($"Trying {profile} profile...");

                        if (TryConnectWithProfile(profile, 1))
                        {
                            _successfulProfile = profile;
                            Console.WriteLine($"✓ Success! Connected using {profile} profile");
                            return true;
                        }

                        Console.WriteLine($"✗ {profile} profile failed in round {round}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error with {profile} profile: {ex.Message}");
                        Close();
                    }

                    Thread.Sleep(500);
                }

                if (round < maxRounds)
                {
                    Console.WriteLine("Waiting before next round...");
                    Thread.Sleep(1000);
                }
            }

            Console.WriteLine($"✗ Failed to connect to {PortName} after {maxRounds} rounds");
            return false;
        }

       
        private bool TryConnectWithProfile(string profile, int maxAttempts = 1)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                Close(); // Clean up any previous connection

                if (!SerialPort.GetPortNames().Contains(PortName))
                {
                    Console.WriteLine($"Port {PortName} not available");
                    return false;
                }

                try
                {
                    _port = new SerialPort(PortName, 115200)
                    {
                        DtrEnable = true,
                        RtsEnable = true,
                        ReadTimeout = 5000,
                        WriteTimeout = 3000,
                        NewLine = "\n"
                    };

                    _port.DataReceived += Port_DataReceived;

                    // Apply profile-specific connection strategy
                    switch (profile.ToLower())
                    {
                        case "leonardo":
                            if (attempt == 1) Console.WriteLine("Applying Leonardo profile (open/close reset)");
                            _port.Open();
                            Thread.Sleep(100);
                            _port.Close();
                            Thread.Sleep(1500);
                            _port.Open();
                            Thread.Sleep(3000);
                            break;

                        case "mega":
                            if (attempt == 1) Console.WriteLine("Applying Mega profile (long reset wait)");
                            _port.Open();
                            Thread.Sleep(4500);
                            break;

                        case "esp8266":
                            if (attempt == 1) Console.WriteLine("Applying ESP8266 profile (advanced reset sequence)");

                            // Advanced ESP8266 reset sequence
                            _port.DtrEnable = false;
                            _port.RtsEnable = false;
                            _port.Open();

                            // Toggle DTR/RTS to ensure proper reset
                            Thread.Sleep(100);
                            _port.DtrEnable = true;
                            Thread.Sleep(100);
                            _port.DtrEnable = false;
                            Thread.Sleep(100);
                            _port.RtsEnable = true;
                            Thread.Sleep(100);
                            _port.RtsEnable = false;
                            Thread.Sleep(1500); // Wait for boot

                            // Sometimes need to close and reopen
                            _port.Close();
                            Thread.Sleep(500);
                            _port.Open();
                            Thread.Sleep(2000);
                            break;

                        default: // "standard"
                            if (attempt == 1) Console.WriteLine("Applying Standard profile");
                            _port.Open();
                            Thread.Sleep(2000);
                            break;
                    }

                    // Clear any buffered data
                    _port.DiscardInBuffer();
                    _port.DiscardOutBuffer();

                    // Try to detect Firmata
                    if (DetectFirmata(profile))
                    {
                        return true;
                    }

                    Close();
                }
                catch (Exception)
                {
                    Close();
                    if (attempt == maxAttempts) throw;
                }

                if (attempt < maxAttempts)
                {
                    Console.WriteLine($"Retrying {profile} profile (attempt {attempt + 1}/{maxAttempts})...");
                    Thread.Sleep(800);
                }
            }

            return false;
        }
        
        private bool DetectFirmata(string profile)
        {
            _firmataDetected = false;
            Version = "Unknown";

            // Adjust detection parameters based on profile
            int maxAttempts;
            int delayPerCheck;

            switch (profile.ToLower())
            {
                case "leonardo":
                    maxAttempts = 8;
                    delayPerCheck = 100;
                    break;
                case "mega":
                    maxAttempts = 12;
                    delayPerCheck = 200;
                    break;
                case "esp8266":
                    maxAttempts = 10;    // ESP8266 may respond differently
                    delayPerCheck = 150;
                    break;
                default:
                    maxAttempts = 6;
                    delayPerCheck = 150;
                    break;
            }

            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    if (_port == null || !_port.IsOpen) return false;

                    _port.Write(new byte[] { REPORT_VERSION }, 0, 1);

                    // Wait for response
                    for (int j = 0; j < 20; j++)
                    {
                        Thread.Sleep(delayPerCheck);
                        if (_firmataDetected)
                        {
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during detection: {ex.Message}");
                    return false;
                }
            }

            return false;
        }
        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_port == null || !_port.IsOpen) return;

                int bytesToRead = _port.BytesToRead;
                if (bytesToRead <= 0) return;

                byte[] buffer = new byte[bytesToRead];
                int bytesRead = _port.Read(buffer, 0, bytesToRead);

                // Parse for version response (0xF9 followed by major.minor)
                for (int i = 0; i < bytesRead - 2; i++)
                {
                    if (buffer[i] == REPORT_VERSION)
                    {
                        byte major = buffer[i + 1];
                        byte minor = buffer[i + 2];
                        Version = $"{major}.{minor}";
                        _firmataDetected = true;
                        Console.WriteLine($"✓ Detected Firmata version: {Version}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in data received: {ex.Message}");
            }
        }

        public void SetDigitalPinOutput(int pin, int value)
        {
            if (!IsConnected) throw new InvalidOperationException("Not connected");

            // Configure pin mode if not already done
            if (!_configuredPins.Contains(pin))
            {
                _port.Write(new byte[] { SET_PIN_MODE, (byte)pin, PIN_MODE_OUTPUT }, 0, 3);
                Thread.Sleep(50);
                _configuredPins.Add(pin);
            }

            // Send digital write command
            byte port = (byte)(pin / 8);
            byte pinMask = (byte)(1 << (pin % 8));
            byte portValue = (byte)(value == 1 ? pinMask : 0);

            _port.Write(new byte[] {
                (byte)(DIGITAL_MESSAGE | port),
                (byte)(portValue & 0x7F),
                (byte)(portValue >> 7)
            }, 0, 3);

            Console.WriteLine($"Set digital pin {pin} to {value}");
        }

        public void SetPwmOutput(int pin, int value)
        {
            if (!IsConnected) throw new InvalidOperationException("Not connected");

            // Configure pin mode if not already done
            if (!_configuredPins.Contains(pin))
            {
                _port.Write(new byte[] { SET_PIN_MODE, (byte)pin, PIN_MODE_PWM }, 0, 3);
                Thread.Sleep(50);
                _configuredPins.Add(pin);
            }

            // Send PWM value (0-255)
            value = Math.Max(0, Math.Min(255, value)); // Clamp value

            _port.Write(new byte[] {
                (byte)(ANALOG_MESSAGE | (pin & 0x0F)),
                (byte)(value & 0x7F),
                (byte)(value >> 7)
            }, 0, 3);

            Console.WriteLine($"Set PWM pin {pin} to {value}");
        }

        public void SetServoPosition(int pin, int angle)
        {
            if (!IsConnected) throw new InvalidOperationException("Not connected");

            // Configure pin mode if not already done
            if (!_configuredPins.Contains(pin))
            {
                _port.Write(new byte[] { SET_PIN_MODE, (byte)pin, PIN_MODE_SERVO }, 0, 3);
                Thread.Sleep(50);
                _configuredPins.Add(pin);
            }

            // Send servo angle (0-180)
            angle = Math.Max(0, Math.Min(180, angle)); // Clamp angle

            _port.Write(new byte[] {
                (byte)(ANALOG_MESSAGE | (pin & 0x0F)),
                (byte)(angle & 0x7F),
                (byte)(angle >> 7)
            }, 0, 3);

            Console.WriteLine($"Set servo pin {pin} to {angle}°");
        }

        public void ReadVersion()
        {
            if (!IsConnected) throw new InvalidOperationException("Not connected");

            _port.Write(new byte[] { REPORT_VERSION }, 0, 1);
            Console.WriteLine("Version request sent");
        }

        public void Close()
        {
            try
            {
                if (_port != null)
                {
                    _port.DataReceived -= Port_DataReceived;
                    if (_port.IsOpen)
                        _port.Close();
                    _port.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing port: {ex.Message}");
            }
            finally
            {
                _port = null;
                _configuredPins.Clear();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Close();
                }
                _disposed = true;
            }
        }
    }
}