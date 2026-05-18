using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;
using System.Configuration;

namespace Client
{
    public class Program
    {
        static void Main(string[] args)
        {
            ChannelFactory<IDroneService> droneFactory = null;
            IDroneService droneProxy = null;
            
            Console.WriteLine("Connecting to Drone telemetry service...");
            
            // Retry connection logic
            int maxRetries = 3;
            int retryDelay = 2000; // 2 seconds
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    Console.WriteLine($"Connection attempt {attempt}/{maxRetries}...");
                    
                    var binding = new NetTcpBinding
                    {
                        TransferMode = TransferMode.Streamed,
                        MaxReceivedMessageSize = 10485760,
                        OpenTimeout = TimeSpan.FromSeconds(10),
                        CloseTimeout = TimeSpan.FromSeconds(10),
                        SendTimeout = TimeSpan.FromSeconds(30),
                        ReceiveTimeout = TimeSpan.FromMinutes(10)
                    };

                    var endpoint = new EndpointAddress("net.tcp://localhost:4100/Drone");
                    droneFactory = new ChannelFactory<IDroneService>(binding, endpoint);
                    droneProxy = droneFactory.CreateChannel();
                    
                    // Test connection
                    ((ICommunicationObject)droneProxy).Open();
                    
                    Console.WriteLine("✅ Connection established successfully!");
                    break; // Success - exit retry loop
                }
                catch (EndpointNotFoundException ex)
                {
                    Console.WriteLine($"❌ Server not found: {ex.Message}");
                    if (attempt == maxRetries)
                    {
                        Console.WriteLine("\n🚫 FAILED to connect to Drone service!");
                        Console.WriteLine("📋 Troubleshooting steps:");
                        Console.WriteLine("  1. Make sure Server.exe is running");
                        Console.WriteLine("  2. Check that port 4100 is available");
                        Console.WriteLine("  3. Verify Windows Firewall settings");
                        Console.WriteLine("  4. Run both programs as Administrator if needed");
                        Console.WriteLine("\n💡 To start the server:");
                        Console.WriteLine("  - Navigate to Server\\bin\\Debug\\");
                        Console.WriteLine("  - Run Server.exe");
                        Console.WriteLine("  - Wait for 'Status: Waiting for client connections...' message");
                        Console.WriteLine("\nPress any key to exit...");
                        try { Console.ReadKey(); } catch { System.Threading.Thread.Sleep(1000); }
                        return;
                    }
                    Console.WriteLine($"⏳ Retrying in {retryDelay/1000} seconds...");
                    System.Threading.Thread.Sleep(retryDelay);
                }
                catch (CommunicationException ex)
                {
                    Console.WriteLine($"❌ Communication error: {ex.Message}");
                    if (attempt == maxRetries)
                    {
                        Console.WriteLine("\n🚫 FAILED to connect to Drone service!");
                        Console.WriteLine("This might be a configuration or network issue.");
                        Console.WriteLine("\nPress any key to exit...");
                        try { Console.ReadKey(); } catch { System.Threading.Thread.Sleep(1000); }
                        return;
                    }
                    Console.WriteLine($"⏳ Retrying in {retryDelay/1000} seconds...");
                    System.Threading.Thread.Sleep(retryDelay);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Unexpected error: {ex.GetType().Name}: {ex.Message}");
                    if (attempt == maxRetries)
                    {
                        Console.WriteLine("\n🚫 FAILED to connect to Drone service!");
                        Console.WriteLine($"Full error details: {ex}");
                        Console.WriteLine("\nPress any key to exit...");
                        try { Console.ReadKey(); } catch { System.Threading.Thread.Sleep(1000); }
                        return;
                    }
                    Console.WriteLine($"⏳ Retrying in {retryDelay/1000} seconds...");
                    System.Threading.Thread.Sleep(retryDelay);
                }
            }

