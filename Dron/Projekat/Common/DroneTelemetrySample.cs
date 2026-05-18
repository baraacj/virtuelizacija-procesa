using System;
using System.Globalization;

namespace Common
{
    // =========================================================================
    // Interna klasa za rad sa uzorcima telemetrije.
    //
    // ZADATAK 5: TryParseCsv parsira CSV red sa invariant culture
    //            (tacka kao decimalni separator), validira opsege,
    //            i vraca opis greske za nevalidne redove.
    // =========================================================================
    public class DroneTelemetrySample
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

        // Izracunate vrednosti koriscene u analitici (Zadaci 9 i 10)
        public double AccelerationNorm =>
            Math.Sqrt(LinearAccelerationX * LinearAccelerationX
                    + LinearAccelerationY * LinearAccelerationY
                    + LinearAccelerationZ * LinearAccelerationZ);

        public double WindEffect =>
            Math.Abs(WindSpeed * Math.Sin(WindAngle * Math.PI / 180.0));

        // =====================================================================
        // ZADATAK 5: Parsiranje jednog CSV reda
        //   - CultureInfo.InvariantCulture (tacka kao decimalni separator)
        //   - Validacija obaveznih polja i opsega (WindSpeed >= 0, BattVolt > 0)
        //   - Vraca false + opis greske za nevalidne/visak redove
        // =====================================================================
        public static bool TryParseCsv(string line, out DroneTelemetrySample sample, out string error)
        {
            sample = null;
            error  = string.Empty;

            if (string.IsNullOrWhiteSpace(line))
            {
                error = "Prazan red";
                return false;
            }

            string[] parts = line.Replace("\"", "").Split(',');

            if (parts.Length < 21)
            {
                error = $"Nedovoljno kolona: ocekivano 21, pronadjeno {parts.Length}";
                return false;
            }

            var ci = CultureInfo.InvariantCulture;

            try
            {
                // Lokalna pomocna funkcija za parsiranje jednog polja
                double Parse(int i)
                {
                    if (!double.TryParse(parts[i].Trim(), NumberStyles.Float, ci, out double v))
                        throw new FormatException($"Kolona {i} nije broj: '{parts[i].Trim()}'");
                    return v;
                }

                double windSpeed  = Parse(1);
                double battVolt   = Parse(3);
                double ax         = Parse(18);
                double ay         = Parse(19);
                double az         = Parse(20);

                // Validacija opsega
                if (windSpeed <= 0)
                {
                    error = $"WindSpeed mora biti > 0 (vrednost: {windSpeed})";
                    return false;
                }
                if (battVolt <= 0)
                {
                    error = $"BatteryVoltage mora biti > 0 (vrednost: {battVolt})";
                    return false;
                }

                // Validacija konacnosti ubrzanja
                if (double.IsNaN(ax) || double.IsInfinity(ax) ||
                    double.IsNaN(ay) || double.IsInfinity(ay) ||
                    double.IsNaN(az) || double.IsInfinity(az))
                {
                    error = "LinearAcceleration sadrzi NaN ili Infinity";
                    return false;
                }

                sample = new DroneTelemetrySample
                {
                    Time                = Parse(0),
                    WindSpeed           = windSpeed,
                    WindAngle           = Parse(2),
                    BatteryVoltage      = battVolt,
                    BatteryCurrent      = Parse(4),
                    PositionX           = Parse(5),
                    PositionY           = Parse(6),
                    PositionZ           = Parse(7),
                    OrientationX        = Parse(8),
                    OrientationY        = Parse(9),
                    OrientationZ        = Parse(10),
                    OrientationW        = Parse(11),
                    VelocityX           = Parse(12),
                    VelocityY           = Parse(13),
                    VelocityZ           = Parse(14),
                    AngularX            = Parse(15),
                    AngularY            = Parse(16),
                    AngularZ            = Parse(17),
                    LinearAccelerationX = ax,
                    LinearAccelerationY = ay,
                    LinearAccelerationZ = az
                };

                return true;
            }
            catch (Exception ex)
            {
                error = $"Greska parsiranja: {ex.Message}";
                return false;
            }
        }

        // Konverzija u CSV red (invariant culture)
        public string ToCsvLine()
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

        public static string CsvHeader() =>
            "time,wind_speed,wind_angle,battery_voltage,battery_current," +
            "position_x,position_y,position_z," +
            "orientation_x,orientation_y,orientation_z,orientation_w," +
            "velocity_x,velocity_y,velocity_z," +
            "angular_x,angular_y,angular_z," +
            "linear_acceleration_x,linear_acceleration_y,linear_acceleration_z";

        // Konverzija u WCF DataContract objekat
        public DroneSample ToDroneSample() => new DroneSample
        {
            Time                = Time,
            WindSpeed           = WindSpeed,
            WindAngle           = WindAngle,
            BatteryVoltage      = BatteryVoltage,
            BatteryCurrent      = BatteryCurrent,
            PositionX           = PositionX,
            PositionY           = PositionY,
            PositionZ           = PositionZ,
            OrientationX        = OrientationX,
            OrientationY        = OrientationY,
            OrientationZ        = OrientationZ,
            OrientationW        = OrientationW,
            VelocityX           = VelocityX,
            VelocityY           = VelocityY,
            VelocityZ           = VelocityZ,
            AngularX            = AngularX,
            AngularY            = AngularY,
            AngularZ            = AngularZ,
            LinearAccelerationX = LinearAccelerationX,
            LinearAccelerationY = LinearAccelerationY,
            LinearAccelerationZ = LinearAccelerationZ
        };

        // Kreiranje iz WCF DataContract objekta
        public static DroneTelemetrySample FromDroneSample(DroneSample s) => new DroneTelemetrySample
        {
            Time                = s.Time,
            WindSpeed           = s.WindSpeed,
            WindAngle           = s.WindAngle,
            BatteryVoltage      = s.BatteryVoltage,
            BatteryCurrent      = s.BatteryCurrent,
            PositionX           = s.PositionX,
            PositionY           = s.PositionY,
            PositionZ           = s.PositionZ,
            OrientationX        = s.OrientationX,
            OrientationY        = s.OrientationY,
            OrientationZ        = s.OrientationZ,
            OrientationW        = s.OrientationW,
            VelocityX           = s.VelocityX,
            VelocityY           = s.VelocityY,
            VelocityZ           = s.VelocityZ,
            AngularX            = s.AngularX,
            AngularY            = s.AngularY,
            AngularZ            = s.AngularZ,
            LinearAccelerationX = s.LinearAccelerationX,
            LinearAccelerationY = s.LinearAccelerationY,
            LinearAccelerationZ = s.LinearAccelerationZ
        };
    }
}
