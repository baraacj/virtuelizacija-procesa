using Common;
using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.ServiceModel;

namespace Server
{
    // =========================================================================
    // ZADATAK 2: Implementacija WCF servisa
    //   - ServiceBehavior: jedan singleton, jedna nit
    //   - Implementira IDroneService (ServiceContract) i IDisposable
    //
    // ZADATAK 3: Validacija podataka + standardizovani izuzeci (Fault)
    //
    // ZADATAK 4: Dispose pattern za FileStream/StreamWriter resurse
    // =========================================================================

    [ServiceBehavior(
        InstanceContextMode = InstanceContextMode.Single,
        ConcurrencyMode     = ConcurrencyMode.Single)]
    public class DroneService : IDroneService, IDisposable
    {
        // =====================================================================
        // ZADATAK 2: Konfiguracija cita se iz app.config
        // =====================================================================
        private readonly double aThreshold;
        private readonly double wThreshold;
        private readonly double deviationPct;
        private readonly string storageRoot;

        // =====================================================================
        // ZADATAK 4: Resursi kojima upravljamo kroz Dispose pattern
        //   - FileStream i StreamWriter za svaki izlazni fajl
        // =====================================================================
        private FileStream   measurementsStream;
        private StreamWriter measurementsWriter;
        private FileStream   rejectsStream;
        private StreamWriter rejectsWriter;

        // Stanje sesije
        private string currentSessionId;
        private int    writtenCount;
        private int    rejectedCount;

        // Zastitni flag za Dispose
        private bool          disposed  = false;
        private readonly object syncLock = new object();

        // =====================================================================
        // ZADATAK 2: Konstruktor - ucitava pragove iz app.config
        // =====================================================================
        public DroneService()
        {
            double.TryParse(
                ConfigurationManager.AppSettings["A_threshold"] ?? "1.5",
                NumberStyles.Float, CultureInfo.InvariantCulture,
                out aThreshold);

            double.TryParse(
                ConfigurationManager.AppSettings["W_threshold"] ?? "2.0",
                NumberStyles.Float, CultureInfo.InvariantCulture,
                out wThreshold);

            double.TryParse(
                ConfigurationManager.AppSettings["DeviationPercent"] ?? "25",
                NumberStyles.Float, CultureInfo.InvariantCulture,
                out deviationPct);

            storageRoot = ConfigurationManager.AppSettings["storagePath"] ?? "DroneStorage";

            Console.WriteLine($"[Servis pokrenut]");
            Console.WriteLine($"  A_threshold      = {aThreshold}");
            Console.WriteLine($"  W_threshold      = {wThreshold}");
            Console.WriteLine($"  DeviationPercent = {deviationPct}%");
            Console.WriteLine($"  StoragePath      = {storageRoot}");
            Console.WriteLine();
        }

