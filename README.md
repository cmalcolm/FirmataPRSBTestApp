# Firmata PRSB Test Application

A sophisticated C# .NET 8 console application for detecting, connecting to, and controlling Arduino and compatible microcontroller boards using the Firmata protocol. Features intelligent device detection using Windows Registry and WMI, with optimized connection strategies for different board types.

## Features

- Smart Device Detection: Uses Windows Registry and WMI to identify connected Arduino devices before scanning
- Multi-Board Support: Optimized connection profiles for Uno, Mega, Leonardo, ESP8266, ESP32, and compatible devices
- Digital Pin Control: Set digital pins high or low (0-13)
- PWM Output Control: Control analog outputs via PWM (0-255)
- Servo Control: Position servos from 0-180 degrees
- Firmata Version Detection: Query and display Firmata protocol version
- Interactive Menu System: User-friendly console interface
- Multi-Device Management: Switch between connected devices without restarting
- COM Port Management: Intelligent COM1 handling with toggle option

## Supported Boards

- Official Arduino: Uno, Mega, Leonardo, Nano, Micro
- ESP Series: ESP8266 (Wemos D1, NodeMCU), ESP32
- Compatible Devices: CH340, CH341, CP210x, FTDI, and PL2303-based clones
- Windows-recognized: Any device that appears in Windows Device Manager as a serial device

## Requirements

- .NET 8.0 Runtime or higher
- Windows 10/11 (uses Windows-specific Registry and WMI APIs)
- Arduino/compatible board with Firmata firmware
- USB connection to your microcontroller board

## Firmware Setup

1. Install Firmata on your Arduino/compatible board:
   - Open Arduino IDE
   - Go to File → Examples → Firmata → StandardFirmata
   - Upload to your board
   - For ESP boards, use StandardFirmataESP or ConfigurableFirmata

2. Note the COM port assigned by Windows

## Installation

1. Download the application or clone the repository
2. Build (if needed):
   dotnet build
3. Run:
   dotnet run
   Or execute the compiled binary directly

## Usage

### Automatic Detection

The application automatically:
1. Queries Windows Registry and WMI to identify connected devices
2. Displays detected devices with their types
3. Uses optimized connection strategies for each device type
4. Attempts Firmata handshake with intelligent fallback

### Interactive Menu

=== DEVICE MENU ===
Connected to: COM4 (Arduino Uno) - Firmata v2.5
1. Set Digital Pin Output
2. Set PWM Output
3. Set Servo Position
4. Read Version
5. Test All Functions
6. Switch to different device
7. Rescan ports
8. Exit application
9. Toggle COM1 scanning (Currently: Disabled)

### Control Options

- Digital Pins (0-13): Set pins high (1) or low (0)
- PWM Output: Control analog values (0-255) on PWM-capable pins
- Servo Control: Position servos (0-180 degrees)
- Version Query: Check Firmata protocol version
- Test Suite: Run comprehensive functionality test

## Technical Details

### Connection Strategies

The application uses specialized connection profiles:

- Standard: Normal connection for Uno, Nano, and compatible boards
- Mega: Extended timeouts for Arduino Mega boards
- Leonardo: Special reset sequence for native USB boards
- ESP8266/ESP32: Advanced DTR/RTS toggling for ESP-based boards

### Detection Methods

1. Windows Registry: Reads SYSTEM\CurrentControlSet\Enum\USB for device information
2. WMI: Queries Win32_PnPEntity for COM port devices
3. Firmata Protocol: Sends version requests to detect Firmata-capable devices

### Baud Rates

- Primary: 115200 baud (optimal for most applications)
- Fallback: 57600, 9600 baud
- Auto-negotiation: Attempts multiple baud rates if needed

## Advanced Features

### Device Priority System

The application prioritizes connection strategies based on detected device types:

Example priority mapping:
"Arduino Mega" → Mega profile (long timeouts)
"Arduino Leonardo" → Leonardo profile (reset sequence)
"ESP8266" → ESP8266 profile (DTR/RTS toggling)
"Arduino Uno" → Standard profile (normal connection)

### COM Port Management

- COM1 skipping: Enabled by default (typically not used for Arduino)
- Toggle option: Menu option to enable/disable COM1 scanning
- Port validation: Checks port availability before attempting connection

## Building from Source

1. Requirements:
   - .NET 8.0 SDK
   - Visual Studio 2022 or VS Code

2. Build command:
   dotnet build -c Release

3. Output: bin/Release/net8.0/FirmataPRSBTestApp.exe

## Troubleshooting

### Common Issues

1. "Access denied" errors:
   - Close other serial port applications (Arduino IDE, serial monitors)
   - Run as Administrator if needed

2. Device not detected:
   - Check USB cable and connections
   - Verify device appears in Windows Device Manager
   - Ensure proper Firmata firmware is installed

3. Connection failures:
   - Try different USB ports
   - Check for driver issues in Device Manager
   - Use "Rescan ports" option

### ESP8266/ESP32 Notes

- Requires StandardFirmataESP instead of standard Firmata
- GPIO0 must not be grounded during operation
- Some clones may need CH340/CH341 drivers

## File Structure

FirmataPRSBTestApp/
├── FirmataClient.cs          # Main Firmata client implementation
├── Program.cs               # Application entry point and UI
├── WmiDeviceFinder.cs       # WMI-based device detection
└── FirmataPRSBTestApp.csproj # Project configuration

## License

This project is provided as-is under the MIT License.

## Contributing

Contributions are welcome! Please feel free to:

- Submit bug reports and feature requests
- Provide pull requests for improvements
- Add support for new device types
- Improve documentation

## Version History

- v2.0: Added Windows Registry/WMI detection, multiple connection profiles
- v1.0: Basic Firmata functionality with auto-detection

## Acknowledgments

- Built on the Firmata protocol standard
- Uses Windows Registry and WMI for professional device detection
- Optimized for flight simulator and real-time control applications
