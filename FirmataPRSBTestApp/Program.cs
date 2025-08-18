using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

//namespace ArduinoFirmataBasic
namespace FirmataPRSBTestApp
{
    class Program
    {
        private static SerialPort _port;
        private static bool _isConnected = false;
        private static bool _keepRunning = true;

        // Basic Firmata command bytes
        private const byte DIGITAL_MESSAGE = 0x90; // Send data for a digital port (collection of 8 pins)
        private const byte ANALOG_MESSAGE = 0xE0; // Send data for an analog pin
        private const byte REPORT_ANALOG = 0xC0; // Enable analog input by pin
        private const byte REPORT_DIGITAL = 0xD0; // Enable digital input by port
        private const byte SET_PIN_MODE = 0xF4; // Set pin mode
        private const byte START_SYSEX = 0xF0; // Start a MIDI SysEx message
        private const byte END_SYSEX = 0xF7; // End a MIDI SysEx message
        private const byte SERVO_CONFIG = 0x70; // Servo config (within SysEx)
        private const byte REPORT_VERSION = 0xF9; // Report firmware version

        // Pin modes
        private const byte PIN_MODE_INPUT = 0x00;
        private const byte PIN_MODE_OUTPUT = 0x01;
        private const byte PIN_MODE_ANALOG = 0x02;
        private const byte PIN_MODE_PWM = 0x03;
        private const byte PIN_MODE_SERVO = 0x04;

        static void Main(string[] args)
        {
            Console.WriteLine("Arduino Firmata Basic Tester");
            Console.WriteLine("==========================");

            // Start device search in background thread
            Task.Run(() => SearchForDevice());

            // Process commands from user
            while (_keepRunning)
            {
                if (_isConnected)
                {
                    ShowMainMenu();
                }
                else
                {
                    Console.WriteLine("Searching for Arduino device...");
                    Thread.Sleep(1000);
                }
            }

            // Clean up
            ClosePort();
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
                // Set pin mode to output
                _port.Write(new byte[] { SET_PIN_MODE, (byte)pin, PIN_MODE_OUTPUT }, 0, 3);
                Thread.Sleep(100);

                // Calculate port and pin mask
                byte port = (byte)(pin / 8);
                byte pinMask = (byte)(1 << (pin % 8));
                byte portValue = (byte)(value == 1 ? pinMask : 0);

                // Send digital message
                _port.Write(new byte[] { (byte)(DIGITAL_MESSAGE | port), (byte)(portValue & 0x7F), (byte)(portValue >> 7) }, 0, 3);
                Console.WriteLine($"Set pin {pin} to {value}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting pin: {ex.Message}");
            }
        }

