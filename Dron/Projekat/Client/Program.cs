using Common;
using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.ServiceModel;

namespace Client
{
    // =========================================================================
    // ZADATAK 1 + 5: Klijent
    //
    //   ZADATAK 1 - Pravila protokola:
    //     - Salje StartSession sa meta-zaglavljem
    //     - For petljom prolazi kroz CSV i salje PushSample za svaki red
    //     - Na kraju poziva EndSession
    //     - Stampuje ACK/NACK i status IN_PROGRESS/COMPLETED
    //
    //   ZADATAK 5 - Rad sa fajlovima:
    //     - Odredjuje putanju do CSV fajla pretragom direktorijuma
    //     - Ucitava prvih 100 redova
    //     - Nevalidne redove prijavljuje u izdvojeni rejects log
    // =========================================================================
    class Program
    {
        // Maksimalan broj redova koji se ucitava (Zadatak 5)
        private const int MAX_ROWS = 100;

        static void Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("   Drone Telemetrija - Klijent (Projekat)");
            Console.WriteLine("========================================\n");

            // =====================================================================
            // ZADATAK 5: Pronalazenje putanje do CSV fajla
            // =====================================================================
            string csvPath = FindCsvPath();
            if (csvPath == null)
            {
                Console.WriteLine("GRESKA: CSV fajl nije pronadjen.");
                Console.WriteLine("Ocekivana putanja: <exe_dir>\\Dataset\\12.csv");
                Console.WriteLine("\nPritisnite Enter za izlaz...");
                Console.ReadLine();
                return;
            }

            Console.WriteLine($"CSV fajl: {csvPath}");
            Console.WriteLine($"Ucitavam prvih {MAX_ROWS} redova...\n");

            // Putanja za klijentski rejects log (Zadatak 5)
            string sessionId = Guid.NewGuid().ToString("N");
            string rejectPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Dataset",
                $"rejects_client_{sessionId}.csv");

            // =====================================================================
            // ZADATAK 2: Konekcija na WCF servis (netTcpBinding)
            // =====================================================================
            ChannelFactory<IDroneService> factory = null;
            IDroneService proxy = null;

            try
            {
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
                factory = new ChannelFactory<IDroneService>(binding, endpoint);
                proxy = factory.CreateChannel();
                ((ICommunicationObject)proxy).Open();

                Console.WriteLine("Konekcija uspostavljena.\n");
            }
            catch (EndpointNotFoundException)
            {
                Console.WriteLine("GRESKA: Server nije dostupan na net.tcp://localhost:4100");
                Console.WriteLine("Pokrenite Server.exe pre klijenta.");
                Pause();
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GRESKA pri konekciji: {ex.Message}");
                Pause();
                return;
            }

