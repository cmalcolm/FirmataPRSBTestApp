using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;

namespace FirmataPRSBTestApp
{
    class Program
    {
        private static List<(string PortName, bool IsFirmata, string Version, string Profile, string DeviceType)> _portScanResults = new();
        private static FirmataClient? _device;
        private static bool _keepRunning = true;
        private static bool _includeCom1 = false;

        
        static void Main(string[] args)
        {
            Console.WriteLine("Arduino Firmata Client - Smart Registry Detection");
            Console.WriteLine("=================================================");

            while (_keepRunning)
            {
                // Scan for devices using Registry first
                ScanPortsWithRegistry();

                // Let user choose a device from the detected devices
                var firmataDevices = _portScanResults.Where(p => p.IsFirmata).ToList();

                if (firmataDevices.Count == 0)
                {
                    Console.WriteLine("No Firmata devices found.");
                    Console.WriteLine("1. Rescan ports");
                    Console.WriteLine("2. Exit");
                    Console.Write("Select option: ");

                    var choice = Console.ReadLine();
                    if (choice == "2")
                    {
                        _keepRunning = false;
                    }
                    continue;
                }

                int selected = SelectDevice(firmataDevices);
                if (selected == -1)
                {
                    _keepRunning = false;
                    continue;
                }

                // Connect to selected device
                var chosenDevice = firmataDevices[selected];
                _device = new FirmataClient(chosenDevice.PortName, chosenDevice.Profile);


                // Use device type information for better connection
                bool connected = chosenDevice.DeviceType != "Unknown"
                    ? _device.Connect(1, chosenDevice.DeviceType)
                    : _device.Connect(1);

                if (connected)
                {
                    Console.WriteLine($"\nConnected to {chosenDevice.PortName} ({chosenDevice.DeviceType})");
                    Console.WriteLine($"Firmata v{_device.Version} detected");
                    RunDeviceMenu();
                }
                else
                {
                    Console.WriteLine("Failed to connect to the selected device.");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                }

                _device?.Dispose();
                _device = null;
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
        
        private static void ScanPortsWithRegistry(bool includeCom1 = false)
        {
            _portScanResults.Clear();

            // First, get devices from Windows Registry
            Console.WriteLine("Querying Windows for connected devices...");
            var registryDevices = WmiDeviceFinder.GetConnectedArduinoDevices();

            if (registryDevices.Count > 0)
            {
                Console.WriteLine("\nWindows-detected devices:");
                foreach (var device in registryDevices)
                {
                    Console.WriteLine($"  {device.Key}: {device.Value}");
                }
            }
            else
            {
                Console.WriteLine("No Arduino devices found in Windows Registry.");
            }

            // Get all COM ports
            var allPorts = SerialPort.GetPortNames()
                .OrderBy(p => p)
                .ToArray();

            if (allPorts.Length == 0)
            {
                Console.WriteLine("No COM ports found!");
                return;
            }

            // Filter ports
            var portsToScan = includeCom1
                ? allPorts
                : allPorts.Where(p => !p.Equals("COM1", StringComparison.OrdinalIgnoreCase)).ToArray();

            Console.WriteLine("\nScanning for Firmata devices...");

            foreach (var port in portsToScan)
            {
                Console.WriteLine($"\n=== Testing {port} ===");

                // Check if Windows knows about this device
                string deviceType = "Unknown";
                if (registryDevices.TryGetValue(port, out var knownDevice))
                {
                    deviceType = knownDevice;
                    Console.WriteLine($"Windows identifies: {deviceType}");
                }

                try
                {
                    using var testClient = new FirmataClient(port);
                    bool isConnected;

                    if (deviceType != "Unknown")
                    {
                        // Use device-specific connection
                        isConnected = testClient.ConnectWithDeviceType(deviceType);
                    }
                    else
                    {
                        // Fallback to auto-detection
                        isConnected = testClient.Connect(2);
                    }

                    _portScanResults.Add((port, isConnected,
                        isConnected ? testClient.Version : "",
                        isConnected ? testClient.DetectedProfile : "",
                        deviceType));

                    if (isConnected)
                    {
                        Console.WriteLine($"✓ Firmata v{testClient.Version} detected");
                    }
                    else
                    {
                        Console.WriteLine("✗ No Firmata response");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error testing {port}: {ex.Message}");
                    _portScanResults.Add((port, false, "", "", deviceType));
                }

                Thread.Sleep(300); // Shorter delay since we're smarter now
            }

            Console.WriteLine("\nScan complete.");
        }
        public static Dictionary<string, string> SafeGetRegistryDevices()
        {
            try
            {
                return WmiDeviceFinder.GetConnectedArduinoDevices();
            }
            catch (System.Security.SecurityException)
            {
                Console.WriteLine("Note: Registry access denied. Using fallback detection.");
                return new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Registry access error: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }
        private static int SelectDevice(List<(string PortName, bool IsFirmata, string Version, string Profile, string DeviceType)> firmataDevices)
        {
            Console.WriteLine("\nFirmata Devices Found:");
            for (int i = 0; i < firmataDevices.Count; i++)
            {
                var device = firmataDevices[i];
                Console.WriteLine($"{i}: {device.PortName} - {device.DeviceType} (v{device.Version})");
            }

            Console.WriteLine($"{firmataDevices.Count}: Rescan all ports");
            Console.WriteLine($"{firmataDevices.Count + 1}: Exit application");

            int selected = -1;
            while (selected < 0 || selected > firmataDevices.Count + 1)
            {
                Console.Write($"\nSelect option (0-{firmataDevices.Count + 1}): ");
                if (!int.TryParse(Console.ReadLine(), out selected) || selected < 0 || selected > firmataDevices.Count + 1)
                {
                    Console.WriteLine("Invalid selection.");
                    selected = -1;
                }
            }

            if (selected == firmataDevices.Count) // Rescan option
            {
                return -2; // Special code for rescan
            }
            else if (selected == firmataDevices.Count + 1) // Exit option
            {
                return -1; // Exit
            }

            return selected;
        }

        private static void RunDeviceMenu()
        {
            bool deviceMenuRunning = true;

            while (deviceMenuRunning && _keepRunning)
            {
                Console.WriteLine("\n=== DEVICE MENU ===");
                Console.WriteLine($"Connected to: {_device?.PortName} (Firmata v{_device?.Version})");
                Console.WriteLine("1. Set Digital Pin Output");
                Console.WriteLine("2. Set PWM Output");
                Console.WriteLine("3. Set Servo Position");
                Console.WriteLine("4. Read Version");
                Console.WriteLine("5. Test All Functions");
                Console.WriteLine("6. Switch to different device");
                Console.WriteLine("7. Rescan ports");
                Console.WriteLine("8. Exit application");
                Console.WriteLine("9. Toggle COM1 scanning (Currently: {0})", _includeCom1 ? "Enabled" : "Disabled");
                Console.Write("Select option: ");

                var input = Console.ReadLine()?.ToLower();

                switch (input)
                {
                    case "1": SetDigitalPinOutput(); break;
                    case "2": SetPwmOutput(); break;
                    case "3": SetServoPosition(); break;
                    case "4": ReadVersion(); break;
                    case "5": TestAllFunctions(); break;
                    case "6":
                        deviceMenuRunning = false; // Break out to device selection
                        break;
                    case "7":
                        deviceMenuRunning = false;
                        _portScanResults.Clear(); // Force rescan
                        break;
                    case "8":
                        deviceMenuRunning = false;
                        _keepRunning = false;
                        break;
                    case "9":
                        _includeCom1 = !_includeCom1;
                        Console.WriteLine($"COM1 scanning is now {(_includeCom1 ? "enabled" : "disabled")}");
                        break;
                    default:
                        Console.WriteLine("Invalid option");
                        break;
                }
            }
        }

        private static void SetDigitalPinOutput()
        {
            if (_device == null) return;

            Console.Write("Enter pin number (0-13): ");
            if (!int.TryParse(Console.ReadLine(), out int pin) || pin < 0 || pin > 13)
            {
                Console.WriteLine("Invalid pin number.");
                return;
            }

            Console.Write("Enter value (0 or 1): ");
            if (!int.TryParse(Console.ReadLine(), out int value) || (value != 0 && value != 1))
            {
                Console.WriteLine("Invalid value. Must be 0 or 1.");
                return;
            }

            try
            {
                _device.SetDigitalPinOutput(pin, value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting pin: {ex.Message}");
            }
        }

        private static void SetPwmOutput()
        {
            if (_device == null) return;

            Console.Write("Enter PWM pin number: ");
            if (!int.TryParse(Console.ReadLine(), out int pin))
            {
                Console.WriteLine("Invalid pin number.");
                return;
            }

            Console.Write("Enter PWM value (0-255): ");
            if (!int.TryParse(Console.ReadLine(), out int value) || value < 0 || value > 255)
            {
                Console.WriteLine("Invalid value. Must be between 0 and 255.");
                return;
            }

            try
            {
                _device.SetPwmOutput(pin, value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting PWM: {ex.Message}");
            }
        }

        private static void SetServoPosition()
        {
            if (_device == null) return;

            Console.Write("Enter servo pin number: ");
            if (!int.TryParse(Console.ReadLine(), out int pin))
            {
                Console.WriteLine("Invalid pin number.");
                return;
            }

            Console.Write("Enter position (0-180 degrees): ");
            if (!int.TryParse(Console.ReadLine(), out int angle) || angle < 0 || angle > 180)
            {
                Console.WriteLine("Invalid angle. Must be between 0 and 180 degrees.");
                return;
            }

            try
            {
                _device.SetServoPosition(pin, angle);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting servo: {ex.Message}");
            }
        }

        private static void ReadVersion()
        {
            if (_device == null) return;

            try
            {
                _device.ReadVersion();
                Thread.Sleep(1000); // Wait for response
                Console.WriteLine($"Firmata version: {_device.Version}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading version: {ex.Message}");
            }
        }

        private static void TestAllFunctions()
        {
            if (_device == null) return;

            Console.WriteLine("\nTesting all functions...");

            try
            {
                // Test digital output (pin 13 usually has LED)
                Console.WriteLine("Testing digital output on pin 13...");
                _device.SetDigitalPinOutput(13, 1);
                Thread.Sleep(1000);
                _device.SetDigitalPinOutput(13, 0);

                // Test PWM (pin 9 or 10 typically)
                Console.WriteLine("Testing PWM on pin 9...");
                for (int i = 0; i <= 255; i += 10)
                {
                    _device.SetPwmOutput(9, i);
                    Thread.Sleep(50);
                }
                _device.SetPwmOutput(9, 0);

                // Test servo (pin 5 typically)
                Console.WriteLine("Testing servo on pin 5...");
                for (int angle = 0; angle <= 180; angle += 10)
                {
                    _device.SetServoPosition(5, angle);
                    Thread.Sleep(100);
                }
                _device.SetServoPosition(5, 90);

                Console.WriteLine("All tests completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test failed: {ex.Message}");
            }
        }
    }
}