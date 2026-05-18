using System;
using System.ServiceModel;

namespace Server
{
    // =========================================================================
    // ZADATAK 2: Hostovanje WCF servisa
    //   - ServiceHost ucitava konfiguraciju iz App.config
    //   - netTcpBinding, adresa i timeout konfigurisani u App.config
    // =========================================================================
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("   Drone Telemetrija - WCF Server");
            Console.WriteLine("========================================\n");

            try
            {
                using (var host = new ServiceHost(typeof(DroneService)))
                {
                    host.Open();

                    Console.WriteLine($"Endpoint: {host.BaseAddresses[0]}Drone");
                    Console.WriteLine("Status:   Cekam klijentske konekcije...\n");
                    Console.WriteLine("(Pritisnite Enter za zaustavljanje)\n");

                    Console.ReadLine();

                    host.Close();
                    Console.WriteLine("Servis zaustavljen.");
                }
            }
            catch (AddressAlreadyInUseException)
            {
                Console.WriteLine("GRESKA: Port 4100 je vec zauzet.");
                Console.WriteLine("Proverite da li druga instanca servisa vec radi.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GRESKA: {ex.Message}");
            }

            Console.WriteLine("\nPritisnite Enter za izlaz...");
            Console.ReadLine();
        }
    }
}
