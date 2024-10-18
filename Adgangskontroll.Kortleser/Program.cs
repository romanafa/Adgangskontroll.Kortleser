using System.IO.Ports;

namespace Adgangskontroll.Kortleser
{
    internal class Program
    {
        static string? tekst = "";
        static int tid = 0;

        static void Main(string[] args)
        {
            SerialPort sp = new SerialPort("COM3", 9600);

            /* //Kommando som sjekker om flere instanser kjøres? Som vi trenger til kortleser nummer
            
            //var exists = System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)).Count() > 1;
            int teller = 0;
            for (int i = 0; i < 1000; i++) //1000, må definere antall kortlesere på forhånd
            {
                var exists = System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)).Count() > i;
                if (exists) teller++;
            }
            Console.WriteLine(teller);
            Console.ReadKey();

            Console.Title = teller; 
            */

            Thread tid = new Thread(DørTid); 
            tid.Start();

            int teller = 0;
            int antallkortleser = 10;
            for (int i = 0; i < antallkortleser; i++)
            {
                var exists = System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly()!.Location)).Count() > i;
                if (exists) teller++;
            }
            Console.WriteLine(teller);
            Console.Title = teller.ToString();

            //Console.ReadKey();

            string data = "";
            string enMelding = "";
            
            Thread t = new Thread(LestMelding);
            t.Start();

            try
            {
                sp.Open();
            }
            catch (Exception u)
            {
                Console.WriteLine("Feil: " + u.Message);
            }

            if (sp.IsOpen)
            {

                SendEnMelding("$S005", sp);
                SendEnMelding("$O90", sp);
                SendEnMelding("$O51", sp);

                while (true)
                {
                    tekst = "";

                    data = data + MottaData(sp);


                    if (EnHelMeldingMotatt(data))
                    {
                        // Ta ut meldingen (bevar eventuell rest)
                        enMelding = HentUtEnMelding(ref data);

                        // Skriv ut meldingen
                        Console.WriteLine(enMelding);

                        // Skriv ut kun temperatur 
                        VisTemperatur(enMelding);

                        DørPosisjon(enMelding);

                        bool hai = DørAlarm(enMelding);
                    }

                    SendEnMelding(tekst, sp);             
                }              
            }
        } // av Main

        //Utgang 5 (Dør låst)(av/på):
        //$A001B20241014C104726D00000000E00000100F0500G0500H0500I020J020#
        //Temperatur: 0,0

        static void DørTid()
        {
            //Utgang 6 (Dør åpen) (av/på):
            //$A001B20241014C104726D00000000E00000010F0500G0500H0500I020J020#
            //Temperatur: 0,0

            while (true)
            {
                Thread.Sleep(1000);
                tid++;
            }
        }

        static void DørPosisjon(string enMelding)
        {
            bool svar = false;

            int indeksDørPosisjon = enMelding.IndexOf('E');

            int råDørÅpen = Convert.ToInt32(enMelding.Substring(indeksDørPosisjon + 7, 1));
            int råDørLukket = Convert.ToInt32(enMelding.Substring(indeksDørPosisjon + 6, 1));

            if ((råDørÅpen == 1) && (råDørLukket == 0))
            {
                Console.WriteLine("Dør åpen");
            }

            if ((råDørLukket == 1) && (råDørÅpen == 0))
            {
                tid = 0;
                Console.WriteLine("Dør lukket");
            }

            if (((råDørÅpen == 1) && (råDørLukket == 1)) || ((råDørÅpen == 0) && (råDørLukket == 0)))
            {
                // Bør trigge alarm (?)
                Console.WriteLine("Ugyldig dør posisjon");
            }
        }


        // Erik må fikse kommunikasjon til sentral
        static bool DørAlarm(string enMelding)
        {
            //Utgang 7 (Indikere alarm) (av/på):
            //$A001B20241014C104726D00000000E00000001F0500G0500H0500I020J020#

            //Potm 1 (verdi over 500 skal representere dør brutt opp):
            //$A001B20241014C104726D00000000E00000000F0500G0736H0500I020J020#

            bool svar = false;

            int indeksDørAlarm = enMelding.IndexOf('E');
            int indeksPotm = enMelding.IndexOf('G');

            int råDørAlarm = Convert.ToInt32(enMelding.Substring(indeksDørAlarm + 8, 1));
            int råPotm = Convert.ToInt32(enMelding.Substring(indeksPotm + 1, 4));

            if ((råDørAlarm == 1) || (råPotm >= 500) || (tid > 10))
            {
                svar = true;
                Console.WriteLine("Alarm: På");
            }

            return svar;
        }

        static void Avgangsforepørsel(int PINkode, int kortID)
        {
            //Erik skriver kode
        }

        static void LestMelding()
        {
            while (true)
            {
                tekst = Console.ReadLine();
            }
            
        }

        static void VisTemperatur(string enMelding)
        {
            int indeksTempStart = enMelding.IndexOf('F');

            int råTempData = Convert.ToInt32(enMelding.Substring(indeksTempStart + 1, 4));

            // Om temperaturområdet er mellom [-50, 52.3]
            double temperatur = (råTempData / 10.0) - 50;

            Console.WriteLine("Temperatur: {0:f1}", temperatur);

        }

        static string HentUtEnMelding(ref string data)
        {
            string svar = "";

            int indeksStart = data.IndexOf('$');
            int indeksSlutt = data.IndexOf('#');

            if (indeksStart > 0) data = data.Substring(indeksStart);

            svar = data.Substring(0, (indeksSlutt - indeksStart) + 1);

            data = data.Substring((indeksSlutt - indeksStart) + 1);

            return svar;
        }

        static string MottaData(SerialPort sp)
        {
            string svar = "";
            try
            {
                svar = sp.ReadExisting();
            }
            catch (Exception u)
            {
                Console.WriteLine("Feil: " + u.Message);
            }
            return svar;
        }

        static bool EnHelMeldingMotatt(string data)
        {
            bool svar = false;

            int indeksStart = data.IndexOf('$');
            int indeksSlutt = data.IndexOf('#');

            if (indeksStart != -1 && indeksSlutt != -1)
            {
                if (indeksStart < indeksSlutt) svar = true;
            }

            return svar;
        }

        static void SendEnMelding(string enMelding, SerialPort sp)
        {
            try
            {
                sp.Write(enMelding);
            }
            catch (Exception u)
            {
                Console.WriteLine("Feil: " + u.Message);
            }
        }
    }
}
