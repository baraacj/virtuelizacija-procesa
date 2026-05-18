using Common;
using System;
using System.IO;

namespace Client
{
    // =========================================================================
    // ZADATAK 4 + 5: DroneCsvSampleReader
    //
    //   ZADATAK 4 - Dispose pattern:
    //     - Implementira IDisposable sa Dispose(bool) i finalizerom
    //     - Upravlja sa dva FileStream-a (CSV ulaz + rejects izlaz)
    //       i dva StreamWriter-a
    //     - Dokazuje zatvaranje resursa: konstruktor otvara, Dispose zatvara,
    //       finalizer je sigurnosna mreza pri izuzetku
    //
    //   ZADATAK 5 - Rad sa fajlovima:
    //     - Otvara CSV fajl kao FileStream sa FileShare.Read
    //     - Parsira redove sa invariant culture (tacka kao decimalni separator)
    //     - Ucitava prvih 100 validnih redova
    //     - Nevalidne/visak redova prijavljuje u izdvojeni rejects log
    // =========================================================================
    public class DroneCsvSampleReader : IDisposable
    {
        // ZADATAK 4: Resursi kojima upravljamo
        private readonly FileStream   csvStream;
        private readonly StreamReader csvReader;
        private readonly FileStream   rejectStream;
        private readonly StreamWriter rejectWriter;

        private bool headerSkipped = false;
        private bool disposed      = false;

        // Statistika citanja
        public int AcceptedCount { get; private set; }
        public int RejectedCount { get; private set; }

        // =====================================================================
        // ZADATAK 4 + 5: Konstruktor otvara fajlove
        //   Ako dodje do greske pri otvaranju, Dispose() cisti parcijalno
        //   otvorene resurse - dokazuje ispravno upravljanje resursima
        // =====================================================================
        public DroneCsvSampleReader(string csvPath, string rejectLogPath)
        {
            try
            {
                // ZADATAK 5: Kreiranje direktorijuma za rejects log ako ne postoji
                string rejectDir = Path.GetDirectoryName(rejectLogPath);
                if (!string.IsNullOrEmpty(rejectDir))
                    Directory.CreateDirectory(rejectDir);

                // ZADATAK 5: Otvaranje CSV fajla kao FileStream
                csvStream  = new FileStream(
                    csvPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                csvReader  = new StreamReader(csvStream);

                // ZADATAK 5: Izdvojeni log za nevalidne redove
                rejectStream = new FileStream(
                    rejectLogPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                rejectWriter = new StreamWriter(rejectStream) { AutoFlush = true };
                rejectWriter.WriteLine("Razlog,OriginalniRed");
            }
            catch
            {
                // ZADATAK 4: Ciscenje parcijalno otvorenih resursa pri gresci
                // u konstruktoru - sprecava curenje resursa (resource leak)
                Dispose();
                throw;
            }
        }

        // =====================================================================
        // ZADATAK 5: TryReadNext - cita sledeci validan uzorak iz CSV
        //
        //   - Preskace header red
        //   - Parsira sa invariant culture (tacka kao decimalni separator)
        //   - Nevalidne redove upisuje u rejects log i nastavlja citanje
        //   - Vraca false na kraju fajla
        // =====================================================================
        public bool TryReadNext(out DroneSample sample)
        {
            sample = null;

            while (true)
            {
                string line = csvReader.ReadLine();

                // Kraj fajla
                if (line == null)
                    return false;

                // Preskakanje header reda (samo jednom, na pocetku)
                if (!headerSkipped)
                {
                    headerSkipped = true;
                    string lower = line.ToLowerInvariant();
                    if (lower.Contains("time") && lower.Contains("wind"))
                        continue;
                }

                // ZADATAK 5: Parsiranje sa invariant culture
                if (DroneTelemetrySample.TryParseCsv(line, out DroneTelemetrySample ts, out string error))
                {
                    AcceptedCount++;
                    sample = ts.ToDroneSample();
                    return true;
                }

                // ZADATAK 5: Nevalidan red - prijavljujemo u izdvojeni rejects log
                //            Zarezima se zamenjuju tackama-zarezima da ne pokvarimo CSV format
                string safeError = error.Replace(",", ";");
                string safeLine  = line.Length > 200
                    ? line.Substring(0, 200) + "..."
                    : line;
                safeLine = safeLine.Replace(",", ";");

                rejectWriter.WriteLine($"{safeError},{safeLine}");
                RejectedCount++;
            }
        }

        // =====================================================================
        // ZADATAK 4: Dispose pattern - ispravno zatvaranje resursa
        //
        //   Redosled zatvaranja je obrnut od redosleda otvaranja:
        //   Poslednji otvoren -> prvi zatvoren
        //   Writer pre Stream (Writer moze imati buffer koji treba flush-ovati)
        // =====================================================================
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
                    // Flush i zatvaranje rejects writera
                    try { rejectWriter?.Flush();   } catch { }
                    try { rejectWriter?.Dispose(); } catch { }
                    try { rejectStream?.Dispose(); } catch { }

                    // Zatvaranje CSV readera
                    try { csvReader?.Dispose(); } catch { }
                    try { csvStream?.Dispose(); } catch { }
                }

                disposed = true;
            }
        }

        // ZADATAK 4: Finalizer - sigurnosna mreza
        // Poziva se od strane GC ako Dispose nije eksplicitno pozvan
        // (npr. pri naglom prekidu veze usred prenosa)
        ~DroneCsvSampleReader()
        {
            Dispose(false);
        }
    }
}
