using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Runtime.Serialization;

namespace Common
{
    [DataContract]
    public class DroneTelemetrySample
    {
        [DataMember]
        public double Time { get; set; }

        [DataMember]
        public double WindSpeed { get; set; }

        [DataMember]
        public double WindAngle { get; set; }

        [DataMember]
        public double BatteryVoltage { get; set; }

        [DataMember]
        public double BatteryCurrent { get; set; }

        [DataMember]
        public double PositionX { get; set; }

        [DataMember]
        public double PositionY { get; set; }

        [DataMember]
        public double PositionZ { get; set; }

        [DataMember]
        public double OrientationX { get; set; }

        [DataMember]
        public double OrientationY { get; set; }

        [DataMember]
        public double OrientationZ { get; set; }

        [DataMember]
        public double OrientationW { get; set; }

        [DataMember]
        public double VelocityX { get; set; }

        [DataMember]
        public double VelocityY { get; set; }

        [DataMember]
        public double VelocityZ { get; set; }

        [DataMember]
        public double AngularX { get; set; }

        [DataMember]
        public double AngularY { get; set; }

        [DataMember]
        public double AngularZ { get; set; }

        [DataMember]
        public double LinearAccelerationX { get; set; }

        [DataMember]
        public double LinearAccelerationY { get; set; }

        [DataMember]
        public double LinearAccelerationZ { get; set; }

        // Dodatne kalkulisane vrednosti
        public double AccelerationNorm 
        { 
            get 
            { 
                return Math.Sqrt(LinearAccelerationX * LinearAccelerationX + 
                               LinearAccelerationY * LinearAccelerationY + 
                               LinearAccelerationZ * LinearAccelerationZ); 
            } 
        }

        public double WindEffect 
        { 
            get 
            { 
                return Math.Abs(WindSpeed * Math.Sin(WindAngle * Math.PI / 180.0)); 
            } 
        }

        public static bool TryParseCsv(string csvLine, out DroneTelemetrySample sample, out string error)
        {
            sample = null;
            error = string.Empty;
            
            if (string.IsNullOrWhiteSpace(csvLine))
            {
                error = "Empty line";
                return false;
            }

            // Ukloni navodnike i podeli po zarezi
            string cleaned = csvLine.Replace("\"", "");
            string[] parts = cleaned.Split(new[] { ',', ';', '\t' }, StringSplitOptions.None);

            var ci = CultureInfo.InvariantCulture;

            // Očekivani format: time,wind_speed,wind_angle,battery_voltage,battery_current,position_x,position_y,position_z,orientation_x,orientation_y,orientation_z,orientation_w,velocity_x,velocity_y,velocity_z,angular_x,angular_y,angular_z,linear_acceleration_x,linear_acceleration_y,linear_acceleration_z
            if (parts.Length < 21)
            {
                error = $"Invalid CSV format - expected 21 columns, found {parts.Length}";
                return false;
            }

            try
            {
                sample = new DroneTelemetrySample
                {
                    Time = double.Parse(parts[0], NumberStyles.Float, ci),
                    WindSpeed = double.Parse(parts[1], NumberStyles.Float, ci),
                    WindAngle = double.Parse(parts[2], NumberStyles.Float, ci),
                    BatteryVoltage = double.Parse(parts[3], NumberStyles.Float, ci),
                    BatteryCurrent = double.Parse(parts[4], NumberStyles.Float, ci),
                    PositionX = double.Parse(parts[5], NumberStyles.Float, ci),
                    PositionY = double.Parse(parts[6], NumberStyles.Float, ci),
                    PositionZ = double.Parse(parts[7], NumberStyles.Float, ci),
                    OrientationX = double.Parse(parts[8], NumberStyles.Float, ci),
                    OrientationY = double.Parse(parts[9], NumberStyles.Float, ci),
                    OrientationZ = double.Parse(parts[10], NumberStyles.Float, ci),
                    OrientationW = double.Parse(parts[11], NumberStyles.Float, ci),
                    VelocityX = double.Parse(parts[12], NumberStyles.Float, ci),
                    VelocityY = double.Parse(parts[13], NumberStyles.Float, ci),
                    VelocityZ = double.Parse(parts[14], NumberStyles.Float, ci),
                    AngularX = double.Parse(parts[15], NumberStyles.Float, ci),
                    AngularY = double.Parse(parts[16], NumberStyles.Float, ci),
                    AngularZ = double.Parse(parts[17], NumberStyles.Float, ci),
                    LinearAccelerationX = double.Parse(parts[18], NumberStyles.Float, ci),
                    LinearAccelerationY = double.Parse(parts[19], NumberStyles.Float, ci),
                    LinearAccelerationZ = double.Parse(parts[20], NumberStyles.Float, ci)
                };

                // Validacija osnovnih opsega
                if (sample.WindSpeed < 0)
                {
                    error = "WindSpeed must be >= 0";
                    return false;
                }

                if (sample.BatteryVoltage <= 0)
                {
                    error = "BatteryVoltage must be > 0";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"Parse error: {ex.Message}";
                return false;
            }
        }

        public string ToCsvString()
        {
            var ci = CultureInfo.InvariantCulture;
            return string.Format(ci, 
                "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20}",
                Time, WindSpeed, WindAngle, BatteryVoltage, BatteryCurrent,
                PositionX, PositionY, PositionZ,
                OrientationX, OrientationY, OrientationZ, OrientationW,
                VelocityX, VelocityY, VelocityZ,
                AngularX, AngularY, AngularZ,
                LinearAccelerationX, LinearAccelerationY, LinearAccelerationZ);
        }

        public static string CsvHeader()
        {
            return "time,wind_speed,wind_angle,battery_voltage,battery_current,position_x,position_y,position_z,orientation_x,orientation_y,orientation_z,orientation_w,velocity_x,velocity_y,velocity_z,angular_x,angular_y,angular_z,linear_acceleration_x,linear_acceleration_y,linear_acceleration_z";
        }
    }
}
