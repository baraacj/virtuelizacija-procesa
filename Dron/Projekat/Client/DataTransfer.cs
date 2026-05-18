using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;

namespace Client
{
    // Pomocna klasa za direktan prenos podataka izmedju CSV fajla i Server storage foldera
    // Koristi se nezavisno od WCF komunikacije - direktno kopira podatke na disk
    public class DataTransfer
    {
        private static readonly Random random = new Random();

        public static void Run()
        {
            Console.WriteLine("=== Data Transfer Program ===");
            Console.WriteLine("Uzimanje nasumicnih podataka iz Client CSV-a i upis u Server fajlove");
            Console.WriteLine();

            try
            {
                string clientCsvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dataset", "12.csv");
                string serverStorageRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "..", "..", "..", "Server", "bin", "Debug");

                Console.WriteLine($"Client CSV: {clientCsvPath}");
                Console.WriteLine($"Server Storage: {serverStorageRoot}");
                Console.WriteLine();

                if (!File.Exists(clientCsvPath))
                {
                    Console.WriteLine($"Client CSV fajl ne postoji: {clientCsvPath}");
                    return;
                }

                if (!Directory.Exists(serverStorageRoot))
                {
                    Console.WriteLine($"Server storage direktorijum ne postoji: {serverStorageRoot}");
                    return;
                }

                var droneData = LoadDroneData(clientCsvPath);
                Console.WriteLine($"Ucitano {droneData.Count} redova iz client CSV-a");

                if (droneData.Count == 0)
                {
                    Console.WriteLine("Nema podataka za transfer");
                    return;
                }

                string sessionId = Guid.NewGuid().ToString("N");

                TransferToStorage(droneData, serverStorageRoot, "DroneStorage", sessionId);
                TransferToStorage(droneData, serverStorageRoot, "SmartGridStorage", sessionId);

                Console.WriteLine();
                Console.WriteLine("Transfer podataka zavrsen uspjesno!");
                Console.WriteLine($"Session ID: {sessionId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greska: {ex.Message}");
            }
        }

        private static List<DroneDataRow> LoadDroneData(string csvPath)
        {
            var data = new List<DroneDataRow>();
            var lines = File.ReadAllLines(csvPath);

            if (lines.Length < 2) return data;

            for (int i = 1; i < lines.Length; i++)
            {
                try
                {
                    var parts = lines[i].Split(',');
                    if (parts.Length >= 21)
                    {
                        var row = new DroneDataRow
                        {
                            Time                = double.Parse(parts[0],  CultureInfo.InvariantCulture),
                            WindSpeed           = double.Parse(parts[1],  CultureInfo.InvariantCulture),
                            WindAngle           = double.Parse(parts[2],  CultureInfo.InvariantCulture),
                            BatteryVoltage      = double.Parse(parts[3],  CultureInfo.InvariantCulture),
                            BatteryCurrent      = double.Parse(parts[4],  CultureInfo.InvariantCulture),
                            PositionX           = double.Parse(parts[5],  CultureInfo.InvariantCulture),
                            PositionY           = double.Parse(parts[6],  CultureInfo.InvariantCulture),
                            PositionZ           = double.Parse(parts[7],  CultureInfo.InvariantCulture),
                            OrientationX        = double.Parse(parts[8],  CultureInfo.InvariantCulture),
                            OrientationY        = double.Parse(parts[9],  CultureInfo.InvariantCulture),
                            OrientationZ        = double.Parse(parts[10], CultureInfo.InvariantCulture),
                            OrientationW        = double.Parse(parts[11], CultureInfo.InvariantCulture),
                            VelocityX           = double.Parse(parts[12], CultureInfo.InvariantCulture),
                            VelocityY           = double.Parse(parts[13], CultureInfo.InvariantCulture),
                            VelocityZ           = double.Parse(parts[14], CultureInfo.InvariantCulture),
                            AngularX            = double.Parse(parts[15], CultureInfo.InvariantCulture),
                            AngularY            = double.Parse(parts[16], CultureInfo.InvariantCulture),
                            AngularZ            = double.Parse(parts[17], CultureInfo.InvariantCulture),
                            LinearAccelerationX = double.Parse(parts[18], CultureInfo.InvariantCulture),
                            LinearAccelerationY = double.Parse(parts[19], CultureInfo.InvariantCulture),
                            LinearAccelerationZ = double.Parse(parts[20], CultureInfo.InvariantCulture),
                            OriginalLine        = lines[i]
                        };
                        data.Add(row);
                    }
                }
                catch { continue; }
            }

            return data;
        }

        private static void TransferToStorage(List<DroneDataRow> droneData,
            string serverRoot, string storageType, string sessionId)
        {
            string storagePath = Path.Combine(serverRoot, storageType, sessionId);
            Directory.CreateDirectory(storagePath);

            Console.WriteLine($"\nKreiranje {storageType}/{sessionId}");

            var shuffledData = droneData.OrderBy(x => random.Next()).ToList();
            int totalCount   = shuffledData.Count;

            int measurementsCount = (int)(totalCount * 0.6);
            int analyticsCount    = (int)(totalCount * 0.3);

            var measurementsData    = shuffledData.Take(measurementsCount).ToList();
            var analyticsSourceData = shuffledData.Skip(measurementsCount).Take(analyticsCount).ToList();
            var rejectsSourceData   = shuffledData.Skip(measurementsCount + analyticsCount).ToList();

            CreateMeasurementsFile(storagePath, measurementsData);
            Console.WriteLine($"   measurements_session.csv ({measurementsData.Count} redova)");

            CreateAnalyticsFile(storagePath, analyticsSourceData);
            Console.WriteLine($"   analytics_alerts.csv ({analyticsSourceData.Count} redova)");

            CreateRejectsFile(storagePath, rejectsSourceData);
            Console.WriteLine($"   rejects.csv ({rejectsSourceData.Count} redova)");
        }

