using Common;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceModel;

namespace Server
{
    // Custom EventArgs класе за типизоване догађаје
    public class DroneSessionEventArgs : EventArgs
    {
        public string SessionId { get; set; }
        public DateTime Timestamp { get; set; }
        public int ProcessedSamples { get; set; }
        public string StoragePath { get; set; }
        public string Message { get; set; }
    }

    public class DroneSampleEventArgs : EventArgs
    {
        public int SampleNumber { get; set; }
        public double Time { get; set; }
        public double AccelerationNorm { get; set; }
        public double WindEffect { get; set; }
        public bool IsValid { get; set; }
        public string ValidationError { get; set; }
    }

    public class DroneAlertEventArgs : EventArgs
    {
        public AlertType Type { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; }
        public double Value { get; set; }
        public double Threshold { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }

    public enum AlertType
    {
        AccelerationSpike,
        WindSpike,
        OutOfBounds,
        ValidationError,
        SystemError
    }

    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Single)]
    public class DroneService : IDroneService, IDisposable
    {
        private readonly string storageRoot = ConfigurationManager.AppSettings["storagePath"] ?? "DroneStorage";
        private FileStream measurementsStream;
        private StreamWriter measurementsWriter;
        private FileStream rejectsStream;
        private StreamWriter rejectsWriter;
        private FileStream analyticsStream;
        private StreamWriter analyticsWriter;

        private string currentSessionId;
        private double aThreshold;
        private double wThreshold;
        private double deviationPct;
        private double? lastAccelerationNorm;
        private double runningMeanAcceleration;
        private long count;
        private int written;
        private int rejected; // Dodano za praćenje odbačenih uzoraka
        private readonly object lockObject = new object();
        private bool disposed = false;
        private DroneSample currentSampleForDisplay;

        // =====================================================================
        // ZADATAK 8: Delegate deklaracije po obrascu sa vezbi
        // (Vezba 6 praktikum: delegate void MyEventHandler(object sender, EventArgs e))
        // =====================================================================
        public delegate void DroneSessionEventHandler(object sender, DroneSessionEventArgs e);
        public delegate void DroneSampleEventHandler(object sender, DroneSampleEventArgs e);
        public delegate void DroneAlertEventHandler(object sender, DroneAlertEventArgs e);
        public delegate void DroneWarningEventHandler(object sender, string message);

        // Moderni dogadjaji bazirani na custom delegate-ima
        public event DroneSessionEventHandler SessionStarted;
        public event DroneSampleEventHandler  SampleProcessed;
        public event DroneSessionEventHandler SessionCompleted;
        public event DroneAlertEventHandler   AlertRaised;

        // Legacy dogadjaji - nazivi iz specifikacije projekta
        public event DroneWarningEventHandler OnTransferStarted;
        public event DroneWarningEventHandler OnSampleReceived;
        public event DroneWarningEventHandler OnTransferCompleted;
        public event DroneWarningEventHandler OnWarningRaised;
        public event DroneWarningEventHandler OnAccelerationSpike;
        public event DroneWarningEventHandler OnWindSpike;
        public event DroneWarningEventHandler OnOutOfBandWarning;

        public DroneService()
        {
            // Učitavanje konfiguracijskih parametara
            double.TryParse(ConfigurationManager.AppSettings["A_threshold"] ?? "1.0", out aThreshold);
            double.TryParse(ConfigurationManager.AppSettings["W_threshold"] ?? "5.0", out wThreshold);
            double.TryParse(ConfigurationManager.AppSettings["DeviationPercent"] ?? "25", out deviationPct);

            // Претплата на модерне догађаје са lambda изразима
            SessionStarted += (sender, args) => {
                Console.WriteLine($"\n[SESIJA ZAPOCETA] {args.SessionId}");
                Console.WriteLine($"  Vreme: {args.Timestamp:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"  Lokacija: {args.StoragePath}\n");
            };

            SampleProcessed += (sender, args) => {
                // ZADATAK 7: Ispis "prenos u toku..." tokom prijema uzoraka
                Console.WriteLine($"prenos u toku... uzorak #{args.SampleNumber}");

                if (args.SampleNumber <= 100)
                {
                    var sample = GetCurrentSampleData();
                    
                    Console.WriteLine($"UZORAK BROJ {args.SampleNumber}");
                    Console.WriteLine($"Vreme: {args.Time:F3} sekundi od pocetka");
                    Console.WriteLine();
                    
                    Console.WriteLine("UBRZANJE:");
                    Console.WriteLine($"  X komponenta: {sample?.LinearAccelerationX:F6} m/s²");
                    Console.WriteLine($"  Y komponenta: {sample?.LinearAccelerationY:F6} m/s²");
                    Console.WriteLine($"  Z komponenta: {sample?.LinearAccelerationZ:F6} m/s²");
                    Console.WriteLine($"  Ukupno (norma): {args.AccelerationNorm:F6} m/s²");
                    Console.WriteLine();
                    
                    Console.WriteLine("VETAR:");
                    Console.WriteLine($"  Brzina vetra: {sample?.WindSpeed:F3} m/s");
                    Console.WriteLine($"  Ugao vetra: {sample?.WindAngle:F1} stepeni");
                    Console.WriteLine($"  Efekat vetra: {args.WindEffect:F6} m/s");
                    Console.WriteLine();
                    
                    Console.WriteLine("BATERIJA:");
                    Console.WriteLine($"  Napon: {sample?.BatteryVoltage:F3} V");
                    Console.WriteLine($"  Struja: {sample?.BatteryCurrent:F6} A");
                    Console.WriteLine();
                    
                    Console.WriteLine("POZICIJA U PROSTORU:");
                    Console.WriteLine($"  X: {sample?.PositionX:F2}");
                    Console.WriteLine($"  Y: {sample?.PositionY:F2}");
                    Console.WriteLine($"  Z: {sample?.PositionZ:F2}");
                    Console.WriteLine();
                    
                    Console.WriteLine("BRZINA KRETANJA:");
                    Console.WriteLine($"  X: {sample?.VelocityX:F6}");
                    Console.WriteLine($"  Y: {sample?.VelocityY:F6}");
                    Console.WriteLine($"  Z: {sample?.VelocityZ:F6}");
                    Console.WriteLine();
                    
                    Console.WriteLine("ORIJENTACIJA (Quaternion):");
                    Console.WriteLine($"  X: {sample?.OrientationX:F6}");
                    Console.WriteLine($"  Y: {sample?.OrientationY:F6}");
                    Console.WriteLine($"  Z: {sample?.OrientationZ:F6}");
                    Console.WriteLine($"  W: {sample?.OrientationW:F6}");
                    Console.WriteLine();
                    
                    Console.WriteLine("UGAONA BRZINA:");
                    Console.WriteLine($"  X: {sample?.AngularX:F6}");
                    Console.WriteLine($"  Y: {sample?.AngularY:F6}");
                    Console.WriteLine($"  Z: {sample?.AngularZ:F6}");
                    Console.WriteLine();
                    
                    Console.WriteLine("================================================");
                    Console.WriteLine();
                }
                else if (args.SampleNumber == 101)
                {
                    Console.WriteLine("ZAVRSEN DETALJAN PRIKAZ");
                    Console.WriteLine("Prikazani su svi podaci za prvih 100 uzoraka");
                    Console.WriteLine("Nastavlja se obrada preostalih uzoraka...");
                    Console.WriteLine();
                }
                else if (args.SampleNumber % 10 == 0)
                {
                    Console.WriteLine($"Obradjen uzorak #{args.SampleNumber}");
                }
            };

            SessionCompleted += (sender, args) => {
                // ZADATAK 7: Ispis "zavrsen prenos" na kraju sesije
                Console.WriteLine("\nzavrsen prenos");
                Console.WriteLine($"\n[SESIJA ZAVRSENA] {args.SessionId}");
                Console.WriteLine($"  Obradjeno uzoraka: {args.ProcessedSamples}");
                Console.WriteLine($"  Trajanje: {(DateTime.UtcNow - args.Timestamp).TotalSeconds:F1}s\n");
            };

            AlertRaised += (sender, args) => {
                string severityPrefix;
                switch (args.Severity)
                {
                    case AlertSeverity.Critical:
                        severityPrefix = "[KRITICNO]";
                        break;
                    case AlertSeverity.Warning:
                        severityPrefix = "[UPOZORENJE]";
                        break;
                    default:
                        severityPrefix = "[INFO]";
                        break;
                }

                string typePrefix;
                switch (args.Type)
                {
                    case AlertType.AccelerationSpike:
                        typePrefix = "[NAGLA PROMENA UBRZANJA]";
                        break;
                    case AlertType.WindSpike:
                        typePrefix = "[UTICAJ VETRA]";
                        break;
                    case AlertType.OutOfBounds:
                        typePrefix = "[VAN OPSEGA]";
                        break;
                    case AlertType.ValidationError:
                        typePrefix = "[GRESKA VALIDACIJE]";
                        break;
                    default:
                        typePrefix = "[SISTEMSKA GRESKA]";
                        break;
                }
                Console.WriteLine($"\n{severityPrefix} {typePrefix} {args.Message}");
            };

            // Legacy компатибилност
            OnTransferStarted += (s, m) => Console.WriteLine($"\n[LEGACY] {m}\n");
            OnSampleReceived += (s, m) => { /* handled by SampleProcessed */ };
            OnTransferCompleted += (s, m) => Console.WriteLine($"\n[LEGACY] {m}\n");
            OnWarningRaised += (s, m) => { /* handled by AlertRaised */ };
        }

        public DroneServiceResponse StartSession(DroneSessionMeta meta)
        {
            lock (lockObject)
            {
                try
                {
                    if (meta == null)
                        throw new FaultException<ValidationFault>(new ValidationFault 
                        { 
                            Message = "Meta is null", 
                            Field = "meta" 
                        });

                    // Validacija meta podataka
                    if (string.IsNullOrWhiteSpace(meta.SessionId))
                        meta.SessionId = Guid.NewGuid().ToString("N");

                    currentSessionId = meta.SessionId;

                    // Kreiranje direktorijuma za sesiju
                    string sessionDir = Path.Combine(storageRoot, currentSessionId);
                    Directory.CreateDirectory(sessionDir);

                    // Kreiranje fajlova
                    measurementsStream = new FileStream(Path.Combine(sessionDir, "measurements_session.csv"), FileMode.Create, FileAccess.Write, FileShare.Read);
                    measurementsWriter = new StreamWriter(measurementsStream) { AutoFlush = true };
                    rejectsStream = new FileStream(Path.Combine(sessionDir, "rejects.csv"), FileMode.Create, FileAccess.Write, FileShare.Read);
                    rejectsWriter = new StreamWriter(rejectsStream) { AutoFlush = true };
                    analyticsStream = new FileStream(Path.Combine(sessionDir, "analytics_alerts.csv"), FileMode.Create, FileAccess.Write, FileShare.Read);
                    analyticsWriter = new StreamWriter(analyticsStream) { AutoFlush = true };

                    // Zaglavlja CSV fajlova
                    measurementsWriter.WriteLine(DroneTelemetrySample.CsvHeader());
                    rejectsWriter.WriteLine("Reason,Line");
                    analyticsWriter.WriteLine("Timestamp,AlertType,Message,Value,Threshold");

                    // Resetovanje statistika
                    lastAccelerationNorm = null;
                    runningMeanAcceleration = 0;
                    count = 0;
                    written = 0;
                    rejected = 0;

                    // Модерни догађај
                    SessionStarted?.Invoke(this, new DroneSessionEventArgs
                    {
                        SessionId = currentSessionId,
                        Timestamp = meta.Time,
                        ProcessedSamples = 0,
                        StoragePath = sessionDir,
                        Message = "Sesija uspesno inicijalizovana"
                    });

                    // Legacy подршка
                    OnTransferStarted?.Invoke(this, $"Session ID: {currentSessionId}\nStarted at: {meta.Time:yyyy-MM-dd HH:mm:ss}\nStorage folder: {sessionDir}");
                    
                    return new DroneServiceResponse
                    {
                        Type = ResponseType.ACK,
                        Status = SessionStatus.IN_PROGRESS,
                        Message = "Session started successfully",
                        SessionId = currentSessionId
                    };
                }
                catch (Exception ex)
                {
                    throw new FaultException<DataFormatFault>(new DataFormatFault 
                    { 
                        Message = ex.Message, 
                        Details = ex.StackTrace 
                    });
                }
            }
        }

        public DroneServiceResponse PushSample(DroneSample sample)
        {
            lock (lockObject)
            {
                try
                {
                    if (measurementsWriter == null)
                        throw new FaultException<ValidationFault>(new ValidationFault 
                        { 
                            Message = "Session not started", 
                            Field = "session" 
                        });

                    if (written == 0)
                    {
                        Console.WriteLine("\nPOCETAK OBRADE UZORAKA DRONA");
                        Console.WriteLine("Prikazuju se detaljan podaci za prvih 100 uzoraka iz 12.csv fajla");
                        Console.WriteLine("===============================================\n");
                    }

                    // Validacija uzorka - prvo proverimo da li je valjan
                    var validationResult = ValidateSampleWithResult(sample);
                    if (!validationResult.IsValid)
                    {
                        // Upiši u rejects.csv
                        rejectsWriter?.WriteLine($"ValidationError: {validationResult.ErrorMessage},{sample?.Time ?? -1}");
                        rejected++;
                        
                        Console.WriteLine($"❌ ODBAČEN uzorak #{rejected}: {validationResult.ErrorMessage}");
                        
                        // Vrati ACK sa porukom o odbacivanju - NE BACAJ EXCEPTION!
                        return new DroneServiceResponse
                        {
                            Type = ResponseType.ACK,  // ACK jer je uzorak uspešno obrađen (odbačen)
                            Status = SessionStatus.IN_PROGRESS,
                            Message = $"Sample rejected: {validationResult.ErrorMessage}",
                            SessionId = currentSessionId
                        };
                    }
                    
                    // Sacuvaj trenutni uzorak za prikaz
                    currentSampleForDisplay = sample;

                    // Kreiranje DroneTelemetrySample objekta
                    var telemetrySample = new DroneTelemetrySample
                    {
                        Time = sample.Time,
                        WindSpeed = sample.WindSpeed,
                        WindAngle = sample.WindAngle,
                        BatteryVoltage = sample.BatteryVoltage,
                        BatteryCurrent = sample.BatteryCurrent,
                        PositionX = sample.PositionX,
                        PositionY = sample.PositionY,
                        PositionZ = sample.PositionZ,
                        OrientationX = sample.OrientationX,
                        OrientationY = sample.OrientationY,
                        OrientationZ = sample.OrientationZ,
                        OrientationW = sample.OrientationW,
                        VelocityX = sample.VelocityX,
                        VelocityY = sample.VelocityY,
                        VelocityZ = sample.VelocityZ,
                        AngularX = sample.AngularX,
                        AngularY = sample.AngularY,
                        AngularZ = sample.AngularZ,
                        LinearAccelerationX = sample.LinearAccelerationX,
                        LinearAccelerationY = sample.LinearAccelerationY,
                        LinearAccelerationZ = sample.LinearAccelerationZ
                    };

                    try
                    {
                        // =====================================================================
                        // ZADATAK 7 + Vezbe 4/5: MemoryStream kao privremeni bafer
                        //
                        // Uzorak se prvo serijalizuje u MemoryStream (privremena memorija),
                        // a zatim se sadrzaj prenosi u FileStream (trajni zapis na disk).
                        // Ovo demonstrira upotrebu MemoryStream-a za prenos podataka
                        // izmedju aplikacijskih slojeva, kao sto je pokazano na Vezbama 4 i 5.
                        // =====================================================================
                        using (var memStream = new System.IO.MemoryStream())
                        {
                            // Serijalizujemo CSV red u bajtove i upisujemo u MemoryStream
                            var encoding = System.Text.Encoding.UTF8;
                            string csvLine = telemetrySample.ToCsvString() + Environment.NewLine;
                            byte[] lineBytes = encoding.GetBytes(csvLine);
                            memStream.Write(lineBytes, 0, lineBytes.Length);

                            // Pozicioniramo se na pocetak MemoryStream-a za citanje
                            memStream.Seek(0, System.IO.SeekOrigin.Begin);

                            // Citamo iz MemoryStream-a i upisujemo u FileStream kroz StreamWriter
                            string lineFromMemory = encoding.GetString(memStream.ToArray()).TrimEnd();
                            measurementsWriter.WriteLine(lineFromMemory);
                        }

                        written++;
                    }
                    catch (Exception ioex)
                    {
                        rejectsWriter?.WriteLine($"WriteError: {ioex.Message.Replace(',', ';')},{telemetrySample.ToCsvString()}");
                        rejected++;
                        Console.WriteLine($"❌ IO GREŠKA uzorak #{rejected}: {ioex.Message}");
                        
                        // Vrati ACK sa porukom o grešci - NE BACAJ EXCEPTION!
                        return new DroneServiceResponse
                        {
                            Type = ResponseType.ACK,  // ACK jer je greška obrađena
                            Status = SessionStatus.IN_PROGRESS,
                            Message = $"Sample processing failed: {ioex.Message}",
                            SessionId = currentSessionId
                        };
                    }

                    // ===== ANALITIKA 1: Detekcija nagle promene ubrzanja (ΔA) =====
                    AnalyzeAccelerationSpike(telemetrySample);

                    // ===== ANALITIKA 2: Detekcija uticaja vetra (Weffect) =====
                    AnalyzeWindEffect(telemetrySample);

                    // Модерни догађај за узорак
                    SampleProcessed?.Invoke(this, new DroneSampleEventArgs
                    {
                        SampleNumber = written,
                        Time = telemetrySample.Time,
                        AccelerationNorm = telemetrySample.AccelerationNorm,
                        WindEffect = telemetrySample.WindEffect,
                        IsValid = true,
                        ValidationError = null
                    });

                    // Legacy подршка
                    OnSampleReceived?.Invoke(this, "sample");
                    
                    return new DroneServiceResponse
                    {
                        Type = ResponseType.ACK,
                        Status = SessionStatus.IN_PROGRESS,
                        Message = $"Sample {written} processed successfully",
                        SessionId = currentSessionId
                    };
                }
                catch (FaultException)
                {
                    throw; // Re-throw WCF faults
                }
                catch (Exception ex)
                {
                    rejectsWriter?.WriteLine($"ProcessError: {ex.Message.Replace(',', ';')},{sample?.Time ?? -1}");
                    rejected++;
                    Console.WriteLine($"❌ PROCESSING GREŠKA uzorak #{rejected}: {ex.Message}");
                    
                    // Vrati ACK sa porukom o grešci - NE BACAJ EXCEPTION!
                    return new DroneServiceResponse
                    {
                        Type = ResponseType.ACK,  // ACK jer je greška obrađena
                        Status = SessionStatus.IN_PROGRESS,
                        Message = $"Sample processing error: {ex.Message}",
                        SessionId = currentSessionId
                    };
                }
            }
        }

        public DroneServiceResponse EndSession()
        {
            lock (lockObject)
            {
                if (measurementsWriter == null)
                    throw new FaultException<ValidationFault>(new ValidationFault 
                    { 
                        Message = "No active session", 
                        Field = "session" 
                    });

                Dispose();
                Console.WriteLine($"\n\nSession summary:");
                Console.WriteLine($"  Samples processed: {written}");
                Console.WriteLine($"  Samples rejected: {rejected}");
                Console.WriteLine($"  Files written to: {Path.Combine(storageRoot, currentSessionId)}");
                Console.WriteLine($"    - measurements_session.csv (telemetry data) - {written} entries");
                Console.WriteLine($"    - rejects.csv (validation errors) - {rejected} entries");
                Console.WriteLine($"    - analytics_alerts.csv (anomaly alerts)");
                
                // Модерни догађај за завршетак
                SessionCompleted?.Invoke(this, new DroneSessionEventArgs
                {
                    SessionId = currentSessionId,
                    Timestamp = DateTime.UtcNow,
                    ProcessedSamples = written,
                    StoragePath = Path.Combine(storageRoot, currentSessionId),
                    Message = $"Sesija zavrsena - obradjeno {written} uzoraka"
                });

                // Legacy подршка
                OnTransferCompleted?.Invoke(this, $"Session ID: {currentSessionId}\nSamples processed: {written}\nFiles created: measurements_session.csv, rejects.csv, analytics_alerts.csv");
                return new DroneServiceResponse
                {
                    Type = ResponseType.ACK,
                    Status = SessionStatus.COMPLETED,
                    Message = $"Session completed successfully. Processed {written} samples.",
                    SessionId = currentSessionId
                };
            }
        }

        // Helper klasa za validation rezultat
        private class ValidationResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; }
            public string FieldName { get; set; }
            public string ExpectedRange { get; set; }
        }
        
        private ValidationResult ValidateSampleWithResult(DroneSample sample)
        {
            var validationErrors = new List<string>();
            
            // Fluent-style валидација са колекцијом грешака
            ValidateRequired(sample, "sample", validationErrors)
                .ValidateNumericRange(sample?.WindSpeed, "WindSpeed", 0, double.MaxValue, validationErrors)
                .ValidateNumericRange(sample?.BatteryVoltage, "BatteryVoltage", 0.1, 30.0, validationErrors)
                .ValidateFiniteNumber(sample?.LinearAccelerationX, "LinearAccelerationX", validationErrors)
                .ValidateFiniteNumber(sample?.LinearAccelerationY, "LinearAccelerationY", validationErrors)
                .ValidateFiniteNumber(sample?.LinearAccelerationZ, "LinearAccelerationZ", validationErrors)
                .ValidateFiniteNumber(sample?.PositionX, "PositionX", validationErrors)
                .ValidateFiniteNumber(sample?.PositionY, "PositionY", validationErrors)
                .ValidateFiniteNumber(sample?.PositionZ, "PositionZ", validationErrors);

            // Ако има грешака, врати резултат
            if (validationErrors.Any())
            {
                var firstError = validationErrors.First().Split('|');
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = firstError[0],
                    FieldName = firstError.Length > 1 ? firstError[1] : "unknown",
                    ExpectedRange = firstError.Length > 2 ? firstError[2] : "valid value"
                };
            }
            
            return new ValidationResult { IsValid = true };
        }

        private void ValidateSample(DroneSample sample)
        {
            var result = ValidateSampleWithResult(sample);
            if (!result.IsValid)
            {
                throw new FaultException<ValidationFault>(new ValidationFault 
                { 
                    Message = result.ErrorMessage, 
                    Field = result.FieldName,
                    ExpectedRange = result.ExpectedRange
                });
            }
        }

        // Fluent validation builder pattern
        private DroneService ValidateRequired(object value, string fieldName, List<string> errors)
        {
            if (value == null)
                errors.Add($"Polje {fieldName} je obavezno|{fieldName}|not null");
            return this;
        }

        private DroneService ValidateNumericRange(double? value, string fieldName, double min, double max, List<string> errors)
        {
            if (!value.HasValue) return this;
            if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value < min || value > max)
                errors.Add($"Neispravna vrednost za {fieldName}: {value}|{fieldName}|{min} - {max}");
            return this;
        }

        private DroneService ValidateFiniteNumber(double? value, string fieldName, List<string> errors)
        {
            if (!value.HasValue) return this;
            if (double.IsNaN(value.Value) || double.IsInfinity(value.Value))
                errors.Add($"Neispravna numericka vrednost za {fieldName}: {value}|{fieldName}|finite number");
            return this;
        }

        private double GetAccelerationNorm(DroneSample sample)
        {
            return Math.Sqrt(sample.LinearAccelerationX * sample.LinearAccelerationX + 
                           sample.LinearAccelerationY * sample.LinearAccelerationY + 
                           sample.LinearAccelerationZ * sample.LinearAccelerationZ);
        }

        private DroneSample GetCurrentSampleData()
        {
            return currentSampleForDisplay;
        }

        private void AnalyzeAccelerationSpike(DroneTelemetrySample sample)
        {
            var acceleration = new AccelerationAnalyzer(sample.AccelerationNorm, aThreshold, deviationPct);
            
            // Анализирај нагле промене убрзања коришћењем Strategy pattern-а
            var spikeResult = acceleration.AnalyzeSpike(lastAccelerationNorm);
            if (spikeResult.IsSpike)
            {
                var notification = CreateSpikeNotification(spikeResult);
                BroadcastAlert("AccelerationSpike", notification, spikeResult.DeltaValue, aThreshold);
            }
            
            lastAccelerationNorm = acceleration.CurrentValue;

            // Статистичка анализа са експоненцијалним просеком
            var statisticsResult = acceleration.AnalyzeStatistics(ref runningMeanAcceleration, ref count);
            if (statisticsResult.IsOutOfBounds)
            {
                var notification = CreateOutOfBoundsNotification(statisticsResult);
                BroadcastAlert("OutOfBandWarning", notification, 
                    statisticsResult.CurrentValue, statisticsResult.BoundaryValue);
            }
        }

        // Helper класа за анализу убрзања са инкапсулираном логиком
        private class AccelerationAnalyzer
        {
            public double CurrentValue { get; }
            private readonly double threshold;
            private readonly double deviationPercent;

            public AccelerationAnalyzer(double currentValue, double threshold, double deviationPercent)
            {
                CurrentValue = currentValue;
                this.threshold = threshold;
                this.deviationPercent = deviationPercent;
            }

            public (bool IsSpike, double DeltaValue, string Direction) AnalyzeSpike(double? previousValue)
            {
                if (!previousValue.HasValue) return (false, 0, "");
                
                var delta = CurrentValue - previousValue.Value;
                var isSpike = Math.Abs(delta) > threshold;
                var direction = delta > 0 ? "IZNAD ocekivanog" : "ISPOD ocekivanog";
                
                return (isSpike, delta, direction);
            }

            public (bool IsOutOfBounds, double CurrentValue, double BoundaryValue, string Direction) 
                AnalyzeStatistics(ref double runningMean, ref long count)
            {
                // Експоненцијални покретни просек уместо аритметичког
                const double alpha = 0.1; // фактор заборављања
                runningMean = count == 0 ? CurrentValue : alpha * CurrentValue + (1 - alpha) * runningMean;
                count++;

                var tolerance = deviationPercent / 100.0;
                var lowerBound = runningMean * (1 - tolerance);
                var upperBound = runningMean * (1 + tolerance);

                if (CurrentValue < lowerBound)
                    return (true, CurrentValue, lowerBound, "ISPOD");
                if (CurrentValue > upperBound)
                    return (true, CurrentValue, upperBound, "IZNAD");

                return (false, CurrentValue, 0, "");
            }
        }

        private string CreateSpikeNotification((bool IsSpike, double DeltaValue, string Direction) result)
        {
            return $"ΔA={result.DeltaValue:F4} m/s² ({result.Direction}) | Prag: {aThreshold:F4} m/s²";
        }

        private string CreateOutOfBoundsNotification(
            (bool IsOutOfBounds, double CurrentValue, double BoundaryValue, string Direction) result)
        {
            return $"Ubrzanje {result.Direction.ToLower()} ocekivanog | A={result.CurrentValue:F4} {(result.Direction == "ISPOD" ? "<" : ">")} {result.BoundaryValue:F4} m/s²";
        }

        private void BroadcastAlert(string alertType, string message, double value, double threshold)
        {
            // Мапирање alert типова
            AlertType type;
            switch (alertType)
            {
                case "AccelerationSpike":
                    type = AlertType.AccelerationSpike;
                    break;
                case "OutOfBandWarning":
                    type = AlertType.OutOfBounds;
                    break;
                case "WindSpike":
                    type = AlertType.WindSpike;
                    break;
                default:
                    type = AlertType.SystemError;
                    break;
            }

            var severity = value > threshold * 2 ? AlertSeverity.Critical : AlertSeverity.Warning;

            // Модерни догађај
            AlertRaised?.Invoke(this, new DroneAlertEventArgs
            {
                Type = type,
                Severity = severity,
                Message = message,
                Value = value,
                Threshold = threshold,
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    { "alertType", alertType },
                    { "exceedsThresholdBy", (value - threshold) / threshold * 100 }
                }
            });

            // Legacy подршка и CSV логовање
            var csvMessage = $"{alertType} {message.Replace("|", " ")}";
            
            switch (alertType)
            {
                case "AccelerationSpike":
                    OnAccelerationSpike?.Invoke(this, message);
                    break;
                case "OutOfBandWarning":
                    OnOutOfBandWarning?.Invoke(this, message);
                    break;
            }
            
            OnWarningRaised?.Invoke(this, $"{alertType}: {message}");
            analyticsWriter?.WriteLine($"{DateTime.UtcNow:O},{alertType},{csvMessage},{value:F3},{threshold:F3}");
        }

        private void AnalyzeWindEffect(DroneTelemetrySample sample)
        {
            // Функционални приступ са lambda изразом и delegate-ом
            Func<double, double, double> calculateWindEffect = (speed, angleInDegrees) => 
                Math.Abs(speed * Math.Sin(angleInDegrees * Math.PI / 180.0));
            
            var windAnalysis = new
            {
                Effect = calculateWindEffect(sample.WindSpeed, sample.WindAngle),
                Speed = sample.WindSpeed,
                Angle = sample.WindAngle,
                Severity = GetWindSeverityLevel(sample.WindSpeed, sample.WindAngle)
            };

            // Алтернативни приступ - предикат за проверу прага
            Predicate<double> exceedsThreshold = effect => effect > wThreshold;
            
            if (exceedsThreshold(windAnalysis.Effect))
            {
                var windAlert = new WindAlertBuilder()
                    .WithEffect(windAnalysis.Effect)
                    .WithThreshold(wThreshold)
                    .WithWindConditions(windAnalysis.Speed, windAnalysis.Angle)
                    .WithSeverity(windAnalysis.Severity)
                    .Build();

                DispatchWindAlert(windAlert);
            }
        }

        // Builder pattern за креирање wind alert-а
        private class WindAlertBuilder
        {
            private double effect;
            private double threshold;
            private double windSpeed;
            private double windAngle;
            private string severity;

            public WindAlertBuilder WithEffect(double effect)
            {
                this.effect = effect;
                return this;
            }

            public WindAlertBuilder WithThreshold(double threshold)
            {
                this.threshold = threshold;
                return this;
            }

            public WindAlertBuilder WithWindConditions(double speed, double angle)
            {
                this.windSpeed = speed;
                this.windAngle = angle;
                return this;
            }

            public WindAlertBuilder WithSeverity(string severity)
            {
                this.severity = severity;
                return this;
            }

            public WindAlert Build()
            {
                return new WindAlert
                {
                    Effect = effect,
                    Threshold = threshold,
                    WindSpeed = windSpeed,
                    WindAngle = windAngle,
                    Severity = severity,
                    Message = $"Weffect={effect:F4} m/s iznad praga | Prag: {threshold:F4} m/s | Vetar: {windSpeed:F3} m/s pod uglom {windAngle:F1}° ({severity})",
                    CsvMessage = $"WIND SPIKE Weffect={effect:F3} m/s IZNAD ocekivanog Threshold={threshold:F3} m/s WindSpeed={windSpeed:F3} m/s WindAngle={windAngle:F1}° Severity={severity}"
                };
            }
        }

        private class WindAlert
        {
            public double Effect { get; set; }
            public double Threshold { get; set; }
            public double WindSpeed { get; set; }
            public double WindAngle { get; set; }
            public string Severity { get; set; }
            public string Message { get; set; }
            public string CsvMessage { get; set; }
        }

        private string GetWindSeverityLevel(double windSpeed, double windAngle)
        {
            // Kategorizacija jacine vetra
            if (windSpeed < 2.0)
                return "SLAB";
            else if (windSpeed < 5.0)
                return "UMEREN";
            else if (windSpeed < 10.0)
                return "JAK";
            else
                return "VRLO JAK";
        }

        private void DispatchWindAlert(WindAlert alert)
        {
            // Модерни Wind Alert догађај
            AlertRaised?.Invoke(this, new DroneAlertEventArgs
            {
                Type = AlertType.WindSpike,
                Severity = alert.Severity == "VRLO JAK" ? AlertSeverity.Critical : AlertSeverity.Warning,
                Message = alert.Message,
                Value = alert.Effect,
                Threshold = alert.Threshold,
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    { "windSpeed", alert.WindSpeed },
                    { "windAngle", alert.WindAngle },
                    { "windSeverity", alert.Severity }
                }
            });

            // Legacy подршка
            OnWindSpike?.Invoke(this, alert.Message);
            OnWarningRaised?.Invoke(this, $"WIND SPIKE: {alert.Message}");
            analyticsWriter?.WriteLine($"{DateTime.UtcNow:O},WindSpike,{alert.CsvMessage},{alert.Effect:F3},{alert.Threshold:F3}");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            lock (lockObject)
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                        // Ослободи управљане ресурсе
                        try { measurementsWriter?.Flush(); measurementsWriter?.Dispose(); } catch { }
                        try { measurementsStream?.Dispose(); } catch { }
                        try { rejectsWriter?.Flush(); rejectsWriter?.Dispose(); } catch { }
                        try { rejectsStream?.Dispose(); } catch { }
                        try { analyticsWriter?.Flush(); analyticsWriter?.Dispose(); } catch { }
                        try { analyticsStream?.Dispose(); } catch { }
                        
                        measurementsWriter = null; measurementsStream = null; 
                        rejectsWriter = null; rejectsStream = null;
                        analyticsWriter = null; analyticsStream = null;
                    }
                    
                    disposed = true;
                }
            }
        }

        ~DroneService()
        {
            Dispose(false);
        }
    }
}
