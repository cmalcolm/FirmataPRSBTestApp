# Firmata PRSB Test Application

A C# .NET 8 console application for testing and controlling Arduino boards using the Firmata protocol. This tool provides a simple interface to interact with Arduino pins and functions via serial communication.

## Features

- **Automatic Arduino Detection**: Scans all available COM ports to find Arduino boards running Firmata
- **Digital Pin Control**: Set digital pins high or low
- **PWM Output Control**: Control analog outputs via PWM
- **Servo Control**: Position servos from 0-180 degrees
- **Version Reporting**: Query the Firmata version from the Arduino
- **Robust Port Handling**: Special handling for COM port access issues

## Requirements

- .NET 8.0 SDK
- Arduino board with Firmata firmware installed
- USB connection to Arduino board

## Setup

### Arduino Setup

1. Connect your Arduino board to your computer via USB
2. Open the Arduino IDE
3. Go to File → Examples → Firmata → StandardFirmata
4. **IMPORTANT:** Modify the baud rate in the StandardFirmata sketch:
   - Find the line `Firmata.begin(57600);` (usually around line 298)
   - Change it to `Firmata.begin(115200);`
5. Upload the modified sketch to your Arduino
6. Note the COM port assigned to your Arduino in the Arduino IDE

### Application Setup

1. Clone this repository
2. Open the solution in Visual Studio 2022
3. Build and run the application
4. The application will automatically scan for and connect to your Arduino

## Usage

Once connected to an Arduino board, the application presents a menu with the following options:

1. **Set Digital Pin Output**: Control digital pins (HIGH/LOW)
2. **Set PWM (Analog) Output**: Set PWM values (0-255)
3. **Set Servo Position**: Position servo motors (0-180 degrees)
4. **Read Version**: Query the Firmata version running on Arduino
5. **q**: Quit the application

## Troubleshooting

### COM Port Access Issues

If you encounter "Access to the path 'COMx' is denied" errors:
- The application includes a `ForceReleasePort` function that attempts to release stuck port handles
- Close any other applications that might be using the COM port (Arduino IDE, Serial Monitor, etc.)
- Restart your computer if problems persist

### Arduino Not Detected

If the Arduino isn't detected:
- Ensure the StandardFirmata sketch is properly uploaded to your Arduino
- **Verify you changed the baud rate to 115200** in the StandardFirmata sketch
- For Arduino Leonardo boards, you may need to press the reset button when the scanning starts
- Check that the Arduino is recognized in Device Manager

## Technical Details

This application communicates with the Arduino using the Firmata protocol, which is a binary protocol for serial communication based on the MIDI message format. The application handles:

- Arduino board detection
- Command formatting according to the Firmata specification
- Response parsing from the Arduino
- Serial port management and error handling

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Contributing

Contributions to improve the application are welcome. Please feel free to submit pull requests or open issues to discuss proposed changes or report bugs.
