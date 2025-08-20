using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;

namespace FirmataPRSBTestApp
{
    class Program
    {
        private static List<(string PortName, bool IsFirmata, string Version)> _portScanResults = new();
        private static FirmataClient? _device;
        private static bool _keepRunning = true;

        static void Main(string[] args)
        {
            // 1. On startup, search for all COM ports and list them out on the console.
            var ports = SerialPort.GetPortNames();
            Console.WriteLine("Available COM Ports:");
            for (int i = 0; i < ports.Length; i++)
                Console.WriteLine($"{i}: {ports[i]}");

            // 2. Wait for the user to press a key.
            Console.WriteLine("\nPress any key to scan for Firmata devices...");
            Console.ReadKey(true);

            // 3. Test each COM port to see if it is a Firmata device, and provide an updated list.
            _portScanResults.Clear();
            for (int i = 0; i < ports.Length; i++)
            {
                string port = ports[i];
                Console.WriteLine($"Testing {port}: Starting scan...");
                Console.WriteLine($"Testing {port}: Creating FirmataClient instance...");
                using var testClient = new FirmataClient(port);
                bool opened = false;
                try
                {
                    Console.WriteLine($"Testing {port}: Attempting to open serial port...");
                    // Connect() will open the port and start handshake
                    Console.WriteLine($"Testing {port}: Calling Connect() to initiate handshake...");
                    opened = testClient.Connect();
                    if (opened)
                    {
                        Console.WriteLine($"Testing {port}: Handshake complete, Firmata detected.");
                        Console.WriteLine($"Testing {port}: Reading Firmata version...");
                        Console.WriteLine($"Testing {port}: Detected Firmata version {testClient.Version}.");
                        _portScanResults.Add((port, true, testClient.Version));
                        Console.WriteLine($"Testing {port}: [X] Firmata device confirmed.");
                    }
                    else
                    {
                        Console.WriteLine($"Testing {port}: No Firmata response or handshake failed.");
                        _portScanResults.Add((port, false, ""));
                        Console.WriteLine($"Testing {port}: [ ] Not a Firmata device.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Testing {port}: Exception occurred: {ex.GetType().Name}: {ex.Message}");
                    if (ex.InnerException != null)
                        Console.WriteLine($"Testing {port}: Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                    _portScanResults.Add((port, false, ""));
                    Console.WriteLine($"Testing {port}: [ ] Not a Firmata device.");
                }
                finally
                {
                    Console.WriteLine($"Testing {port}: Disposing FirmataClient and closing serial port.");
                    // The using statement ensures disposal
                }
                Console.WriteLine($"Testing {port}: Scan complete.\n");
            }

            // 4. At this stage, no device should be connected to app, but we now know what we have connected and available.

            // 5. Ask the user to choose a device, and then connect to it.
            int selected = -1;
            while (selected < 0 || selected >= _portScanResults.Count || !_portScanResults[selected].IsFirmata)
            {
                Console.Write("\nEnter the number of a Firmata device to connect: ");
                if (!int.TryParse(Console.ReadLine(), out selected) ||
                    selected < 0 || selected >= _portScanResults.Count ||
                    !_portScanResults[selected].IsFirmata)
                {
                    Console.WriteLine("Invalid selection. Please choose a valid Firmata device.");
                    selected = -1;
                }
            }

            // 6. Show status message for that selected device (Firmata version, connected status)
            var chosenPort = _portScanResults[selected].PortName;
            _device = new FirmataClient(chosenPort);
            if (_device.Connect())
            {
                Console.WriteLine($"\nConnected to {chosenPort} (Firmata v{_device.Version})");
            }
            else
            {
                Console.WriteLine("Failed to connect to the selected device.");
                return;
            }

            // 7. Provide the menu to allow the user to configure/test pins/servos on that device.
            while (_keepRunning)
            {
                ShowMainMenu();
            }

            _device.Close();
        }

        private static void ShowMainMenu()
        {
            Console.WriteLine("\nAvailable Commands:");
            Console.WriteLine("1. Set Digital Pin Output");
            Console.WriteLine("2. Set PWM (Analog) Output");
            Console.WriteLine("3. Set Servo Position (0-180)");
            Console.WriteLine("4. Read Version");
            Console.WriteLine("q. Quit");
            Console.Write("Enter command: ");

            string input = Console.ReadLine();

            switch (input?.ToLower())
            {
                case "1":
                    SetDigitalPinOutput();
                    break;
                case "2":
                    SetPwmOutput();
                    break;
                case "3":
                    SetServoPosition();
                    break;
                case "4":
                    ReadVersion();
                    break;
                case "q":
                    _keepRunning = false;
                    break;
                default:
                    Console.WriteLine("Invalid command.");
                    break;
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
                Console.WriteLine($"Set pin {pin} to {value}");
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
                Console.WriteLine($"Set PWM pin {pin} to {value}");
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
                Console.WriteLine($"Set servo on pin {pin} to {angle} degrees");
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
                Thread.Sleep(1000);
                Console.WriteLine($"Firmata version: {_device.Version}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error requesting version: {ex.Message}");
            }
        }
    }
}