        private static void CreateMeasurementsFile(string storagePath, List<DroneDataRow> data)
        {
            string filePath = Path.Combine(storagePath, "measurements_session.csv");
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("time,wind_speed,wind_angle,battery_voltage,battery_current," +
                    "position_x,position_y,position_z,orientation_x,orientation_y,orientation_z," +
                    "orientation_w,velocity_x,velocity_y,velocity_z,angular_x,angular_y,angular_z," +
                    "linear_acceleration_x,linear_acceleration_y,linear_acceleration_z");
                foreach (var row in data)
                    writer.WriteLine(row.OriginalLine);
            }
        }

        private static void CreateAnalyticsFile(string storagePath, List<DroneDataRow> data)
        {
            string filePath = Path.Combine(storagePath, "analytics_alerts.csv");
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("Timestamp,AlertType,Message,Value,Threshold");
                foreach (var row in data)
                    foreach (var alert in GenerateAlerts(row))
                        writer.WriteLine(alert);
            }
        }

        private static void CreateRejectsFile(string storagePath, List<DroneDataRow> data)
        {
            string filePath = Path.Combine(storagePath, "rejects.csv");
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("Reason,Line");
                foreach (var row in data)
                    writer.WriteLine($"{GenerateRejectReason(row)},{row.OriginalLine}");
            }
        }

        private static List<string> GenerateAlerts(DroneDataRow row)
        {
            var alerts    = new List<string>();
            var timestamp = DateTime.UtcNow.AddSeconds(-random.Next(0, 3600))
                                           .ToString("yyyy-MM-dd HH:mm:ss");

            if (row.BatteryVoltage < 24.0)
                alerts.Add($"{timestamp},BATTERY_LOW,Battery voltage below threshold,{row.BatteryVoltage:F6},24.0");

            if (row.WindSpeed > 0.8)
                alerts.Add($"{timestamp},WIND_HIGH,High wind speed detected,{row.WindSpeed:F6},0.8");

            double totalAcc = Math.Sqrt(
                row.LinearAccelerationX * row.LinearAccelerationX +
                row.LinearAccelerationY * row.LinearAccelerationY +
                (row.LinearAccelerationZ + 9.81) * (row.LinearAccelerationZ + 9.81));

            if (totalAcc > 2.0)
                alerts.Add($"{timestamp},ACCELERATION_HIGH,High acceleration detected,{totalAcc:F6},2.0");

            double distance = Math.Sqrt(row.PositionX * row.PositionX + row.PositionY * row.PositionY);
            if (distance > 100)
                alerts.Add($"{timestamp},POSITION_OUT_OF_BOUNDS,Drone outside safe zone,{distance:F6},100.0");

            if (random.NextDouble() < 0.1)
            {
                string[] types = { "SYSTEM_CHECK", "CALIBRATION_NEEDED", "MAINTENANCE_DUE", "COMMUNICATION_WEAK" };
                alerts.Add($"{timestamp},{types[random.Next(types.Length)]},Random system alert,{random.NextDouble():F6},0.5");
            }

            return alerts;
        }

        private static string GenerateRejectReason(DroneDataRow row)
        {
            var reasons = new List<string>();

            if (row.BatteryVoltage <= 0 || row.BatteryVoltage > 30)
                reasons.Add("Invalid battery voltage");
            if (row.WindSpeed < 0 || row.WindSpeed > 50)
                reasons.Add("Invalid wind speed");
            if (Math.Abs(row.PositionZ) > 1000)
                reasons.Add("Invalid altitude");
            if (Math.Abs(row.LinearAccelerationZ + 9.81) > 50)
                reasons.Add("Invalid acceleration data");

            if (reasons.Count == 0)
            {
                string[] randomReasons = {
                    "Data quality check failed",
                    "Timestamp out of sequence",
                    "Sensor calibration error",
                    "Communication timeout",
                    "Checksum validation failed"
                };
                reasons.Add(randomReasons[random.Next(randomReasons.Length)]);
            }

            return string.Join("; ", reasons);
        }
    }

    public class DroneDataRow
    {
        public double Time                { get; set; }
        public double WindSpeed           { get; set; }
        public double WindAngle           { get; set; }
        public double BatteryVoltage      { get; set; }
        public double BatteryCurrent      { get; set; }
        public double PositionX           { get; set; }
        public double PositionY           { get; set; }
        public double PositionZ           { get; set; }
        public double OrientationX        { get; set; }
        public double OrientationY        { get; set; }
        public double OrientationZ        { get; set; }
        public double OrientationW        { get; set; }
        public double VelocityX           { get; set; }
        public double VelocityY           { get; set; }
        public double VelocityZ           { get; set; }
        public double AngularX            { get; set; }
        public double AngularY            { get; set; }
        public double AngularZ            { get; set; }
        public double LinearAccelerationX { get; set; }
        public double LinearAccelerationY { get; set; }
        public double LinearAccelerationZ { get; set; }
        public string OriginalLine        { get; set; }
    }
}