        // =====================================================================
        // ZADATAK 1 + 2: StartSession
        //   - Validira meta-zaglavlje
        //   - Kreira direktorijum i CSV fajlove za sesiju
        //   - Vraca ACK + IN_PROGRESS
        // =====================================================================
        public DroneServiceResponse StartSession(DroneSessionMeta meta)
        {
            lock (syncLock)
            {
                try
                {
                    // ZADATAK 3: Validacija meta objekta
                    if (meta == null)
                        throw new FaultException<ValidationFault>(
                            new ValidationFault
                            {
                                Message       = "Meta-zaglavlje je null",
                                Field         = "meta",
                                ExpectedRange = "non-null"
                            });

                    if (string.IsNullOrWhiteSpace(meta.SessionId))
                        meta.SessionId = Guid.NewGuid().ToString("N");

                    currentSessionId = meta.SessionId;

                    // Kreiranje direktorijuma za sesiju
                    string sessionDir = Path.Combine(storageRoot, currentSessionId);
                    Directory.CreateDirectory(sessionDir);

                    // =========================================================
                    // ZADATAK 4: Otvaranje FileStream i StreamWriter resursa.
                    //            Ovi resursi se zatvaraju u Dispose().
                    // =========================================================
                    string measurementsPath = Path.Combine(sessionDir, "measurements_session.csv");
                    string rejectsPath      = Path.Combine(sessionDir, "rejects.csv");

                    measurementsStream = new FileStream(
                        measurementsPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                    measurementsWriter = new StreamWriter(measurementsStream) { AutoFlush = true };

                    rejectsStream = new FileStream(
                        rejectsPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                    rejectsWriter = new StreamWriter(rejectsStream) { AutoFlush = true };

                    // Zaglavlja CSV fajlova
                    measurementsWriter.WriteLine(DroneTelemetrySample.CsvHeader());
                    rejectsWriter.WriteLine("Razlog,OriginalniRed");

                    // Resetovanje brojaca
                    writtenCount  = 0;
                    rejectedCount = 0;

                    Console.WriteLine($"[StartSession] {currentSessionId}");
                    Console.WriteLine($"  Fajlovi: {sessionDir}");
                    Console.WriteLine();

                    // ZADATAK 1: Vraca ACK + IN_PROGRESS
                    return new DroneServiceResponse
                    {
                        Type      = ResponseType.ACK,
                        Status    = SessionStatus.IN_PROGRESS,
                        Message   = "Sesija uspesno pokrenuta",
                        SessionId = currentSessionId
                    };
                }
                catch (FaultException) { throw; }
                catch (Exception ex)
                {
                    // ZADATAK 3: Neocekivane greske se umotavaju u DataFormatFault
                    throw new FaultException<DataFormatFault>(
                        new DataFormatFault
                        {
                            Message = $"Greska pri pokretanju sesije: {ex.Message}",
                            Details = ex.StackTrace
                        });
                }
            }
        }

        // =====================================================================
        // ZADATAK 1 + 2 + 3: PushSample
        //   - Validira uzorak (tipovi, obavezna polja, dozvoljeni opsezi)
        //   - Na greske vraca NACK + upisuje u rejects.csv
        //   - Validne uzorke upisuje u measurements_session.csv
        //   - Vraca ACK + IN_PROGRESS
        // =====================================================================
        public DroneServiceResponse PushSample(DroneSample sample)
        {
            lock (syncLock)
            {
                if (measurementsWriter == null)
                    throw new FaultException<ValidationFault>(
                        new ValidationFault
                        {
                            Message       = "Sesija nije pokrenuta - pozovite StartSession najpre",
                            Field         = "session",
                            ExpectedRange = "active session"
                        });

                // =====================================================================
                // ZADATAK 3: Validacija tipova, obaveznih polja i dozvoljenih opsega
                // =====================================================================
                string validationError = Validate(sample);

                if (validationError != null)
                {
                    // Odbacen uzorak - upisujemo u rejects.csv
                    string safeError = validationError.Replace(",", ";");
                    rejectsWriter.WriteLine($"{safeError},{sample?.Time.ToString(CultureInfo.InvariantCulture) ?? "?"}");
                    rejectedCount++;

                    Console.WriteLine($"  [NACK] Odbacen uzorak #{writtenCount + rejectedCount}: {validationError}");

                    // ZADATAK 1: Vraca NACK sa opisom greske
                    return new DroneServiceResponse
                    {
                        Type      = ResponseType.NACK,
                        Status    = SessionStatus.IN_PROGRESS,
                        Message   = $"Odbacen: {validationError}",
                        SessionId = currentSessionId
                    };
                }

                // Validan uzorak - upisujemo u measurements_session.csv
                var ts = DroneTelemetrySample.FromDroneSample(sample);
                measurementsWriter.WriteLine(ts.ToCsvLine());
                writtenCount++;

                Console.WriteLine($"  [ACK]  Uzorak #{writtenCount} primljen " +
                                  $"(t={sample.Time:F2}s)");

                // ZADATAK 1: Vraca ACK + IN_PROGRESS
                return new DroneServiceResponse
                {
                    Type      = ResponseType.ACK,
                    Status    = SessionStatus.IN_PROGRESS,
                    Message   = $"Uzorak {writtenCount} uspesno primljen",
                    SessionId = currentSessionId
                };
            }
        }

        // =====================================================================
        // ZADATAK 1 + 2: EndSession
        //   - Zatvara resurse kroz Dispose()
        //   - Vraca ACK + COMPLETED
        // =====================================================================
        public DroneServiceResponse EndSession()
        {
            lock (syncLock)
            {
                if (measurementsWriter == null)
                    throw new FaultException<ValidationFault>(
                        new ValidationFault
                        {
                            Message       = "Nema aktivne sesije",
                            Field         = "session",
                            ExpectedRange = "active session"
                        });

                string savedId  = currentSessionId;
                string savedDir = Path.Combine(storageRoot, currentSessionId);
                int    saved    = writtenCount;
                int    rejected = rejectedCount;

                // ZADATAK 4: Zatvaranje svih resursa kroz Dispose
                Dispose();

                Console.WriteLine($"\n[EndSession] {savedId}");
                Console.WriteLine($"  Primljeno uzoraka:  {saved}");
                Console.WriteLine($"  Odbaceno uzoraka:   {rejected}");
                Console.WriteLine($"  Fajlovi u:          {savedDir}");
                Console.WriteLine();

                // ZADATAK 1: Vraca ACK + COMPLETED
                return new DroneServiceResponse
                {
                    Type      = ResponseType.ACK,
                    Status    = SessionStatus.COMPLETED,
                    Message   = $"Sesija zavrsena. Primljeno: {saved}, Odbaceno: {rejected}",
                    SessionId = savedId
                };
            }
        }

        // =====================================================================
        // ZADATAK 3: Validacija uzorka
        //   - Proverava obavezna polja, tipove i dozvoljene opsege
        //   - Vraca null ako je uzorak validan
        //   - Vraca string sa opisom greske za nevalidne uzorke
        //
        //   Standardizovani izuzeci: DataFormatFault, ValidationFault
        // =====================================================================
        private string Validate(DroneSample s)
        {
            if (s == null)
                return "Uzorak je null";

            // Obavezna polja - WindSpeed mora biti > 0
            if (s.WindSpeed <= 0)
                return $"WindSpeed mora biti > 0 (vrednost: {s.WindSpeed})";

            // BatteryVoltage mora biti > 0
            if (s.BatteryVoltage <= 0)
                return $"BatteryVoltage mora biti > 0 (vrednost: {s.BatteryVoltage})";

            // LinearAcceleration ne sme biti NaN ni Infinity
            if (double.IsNaN(s.LinearAccelerationX) || double.IsInfinity(s.LinearAccelerationX))
                return $"LinearAccelerationX nije validan broj: {s.LinearAccelerationX}";

            if (double.IsNaN(s.LinearAccelerationY) || double.IsInfinity(s.LinearAccelerationY))
                return $"LinearAccelerationY nije validan broj: {s.LinearAccelerationY}";

            if (double.IsNaN(s.LinearAccelerationZ) || double.IsInfinity(s.LinearAccelerationZ))
                return $"LinearAccelerationZ nije validan broj: {s.LinearAccelerationZ}";

            // Pozicija ne sme biti NaN
            if (double.IsNaN(s.PositionX) || double.IsNaN(s.PositionY) || double.IsNaN(s.PositionZ))
                return "Pozicija sadrzi NaN vrednost";

            return null; // Uzorak je validan
        }

        // =====================================================================
        // ZADATAK 4: Dispose pattern
        //
        //   Ispravna implementacija IDisposable:
        //   1. Javni Dispose() poziva Dispose(true) + GC.SuppressFinalize
        //   2. Zasticeni virtual Dispose(bool) zatvara upravljane resurse
        //   3. Finalizer ~DroneService() poziva Dispose(false) kao sigurnosna mreza
        //   4. Svako zatvaranje je u try/catch da bi se nastavilo cak i pri gresci
        //   5. Flag 'disposed' sprecava dvostruko zatvaranje
        //
        //   Resursi koji se zatvaraju: FileStream i StreamWriter za
        //   measurements_session.csv i rejects.csv
        // =====================================================================
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            lock (syncLock)
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                        // Flush pre zatvaranja da osiguramo da su svi podaci zapisani
                        try { measurementsWriter?.Flush();   } catch { }
                        try { measurementsWriter?.Dispose(); } catch { }
                        try { measurementsStream?.Dispose(); } catch { }

                        try { rejectsWriter?.Flush();   } catch { }
                        try { rejectsWriter?.Dispose(); } catch { }
                        try { rejectsStream?.Dispose(); } catch { }

                        // Nulliramo reference da ne dodje do dvostrukog zatvaranja
                        measurementsWriter = null;
                        measurementsStream = null;
                        rejectsWriter      = null;
                        rejectsStream      = null;
                    }

                    disposed = true;
                }
            }
        }

        // Finalizer - sigurnosna mreza u slucaju da Dispose nije eksplicitno pozvan
        // (na primer, pri izuzetku usred prenosa)
        ~DroneService()
        {
            Dispose(false);
        }
    }
}
