using Common;
using System;
using System.Globalization;
using System.IO;

namespace Client
{
    public class DroneCsvSampleReader : IDisposable
    {
        private readonly FileStream fileStream;
        private readonly StreamReader reader;
        private readonly StreamWriter rejectWriter;
        private readonly FileStream rejectStream;
        private bool headerChecked = false;
        private bool disposed = false;
        public int AcceptedCount { get; private set; }
        public int RejectedCount { get; private set; }

        public DroneCsvSampleReader(string csvPath, string rejectLogPath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(rejectLogPath));
                fileStream = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                reader = new StreamReader(fileStream);
                rejectStream = new FileStream(rejectLogPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                rejectWriter = new StreamWriter(rejectStream);
                rejectWriter.WriteLine("Reason,Line");
            }
            catch
            {
                // Очисти ресурсе ако има грешку у конструктору
                Dispose();
                throw;
            }
        }

        public bool TryReadNext(out DroneTelemetrySample sample)
        {
            sample = null;
            while (true)
            {
                string line = reader.ReadLine();
                if (line == null) return false;

                // Prvi red može biti header – proveri samo jednom
                if (!headerChecked)
                {
                    headerChecked = true;
                    var low = line.ToLowerInvariant();
                    if (low.Contains("time") && low.Contains("wind_speed") && low.Contains("linear_acceleration"))
                    {
                        continue; // preskoči header
                    }
                }

                if (DroneTelemetrySample.TryParseCsv(line, out sample, out var error))
                {
                    AcceptedCount++;
                    // Debug info za prve nekoliko uspešnih
                    if (AcceptedCount <= 3)
                    {
                        Console.WriteLine($"Sample {AcceptedCount}: Time={sample.Time:F2}s, Wind={sample.WindSpeed:F2}m/s, Accel={sample.AccelerationNorm:F2}m/s2");
                    }
                    return true;
                }

                // loguj i nastavi čitanje
                rejectWriter.WriteLine(string.Join(",", error.Replace(',', ';'), line.Replace(',', ';')));
                rejectWriter.Flush();
                RejectedCount++;
                
                // Debug info za prve nekoliko grešaka
                if (RejectedCount <= 2)
                {
                    Console.WriteLine($"Rejected line {RejectedCount}: {error}");
                    Console.WriteLine($"  Preview: {line.Substring(0, Math.Min(80, line.Length))}...");
                }
            }
        }

        public bool TryReadNext(out DroneSample sample)
        {
            sample = null;
            DroneTelemetrySample telemetrySample;
            
            if (TryReadNext(out telemetrySample))
            {
                // Konvertuj DroneTelemetrySample u DroneSample za WCF
                sample = new DroneSample
                {
                    Time = telemetrySample.Time,
                    WindSpeed = telemetrySample.WindSpeed,
                    WindAngle = telemetrySample.WindAngle,
                    BatteryVoltage = telemetrySample.BatteryVoltage,
                    BatteryCurrent = telemetrySample.BatteryCurrent,
                    PositionX = telemetrySample.PositionX,
                    PositionY = telemetrySample.PositionY,
                    PositionZ = telemetrySample.PositionZ,
                    OrientationX = telemetrySample.OrientationX,
                    OrientationY = telemetrySample.OrientationY,
                    OrientationZ = telemetrySample.OrientationZ,
                    OrientationW = telemetrySample.OrientationW,
                    VelocityX = telemetrySample.VelocityX,
                    VelocityY = telemetrySample.VelocityY,
                    VelocityZ = telemetrySample.VelocityZ,
                    AngularX = telemetrySample.AngularX,
                    AngularY = telemetrySample.AngularY,
                    AngularZ = telemetrySample.AngularZ,
                    LinearAccelerationX = telemetrySample.LinearAccelerationX,
                    LinearAccelerationY = telemetrySample.LinearAccelerationY,
                    LinearAccelerationZ = telemetrySample.LinearAccelerationZ
                };
                return true;
            }
            
            return false;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Ослободи управљане ресурсе
                    try { rejectWriter?.Flush(); } catch { }
                    try { rejectWriter?.Dispose(); } catch { }
                    try { rejectStream?.Dispose(); } catch { }
                    try { reader?.Dispose(); } catch { }
                    try { fileStream?.Dispose(); } catch { }
                }
                
                disposed = true;
            }
        }

        ~DroneCsvSampleReader()
        {
            Dispose(false);
        }
    }
}