            try
            {
                // =================================================================
                // ZADATAK 1: StartSession sa meta-zaglavljem sesije
                // Polja: LinearAccelerationX/Y/Z, WindSpeed, WindAngle, Time
                // =================================================================
                var meta = new DroneSessionMeta
                {
                    SessionId = sessionId,
                    Time = DateTime.UtcNow,
                    LinearAccelerationX = 0.0,
                    LinearAccelerationY = 0.0,
                    LinearAccelerationZ = -9.81,   // gravitacija kao pocetna vrednost
                    WindSpeed = 0.0,
                    WindAngle = 0.0
                };

                Console.Write("Pokretanje sesije... ");
                DroneServiceResponse startResp = proxy.StartSession(meta);

                // ZADATAK 1: Stampujemo ACK/NACK i status
                Console.WriteLine($"{startResp.Type} / {startResp.Status}");

                if (startResp.Type != ResponseType.ACK)
                {
                    Console.WriteLine($"Server odbio sesiju: {startResp.Message}");
                    return;
                }

                Console.WriteLine($"SessionId: {startResp.SessionId}\n");

                // =================================================================
                // ZADATAK 1 + 5: Sekvencijalno slanje uzoraka
                //
                //   ZADATAK 1: Klijent prolazi for petljom kroz CSV i salje
                //              po jedan red (PushSample) - sekvencijalni protokol
                //
                //   ZADATAK 5: DroneCsvSampleReader cita prvih 100 redova,
                //              nevalidne prijavljuje u rejects log
                // =================================================================
                int sent = 0;
                int successful = 0;
                int rejected = 0;

                Console.WriteLine($"--- Slanje uzoraka (max {MAX_ROWS}) ---\n");

                // ZADATAK 4: 'using' garantuje Dispose() cak i pri izuzetku
                using (var reader = new DroneCsvSampleReader(csvPath, rejectPath))
                {
                    // ZADATAK 1 (b): For petlja - klijent prolazi kroz CSV
                    //                i salje po jedan red
                    for (int row = 0; row < MAX_ROWS; row++)
                    {
                        // ZADATAK 5: Citanje sledeceg validnog uzorka
                        if (!reader.TryReadNext(out DroneSample sample))
                        {
                            Console.WriteLine($"\nKraj fajla posle {row} redova.");
                            break;
                        }

                        // ZADATAK 1 (b): Slanje jednog uzorka serveru
                        try
                        {
                            DroneServiceResponse resp = proxy.PushSample(sample);
                            sent++;

                            // ZADATAK 1 (d): Stampujemo ACK/NACK i status
                            if (resp.Type == ResponseType.ACK)
                            {
                                successful++;
                                Console.WriteLine($"  #{sent:D3}  ACK / {resp.Status}  t={sample.Time:F2}s");
                            }
                            else
                            {
                                rejected++;
                                Console.WriteLine($"  #{sent:D3}  NACK / {resp.Status}  {resp.Message}");
                            }
                        }
                        catch (FaultException<ValidationFault> ex)
                        {
                            rejected++;
                            Console.WriteLine($"  #{sent + 1:D3}  [ValidationFault] " +
                                              $"Polje={ex.Detail.Field}: {ex.Detail.Message}");
                        }
                        catch (FaultException<DataFormatFault> ex)
                        {
                            rejected++;
                            Console.WriteLine($"  #{sent + 1:D3}  [DataFormatFault] {ex.Detail.Message}");
                        }
                        catch (CommunicationException ex)
                        {
                            // Prekid veze - prekidamo petlju
                            Console.WriteLine($"\n[CommunicationException] {ex.Message}");
                            Console.WriteLine("Prenos prekinut.");
                            break;
                        }
                    }

                    // ZADATAK 5: Izvestaj o citanju CSV fajla
                    Console.WriteLine($"\n--- Izvestaj citanja CSV ---");
                    Console.WriteLine($"  Prihvaceno redova (parser): {reader.AcceptedCount}");
                    Console.WriteLine($"  Odbaceno redova  (parser): {reader.RejectedCount}");
                    if (reader.RejectedCount > 0)
                        Console.WriteLine($"  Rejects log: {Path.GetFileName(rejectPath)}");
                }
                // ZADATAK 4: Ovde je DroneCsvSampleReader.Dispose() vec pozvan
                //            kroz 'using' - fajlovi su zatvoreni

                // =================================================================
                // ZADATAK 1: EndSession - kraj prenosa
                // =================================================================
                Console.Write("\nZavravanje sesije... ");
                DroneServiceResponse endResp = proxy.EndSession();

                // ZADATAK 1 (d): Stampujemo ACK i COMPLETED status
                Console.WriteLine($"{endResp.Type} / {endResp.Status}");
                Console.WriteLine($"Poruka: {endResp.Message}");

                // Sumarni izvestaj
                Console.WriteLine($"\n--- Sumarni izvestaj prenosa ---");
                Console.WriteLine($"  Poslato ukupno:  {sent}");
                Console.WriteLine($"  Uspesno (ACK):   {successful}");
                Console.WriteLine($"  Odbaceno (NACK): {rejected}");
            }
            catch (FaultException<ValidationFault> ex)
            {
                Console.WriteLine($"\n[ValidationFault] {ex.Detail.Message}");
                Console.WriteLine($"  Polje:   {ex.Detail.Field}");
                Console.WriteLine($"  Opseg:   {ex.Detail.ExpectedRange}");
            }
            catch (FaultException<DataFormatFault> ex)
            {
                Console.WriteLine($"\n[DataFormatFault] {ex.Detail.Message}");
                if (!string.IsNullOrEmpty(ex.Detail.Details))
                    Console.WriteLine($"  Detalji: {ex.Detail.Details}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nNeocekivana greska: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                // ZADATAK 4: Zatvaranje WCF kanala
                try
                {
                    if (proxy is ICommunicationObject comm)
                        comm.Close();
                    factory?.Close();
                }
                catch { }
            }

            Pause();
        }

        // =====================================================================
        // ZADATAK 5: Pretraga CSV fajla na vise mogucih lokacija
        // =====================================================================
        private static string FindCsvPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Gradimo listu svih mogucih putanja
            var candidates = new System.Collections.Generic.List<string>
            {
                // bin\Debug\Dataset\12.csv  (najcesca lokacija)
                Path.Combine(baseDir, "Dataset", "12.csv"),
                // bin\Debug\net472\Dataset\12.csv  (SDK-style projekti)
                Path.Combine(baseDir, "net472", "Dataset", "12.csv"),
                // Jedan nivo gore od bin\Debug
                Path.Combine(baseDir, "..", "Dataset", "12.csv"),
                // Dva nivoa gore (bin folder)
                Path.Combine(baseDir, "..", "..", "Dataset", "12.csv"),
                // Direktorijum projekta
                Path.Combine(baseDir, "..", "..", "..", "Dataset", "12.csv"),
                Path.Combine(baseDir, "..", "..", "..", "Client", "Dataset", "12.csv"),
            };

            Console.WriteLine("Trazim CSV fajl na sledecim putanjama:");
            foreach (string c in candidates)
            {
                string full = Path.GetFullPath(c);
                bool exists = File.Exists(full);
                Console.WriteLine($"  [{(exists ? "NADJEN" : "      ")}] {full}");
                if (exists)
                    return full;
            }

            // Poslednji pokusaj - pretraga svih Dataset foldera u blizini
            Console.WriteLine("\nPretraga Dataset foldera...");
            string[] searchRoots =
            {
                baseDir,
                Path.GetFullPath(Path.Combine(baseDir, "..")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..")),
            };

            foreach (string root in searchRoots)
            {
                string datasetDir = Path.Combine(root, "Dataset");
                if (Directory.Exists(datasetDir))
                {
                    Console.WriteLine($"  Pretrazujem: {datasetDir}");
                    foreach (string f in Directory.GetFiles(datasetDir, "*.csv"))
                    {
                        if (!Path.GetFileName(f).StartsWith("rejects_"))
                        {
                            Console.WriteLine($"  [NADJEN] {f}");
                            return f;
                        }
                    }
                }
            }

            return null;
        }

        private static void Pause()
        {
            Console.WriteLine("\nPritisnite Enter za izlaz...");
            Console.ReadLine();
        }
    }
}