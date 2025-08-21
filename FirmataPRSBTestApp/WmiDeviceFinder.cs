using System;
using System.Collections.Generic;
using System.Management;
using System.Text.RegularExpressions;

namespace FirmataPRSBTestApp
{
    public static class WmiDeviceFinder
    {
        /// <summary>
        /// Uses WMI to find connected Arduino and compatible devices by scanning Win32_PnPEntity.
        /// Returns a dictionary mapping COM port names (e.g. "COM3") to a friendly board type.
        /// </summary>
        public static Dictionary<string, string> GetConnectedArduinoDevices()
        {
            var arduinoDevices = new Dictionary<string, string>();

            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'"))
                {
                    foreach (var device in searcher.Get())
                    {
                        string name = device["Name"]?.ToString() ?? "";
                        string hardwareId = (device["HardwareID"] as string[])?[0] ?? "";

                        // Try to identify board type from name
                        string boardType = IdentifyBoardType(name, hardwareId);

                        // Extract COM port name from the string, e.g. "Arduino Uno (COM3)"
                        var match = Regex.Match(name, @"\(COM\d+\)");
                        if (match.Success)
                        {
                            string port = match.Value.Trim('(', ')');
                            if (!arduinoDevices.ContainsKey(port))
                                arduinoDevices.Add(port, boardType);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WMI scan error: {ex.Message}");
            }

            return arduinoDevices;
        }

        private static string IdentifyBoardType(string name, string hardwareId)
        {
            string text = (name + " " + hardwareId).ToLower();

            if (text.Contains("uno")) return "Arduino Uno";
            if (text.Contains("mega")) return "Arduino Mega";
            if (text.Contains("leonardo")) return "Arduino Leonardo";
            if (text.Contains("nano")) return "Arduino Nano";
            if (text.Contains("micro")) return "Arduino Micro";
            if (text.Contains("esp32")) return "ESP32";
            if (text.Contains("esp8266")) return "ESP8266";
            if (text.Contains("ch340")) return "Arduino-Compatible (CH340)";
            if (text.Contains("ch341")) return "Arduino-Compatible (CH341)";
            if (text.Contains("cp210")) return "Arduino-Compatible (CP210x)";
            if (text.Contains("ftdi")) return "Arduino-Compatible (FTDI)";
            if (text.Contains("pl2303")) return "Arduino-Compatible (PL2303)";
            if (text.Contains("usb serial")) return "Arduino-Compatible (USB Serial)";

            // Fallback
            if (text.Contains("arduino")) return "Arduino-Compatible";
            return "Unknown Device";
        }
    }
}