        private static void SetPwmOutput()
        {
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
                // Set pin mode to PWM
                _port.Write(new byte[] { SET_PIN_MODE, (byte)pin, PIN_MODE_PWM }, 0, 3);
                Thread.Sleep(100);

                // Send analog message
                _port.Write(new byte[] { (byte)(ANALOG_MESSAGE | pin), (byte)(value & 0x7F), (byte)(value >> 7) }, 0, 3);
                Console.WriteLine($"Set PWM pin {pin} to {value}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting PWM: {ex.Message}");
            }
        }

        private static void SetServoPosition()
        {
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
                // Set pin mode to SERVO
                _port.Write(new byte[] { SET_PIN_MODE, (byte)pin, PIN_MODE_SERVO }, 0, 3);
                Thread.Sleep(100);

                // Send servo position using analog message format
                _port.Write(new byte[] { 
                    (byte)(ANALOG_MESSAGE | (pin & 0x0F)), 
                    (byte)(angle & 0x7F), 
                    (byte)(angle >> 7) 
                }, 0, 3);
                
                Console.WriteLine($"Set servo on pin {pin} to {angle} degrees");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting servo: {ex.Message}");
            }
        }

        private static void ReadVersion()
        {
            try
            {
                Console.WriteLine("Requesting Firmata version...");
                _port.Write(new byte[] { REPORT_VERSION }, 0, 1);
                Thread.Sleep(1000);  // Give time for response
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error requesting version: {ex.Message}");
            }
        }

        private static void SearchForDevice()
        {
            while (_keepRunning)
            {
                if (!_isConnected)
                {
                    firmataDetected = false;
                    string[] ports = SerialPort.GetPortNames();
                    Console.WriteLine($"Found {ports.Length} ports to try: {string.Join(", ", ports)}");

                    List<string> orderedPorts = new List<string>();
                    // Try all ports - Leonardo needs special handling
                    foreach (string port in ports)
                    {
                        orderedPorts.Add(port);
                    }

                    foreach (string portName in orderedPorts)
                    {
                        try
                        {
                            Console.WriteLine($"Trying port {portName}...");
                            ClosePort();

                            ForceReleasePort(portName);

                            // Now connect at Firmata speed
                            _port = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One)
                            {
                                DtrEnable = true, // These signals help with Leonardo
                                RtsEnable = true
                            };
                            
                            _port.DataReceived += Port_DataReceived;
                            _port.Open();
                            Console.WriteLine("Connection opened...");
                            
                            // Give Leonardo extra time to boot after opening connection
                            Thread.Sleep(3000);
                            
                            // Clear any startup messages
                            _port.DiscardInBuffer();
                            
                            Console.WriteLine("Sending version request...");
                            _port.Write(new byte[] { REPORT_VERSION }, 0, 1);
                            
                            // Wait longer for Leonardo (up to 5 seconds)
                            for (int i = 0; i < 20; i++)
                            {
                                Thread.Sleep(250);
                                if (firmataDetected)
                                    break;
                                
                                // Try sending the version request again every second
                                if (i % 4 == 0)
                                {
                                    Console.WriteLine("Re-sending version request...");
                                    _port.Write(new byte[] { REPORT_VERSION }, 0, 1);
                                }
                            }
                            
                            if (firmataDetected)
                            {
                                Console.WriteLine($"Arduino with Firmata confirmed on port {portName}!");
                                _isConnected = true;
                                break;
                            }
                            else
                            {
                                Console.WriteLine($"No Firmata response on port {portName}, not an Arduino");
                                ClosePort();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error with port {portName}: {ex.Message}");
                            ClosePort();
                        }
                    }

                    if (!_isConnected)
                    {
                        Console.WriteLine("No Arduino found. Will retry in 5 seconds...");
                        Console.WriteLine("For Arduino Leonardo:");
                        Console.WriteLine("1. Try pressing reset button right before scan starts");
                        Console.WriteLine("2. Check Arduino IDE port assignment");
                        Console.WriteLine("3. Try using 115200 baud in StandardFirmata");
                        Thread.Sleep(5000);
                    }
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }
        }

        private static bool firmataDetected = false;

        private static void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                SerialPort sp = (SerialPort)sender;
                int bytesToRead = sp.BytesToRead;

                if (bytesToRead > 0)
                {
                    byte[] buffer = new byte[bytesToRead];
                    sp.Read(buffer, 0, bytesToRead);

                    Console.WriteLine($"Raw data: {BitConverter.ToString(buffer)}");

                    // Parse Firmata messages
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        byte b = buffer[i];

                        // Look for version report (3 bytes: command, major, minor)
                        if (b == REPORT_VERSION && i + 2 < buffer.Length)
                        {
                            byte major = buffer[i + 1];
                            byte minor = buffer[i + 2];
                            Console.WriteLine($"Firmata version: {major}.{minor}");
                            firmataDetected = true;  // Set flag that Firmata was detected
                            i += 2;
                        }
                        // Process other message types...
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading data: {ex.Message}");
            }
        }

        private static void ForceReleasePort(string portName)
        {
            try
            {
                // Try to briefly open and close the port to release any stuck handles
                using (SerialPort testPort = new SerialPort(portName, 9600))
                {
                    testPort.Open();
                    testPort.Close();
                }
                Console.WriteLine($"Successfully released port {portName}");
            }
            catch
            {
                Console.WriteLine($"Port {portName} is in use by another application");
            }
        }
        private static void ClosePort()
        {
            if (_port != null)
            {
                try
                {
                    if (_port.IsOpen)
                    {
                        _port.Close();
                    }
                    _port.Dispose();
                    _port = null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error closing port: {ex.Message}");
                }
            }
        }
    }
}