using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;

namespace FirmataPRSBTestApp
{
    class Program
    {
        private static List<(string PortName, bool IsFirmata, string Version, string Profile)> _portScanResults = new();
        private static FirmataClient? _device;
        private static bool _keepRunning = true;
        private static bool _includeCom1 = false;

        static void Main(string[] args)
        {
            Console.WriteLine("Arduino Firmata Client - Smart Detection");
            Console.WriteLine("=========================================");

            while (_keepRunning)
            {
                // Scan for available ports
                ScanPorts(_includeCom1);

                // Let user choose a device from the FIRMATA devices only
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

                // Connect to selected device using the known working profile
                var chosenDevice = firmataDevices[selected];
                _device = new FirmataClient(chosenDevice.PortName, chosenDevice.Profile);

                if (_device.Connect(1)) // Only 1 round needed since we know the profile
                {
                    Console.WriteLine($"\nConnected to {chosenDevice.PortName} (Firmata v{_device.Version})");
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

        private static void ScanPorts(bool includeCom1 = false)
        {
            _portScanResults.Clear();
            var ports = SerialPort.GetPortNames()
                .OrderBy(p => p)
                .ToArray();

            Console.WriteLine("\nAvailable COM Ports:");
            foreach (var port in ports)
            {
                Console.WriteLine($"  {port}");
            }

            if (ports.Length == 0)
            {
                Console.WriteLine("No COM ports found!");
                return;
            }

            // Filter ports based on includeCom1 flag
            var portsToScan = includeCom1
                ? ports
                : ports.Where(p => !p.Equals("COM1", StringComparison.OrdinalIgnoreCase)).ToArray();

            if (!includeCom1 && ports.Contains("COM1", StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine("\nNote: COM1 is skipped by default (typically not used for Arduino devices)");
            }

            Console.WriteLine("\nScanning for Firmata devices...");

            foreach (var port in portsToScan)
            {
                Console.WriteLine($"\n=== Testing {port} ===");

                try
                {
                    using var testClient = new FirmataClient(port);
                    bool isConnected = testClient.Connect(2);

                    _portScanResults.Add((port, isConnected,
                        isConnected ? testClient.Version : "",
                        isConnected ? testClient.DetectedProfile : ""));

                    if (isConnected)
                    {
                        Console.WriteLine($"✓ Firmata v{testClient.Version} detected ({testClient.DetectedProfile} profile)");
                    }
                    else
                    {
                        Console.WriteLine("✗ No Firmata device found");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error testing {port}: {ex.Message}");
                    _portScanResults.Add((port, false, "", ""));
                }

                Thread.Sleep(500);
            }

            Console.WriteLine("\nScan complete.");
        }

        private static int SelectDevice(List<(string PortName, bool IsFirmata, string Version, string Profile)> firmataDevices)
        {
            Console.WriteLine("\nFirmata Devices Found:");
            for (int i = 0; i < firmataDevices.Count; i++)
            {
                var device = firmataDevices[i];
                Console.WriteLine($"{i}: {device.PortName} (v{device.Version}) - {device.Profile} profile");
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