            Console.WriteLine("\nDrone Telemetry Client");
            Console.WriteLine("Enter CSV file path (or press Enter for auto-detection):");
            string path = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(path))
            {
                Console.WriteLine("Auto-detecting CSV files...");
                
                // Auto-detect drone CSV files - koristi 12.csv
                string binDataset = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dataset", "12.csv");
                string projDataset = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Client", "Dataset", "12.csv");
                
                if (File.Exists(binDataset))
                {
                    path = binDataset;
                    Console.WriteLine($"Found: {Path.GetFileName(path)} in bin/Debug/Dataset");
                }
                else if (File.Exists(projDataset))
                {
                    path = projDataset;
                    Console.WriteLine($"Found: {Path.GetFileName(path)} in project Dataset folder");
                }
                else
                {
                    path = binDataset; // fallback
                }

                if (!File.Exists(path))
                {
                    // Pokušaj da nadješ bilo koji CSV koji nije rejects fajl
                    string binDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dataset");
                    string projDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Client", "Dataset");
                    string found = null;
                    
                    if (Directory.Exists(binDir))
                    {
                        var files = Directory.GetFiles(binDir, "*.csv")
                            .Where(f => !Path.GetFileName(f).StartsWith("rejects_"))
                            .ToArray();
                        if (files.Length > 0) found = files[0];
                    }
                    if (found == null && Directory.Exists(projDir))
                    {
                        var files = Directory.GetFiles(projDir, "*.csv")
                            .Where(f => !Path.GetFileName(f).StartsWith("rejects_"))
                            .ToArray();
                        if (files.Length > 0) found = files[0];
                    }
                    if (found != null)
                    {
                        path = found;
                    }
                }
            }

            while (!File.Exists(path))
            {
                Console.WriteLine($"CSV file not found at: {path}");
                Console.WriteLine("Please enter full path to .csv file:");
                path = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(path))
                {
                    Console.WriteLine("Empty path. Please try again.");
                    continue;
                }
            }
            
            Console.WriteLine($"Using CSV file: {path}");

            var meta = new DroneSessionMeta
            {
                SessionId = Guid.NewGuid().ToString("N"),
                Time = DateTime.UtcNow,
                LinearAccelerationX = 0,
                LinearAccelerationY = 0,
                LinearAccelerationZ = -9.81, // gravitacija
                WindSpeed = 0,
                WindAngle = 0
            };

            try
            {
                Console.WriteLine("🚀 Starting session...");
                DroneServiceResponse response = droneProxy.StartSession(meta);
                
                if (response.Type != ResponseType.ACK)
                {
                    Console.WriteLine($"❌ Failed to start session: {response.Message}");
                    Console.WriteLine("Press any key to exit...");
                    try { Console.ReadKey(); } catch { System.Threading.Thread.Sleep(1000); }
                    return;
                }
                
                Console.WriteLine($"✅ Session started: {response.SessionId}");
                Console.WriteLine($"Session status: {response.Status}");

                int sent = 0;
                int successful = 0;
                int failed = 0;
                Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dataset"));
                string rejects = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dataset", $"rejects_client_{meta.SessionId}.csv");
                
                Console.WriteLine($"\nReading from: {Path.GetFileName(path)}");
                double aThreshold = double.Parse(ConfigurationManager.AppSettings["A_threshold"] ?? "1.0", CultureInfo.InvariantCulture);
                double wThreshold = double.Parse(ConfigurationManager.AppSettings["W_threshold"] ?? "5.0", CultureInfo.InvariantCulture);
                Console.WriteLine($"Analysis thresholds: Acceleration={aThreshold:F4} m/s2, Wind={wThreshold:F4} m/s");
                Console.WriteLine("Loading and sending drone telemetry samples...");
                
                Console.WriteLine($"Ucitavanje podataka iz {Path.GetFileName(path)} (12.csv)...");
                
                // Alternativni pristup - Lazy loading sa LINQ i IEnumerable
                var csvProcessor = new DroneDataProcessor(path, rejects);
                var allSamples = csvProcessor.LoadSamples().ToList();
                
                // Generiraj mix podataka za sve tri fajla
                var sampleBatch = GenerateMixedSamples(allSamples, 100);

                Console.WriteLine($"Pripremljeno {sampleBatch.Count} uzoraka za slanje...");
                Console.WriteLine("Mix podataka: ~70% validni (measurements), ~20% alerte (analytics), ~10% neispravni (rejects)");
                Console.WriteLine("Ocekivani rezultati:");
                Console.WriteLine("  - measurements_session.csv: validni podaci");
                Console.WriteLine("  - rejects.csv: podaci sa neispravnim vrednostima");
                Console.WriteLine("  - analytics_alerts.csv: alerte za ubrzanje i vetar");
                Console.WriteLine();
                
                foreach (var sampleWrapper in sampleBatch)
                {
                    var sample = sampleWrapper.Sample;
                    
                    // Progresivno slanje sa asinhronim obrascem
                    var sendTask = Task.Run(() =>
                    {
                        try
                        {
                            return droneProxy.PushSample(sample);
                        }
                        catch (FaultException<ValidationFault> vfEx)
                        {
                            sampleWrapper.ProcessingError = $"Validation: {vfEx.Detail.Message} (Field: {vfEx.Detail.Field})";
                            return null;
                        }
                        catch (FaultException<DataFormatFault> dfEx)
                        {
                            sampleWrapper.ProcessingError = $"Data format: {dfEx.Detail.Message}";
                            return null;
                        }
                        catch (FaultException fEx)
                        {
                            sampleWrapper.ProcessingError = $"Service fault: {fEx.Message}";
                            return null;
                        }
                        catch (CommunicationException commEx)
                        {
                            sampleWrapper.ProcessingError = $"Communication: {commEx.Message}";
                            return null;
                        }
                        catch (Exception ex)
                        {
                            sampleWrapper.ProcessingError = $"Unexpected: {ex.GetType().Name}: {ex.Message}";
                            return null;
                        }
                    });

                    // Cekamo rezultat ili timeout
                    if (sendTask.Wait(5000)) // 5 sekundi timeout
                    {
                        var resp = sendTask.Result;
                        sent++;
                        
                        if (resp?.Type == ResponseType.ACK)
                        {
                            if (resp.Message.StartsWith("Sample rejected:") || 
                                resp.Message.StartsWith("Sample processing failed:") || 
                                resp.Message.StartsWith("Sample processing error:"))
                            {
                                // Uzorak je odbačen ali komunikacija nastavlja
                                failed++;
                                sampleWrapper.ProcessingResult = $"REJECTED: {resp.Message}";
                                if (failed <= 5)
                                    Console.WriteLine($"\n⚠️ Odbačen: {resp.Message}");
                            }
                            else
                            {
                                // Uzorak je uspešno obrađen
                                successful++;
                                sampleWrapper.ProcessingResult = "SUCCESS";
                            }
                        }
                        else
                        {
                            failed++;
                            sampleWrapper.ProcessingResult = $"FAILED: {resp?.Message ?? "Unknown error"}";
                            if (failed <= 3)
                                Console.WriteLine($"\n❌ Greska: {resp?.Message ?? "Nepoznata greska"}");
                        }
                    }
                    else
                    {
                        failed++;
                        sampleWrapper.ProcessingResult = "TIMEOUT";
                        Console.WriteLine($"\n⏱️ Timeout za uzorak {sent + 1}");
                    }
                    
                    // Dinamicki prikaz napretka
                    UpdateProgressDisplay(sent, successful, failed, sampleBatch.Count);

                    // Adaptivna pauza na osnovu uspeha
                    var delay = successful > failed ? 50 : 150;
                    System.Threading.Thread.Sleep(delay);
                }
                
                Console.WriteLine($"\n\nPregled obrade fajla:");
                Console.WriteLine($"  Uspesno procitano uzoraka: {csvProcessor.AcceptedCount}");
                Console.WriteLine($"  Odbaceno neispravnih redova: {csvProcessor.RejectedCount}");
                if (csvProcessor.RejectedCount > 0)
                {
                    Console.WriteLine($"  Log odbacenih redova: {Path.GetFileName(rejects)}");
                }
                
                DroneServiceResponse endResponse = droneProxy.EndSession();
                Console.WriteLine($"\nSession completed: {endResponse.Type} - {endResponse.Status} - {endResponse.Message}");
                Console.WriteLine($"\nTransmission summary:");
                Console.WriteLine($"  Total sent: {sent}");
                Console.WriteLine($"  Successful: {successful}");
                Console.WriteLine($"  Failed: {failed}");
                if (sent == 0)
                {
                    Console.WriteLine("No samples were sent. Please check CSV format or file path.");
                }
            }
            catch (FaultException<ValidationFault> ex)
            {
                Console.WriteLine($"\nValidation error: {ex.Detail.Message}");
                Console.WriteLine($"Field: {ex.Detail.Field}, Expected: {ex.Detail.ExpectedRange}");
            }
            catch (FaultException<DataFormatFault> ex)
            {
                Console.WriteLine($"\nData format error: {ex.Detail.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nUnexpected error: {ex.Message}");
            }
            finally
            {
                try
                {
                    if (droneProxy is ICommunicationObject commObj)
                        commObj.Close();
                    droneFactory?.Close();
                }
                catch { }
            }

            Console.WriteLine("\nPress any key to exit...");
            try 
            { 
                Console.ReadKey(); 
            } 
            catch 
            { 
                // Handle redirected input gracefully
                System.Threading.Thread.Sleep(1000);
            }
        }

        // Generiraj mix podataka za testiranje svih fajlova
        // Ovo ce kreirati podatke koji ce popuniti sva tri izlazna fajla:
        // - measurements_session.csv (validni podaci)
        // - rejects.csv (neispravni podaci koji ne prodje validaciju)
        // - analytics_alerts.csv (validni podaci koji triggeruju alerte)
        private static List<SampleWrapper> GenerateMixedSamples(List<SampleWrapper> originalSamples, int totalCount)
        {
            var result = new List<SampleWrapper>();
            var random = new Random(42); // Fixed seed za konzistentnost
            
            // Uzmi validne uzorke kao osnovu
            var validSamples = originalSamples.Where(s => s.IsValid).Take(totalCount).ToList();
            
            for (int i = 0; i < totalCount && i < validSamples.Count; i++)
            {
                var originalSample = validSamples[i];
                var sampleType = random.NextDouble();
                
                if (sampleType < 0.1) // 10% - Invalid data za rejects
                {
                    result.Add(CreateInvalidSample(originalSample, i + 1));
                }
                else if (sampleType < 0.3) // 20% - Alert triggering data
                {
                    result.Add(CreateAlertSample(originalSample, i + 1, random));
                }
                else // 70% - Valid data za measurements
                {
                    result.Add(originalSample);
                }
            }
            
            return result;
        }
        
        // Kreiraj neispravne podatke za rejects.csv
        private static SampleWrapper CreateInvalidSample(SampleWrapper original, int sampleNumber)
        {
            var invalidSample = new SampleWrapper
            {
                IsValid = true, // Pustimo da prodje parsing, ali ce server da ga odbaci
                Sample = new DroneSample
                {
                    Time = original.Sample.Time,
                    WindSpeed = sampleNumber % 3 == 0 ? -1.0 : original.Sample.WindSpeed, // Negativan wind speed
                    WindAngle = original.Sample.WindAngle,
                    BatteryVoltage = sampleNumber % 4 == 0 ? 0.0 : original.Sample.BatteryVoltage, // Nula voltage
                    BatteryCurrent = original.Sample.BatteryCurrent,
                    PositionX = sampleNumber % 5 == 0 ? double.NaN : original.Sample.PositionX, // NaN vrednost
                    PositionY = original.Sample.PositionY,
                    PositionZ = original.Sample.PositionZ,
                    OrientationX = original.Sample.OrientationX,
                    OrientationY = original.Sample.OrientationY,
                    OrientationZ = original.Sample.OrientationZ,
                    OrientationW = original.Sample.OrientationW,
                    VelocityX = original.Sample.VelocityX,
                    VelocityY = original.Sample.VelocityY,
                    VelocityZ = original.Sample.VelocityZ,
                    AngularX = original.Sample.AngularX,
                    AngularY = original.Sample.AngularY,
                    AngularZ = original.Sample.AngularZ,
                    LinearAccelerationX = sampleNumber % 6 == 0 ? double.PositiveInfinity : original.Sample.LinearAccelerationX,
                    LinearAccelerationY = original.Sample.LinearAccelerationY,
                    LinearAccelerationZ = original.Sample.LinearAccelerationZ
                },
                LineNumber = original.LineNumber,
                ProcessingResult = "PENDING"
            };
            
            return invalidSample;
        }
        
        // Kreiraj podatke koji ce triggerovati alerte za analytics_alerts.csv
        private static SampleWrapper CreateAlertSample(SampleWrapper original, int sampleNumber, Random random)
        {
            var alertSample = new SampleWrapper
            {
                IsValid = true,
                Sample = new DroneSample
                {
                    Time = original.Sample.Time,
                    WindSpeed = random.NextDouble() < 0.5 ? 12.0 + random.NextDouble() * 8.0 : original.Sample.WindSpeed, // Jak vetar (>5.0 threshold)
                    WindAngle = 45.0 + random.NextDouble() * 90.0, // Ugao koji ce dati veliki wind effect
                    BatteryVoltage = original.Sample.BatteryVoltage,
                    BatteryCurrent = original.Sample.BatteryCurrent,
                    PositionX = original.Sample.PositionX,
                    PositionY = original.Sample.PositionY,
                    PositionZ = original.Sample.PositionZ,
                    OrientationX = original.Sample.OrientationX,
                    OrientationY = original.Sample.OrientationY,
                    OrientationZ = original.Sample.OrientationZ,
                    OrientationW = original.Sample.OrientationW,
                    VelocityX = original.Sample.VelocityX,
                    VelocityY = original.Sample.VelocityY,
                    VelocityZ = original.Sample.VelocityZ,
                    AngularX = original.Sample.AngularX,
                    AngularY = original.Sample.AngularY,
                    AngularZ = original.Sample.AngularZ,
                    // Velike promene ubrzanja za acceleration spike alert (>1.0 threshold)
                    LinearAccelerationX = original.Sample.LinearAccelerationX + (random.NextDouble() < 0.5 ? 3.0 : -3.0),
                    LinearAccelerationY = original.Sample.LinearAccelerationY + (random.NextDouble() < 0.5 ? 2.5 : -2.5),
                    LinearAccelerationZ = original.Sample.LinearAccelerationZ + (random.NextDouble() < 0.5 ? 4.0 : -4.0)
                },
                LineNumber = original.LineNumber,
                ProcessingResult = "PENDING"
            };
            
            return alertSample;
        }

        // Helper metod za prikaz napretka
        private static void UpdateProgressDisplay(int sent, int successful, int failed, int total)
        {
            if (sent == 1)
                Console.WriteLine("\n📡 Status prenosa:");
            
            if (sent % 10 == 0 || sent <= 5)
            {
                var percentage = (sent * 100) / total;
                var progressBar = new string('█', sent / 5) + new string('░', (total - sent) / 5);
                Console.Write($"\r[{progressBar}] {sent}/{total} ({percentage}%) | ✅{successful} ❌{failed}    ");
            }
        }
    }

    // Nova klasa za procesiranje CSV podataka sa LINQ pristupom
    public class DroneDataProcessor : IDisposable
    {
        private readonly string csvPath;
        private readonly string rejectPath;
        private FileStream csvStream;
        private StreamWriter rejectWriter;
        public int AcceptedCount { get; private set; }
        public int RejectedCount { get; private set; }

        public DroneDataProcessor(string csvPath, string rejectPath)
        {
            this.csvPath = csvPath;
            this.rejectPath = rejectPath;
            Directory.CreateDirectory(Path.GetDirectoryName(rejectPath));
            rejectWriter = new StreamWriter(rejectPath);
            rejectWriter.WriteLine("Reason,Line,ProcessingResult");
        }

        public IEnumerable<SampleWrapper> LoadSamples()
        {
            using (var reader = new StreamReader(csvPath))
            {
                string line;
                int lineNumber = 0;
                bool isFirstLine = true;

                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    
                    // Preskoci header
                    if (isFirstLine && line.ToLowerInvariant().Contains("time"))
                    {
                        isFirstLine = false;
                        continue;
                    }
                    isFirstLine = false;

                    // LINQ approach za parsiranje
                    var parseResult = TryParseLine(line, lineNumber);
                    
                    if (parseResult.IsValid)
                    {
                        AcceptedCount++;
                        yield return parseResult;
                    }
                    else
                    {
                        RejectedCount++;
                        rejectWriter.WriteLine($"{parseResult.ValidationError},{lineNumber},REJECTED");
                        rejectWriter.Flush();
                    }
                }
            }
        }

        private SampleWrapper TryParseLine(string line, int lineNumber)
        {
            try
            {
                var fields = line.Split(',')
                    .Select(f => f.Trim().Trim('"'))
                    .ToArray();

                if (fields.Length < 21)
                {
                    return new SampleWrapper
                    {
                        IsValid = false,
                        ValidationError = $"Nedovoljno polja: {fields.Length}/21",
                        LineNumber = lineNumber
                    };
                }

                // Funkcionalni pristup parsiranja
                var parser = new CsvFieldParser(fields);
                var sample = parser
                    .ParseDouble(0, "Time")
                    .ParseDouble(1, "WindSpeed", min: 0)
                    .ParseDouble(2, "WindAngle")
                    .ParseDouble(3, "BatteryVoltage", min: 0.1, max: 30)
                    .ParseDouble(4, "BatteryCurrent")
                    .ParseDoubleRange(5, 17) // Position, Orientation, Velocity, Angular
                    .ParseDoubleRange(18, 20) // LinearAcceleration
                    .BuildDroneSample();

                return new SampleWrapper
                {
                    Sample = sample,
                    IsValid = parser.IsValid,
                    ValidationError = parser.ErrorMessage,
                    LineNumber = lineNumber
                };
            }
            catch (Exception ex)
            {
                return new SampleWrapper
                {
                    IsValid = false,
                    ValidationError = $"Greska parsiranja: {ex.Message}",
                    LineNumber = lineNumber
                };
            }
        }

        public void Dispose()
        {
            csvStream?.Dispose();
            rejectWriter?.Dispose();
        }
    }

    // Wrapper klasa za uzorak sa dodatnim metapodacima
    public class SampleWrapper
    {
        public DroneSample Sample { get; set; }
        public bool IsValid { get; set; }
        public string ValidationError { get; set; }
        public int LineNumber { get; set; }
        public string ProcessingResult { get; set; }
        public string ProcessingError { get; set; }
    }

    // Fluent CSV parser sa metodama za chain-ovanje
    public class CsvFieldParser
    {
        private readonly string[] fields;
        private readonly Dictionary<string, double> parsedValues = new Dictionary<string, double>();
        public bool IsValid { get; private set; } = true;
        public string ErrorMessage { get; private set; } = "";

        public CsvFieldParser(string[] fields)
        {
            this.fields = fields;
        }

        public CsvFieldParser ParseDouble(int index, string fieldName, double min = double.MinValue, double max = double.MaxValue)
        {
            if (!IsValid) return this;

            if (index >= fields.Length)
            {
                IsValid = false;
                ErrorMessage = $"Nedostaje polje {fieldName} na poziciji {index}";
                return this;
            }

            if (double.TryParse(fields[index], NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                if (value >= min && value <= max && !double.IsNaN(value) && !double.IsInfinity(value))
                {
                    parsedValues[fieldName] = value;
                }
                else
                {
                    IsValid = false;
                    ErrorMessage = $"Vrednost za {fieldName} van opsega: {value} (min: {min}, maks: {max})";
                }
            }
            else
            {
                IsValid = false;
                ErrorMessage = $"Neispravan broj za {fieldName}: {fields[index]}";
            }

            return this;
        }

        public CsvFieldParser ParseDoubleRange(int startIndex, int endIndex)
        {
            for (int i = startIndex; i <= endIndex && i < fields.Length; i++)
            {
                ParseDouble(i, $"Field_{i}");
            }
            return this;
        }

        public DroneSample BuildDroneSample()
        {
            if (!IsValid) return null;

            return new DroneSample
            {
                Time = GetValue("Time"),
                WindSpeed = GetValue("WindSpeed"),
                WindAngle = GetValue("WindAngle"),
                BatteryVoltage = GetValue("BatteryVoltage"),
                BatteryCurrent = GetValue("BatteryCurrent"),
                PositionX = GetValue("Field_5"),
                PositionY = GetValue("Field_6"),
                PositionZ = GetValue("Field_7"),
                OrientationX = GetValue("Field_8"),
                OrientationY = GetValue("Field_9"),
                OrientationZ = GetValue("Field_10"),
                OrientationW = GetValue("Field_11"),
                VelocityX = GetValue("Field_12"),
                VelocityY = GetValue("Field_13"),
                VelocityZ = GetValue("Field_14"),
                AngularX = GetValue("Field_15"),
                AngularY = GetValue("Field_16"),
                AngularZ = GetValue("Field_17"),
                LinearAccelerationX = GetValue("Field_18"),
                LinearAccelerationY = GetValue("Field_19"),
                LinearAccelerationZ = GetValue("Field_20")
            };
        }

        private double GetValue(string key)
        {
            return parsedValues.ContainsKey(key) ? parsedValues[key] : 0.0;
        }
    }
}