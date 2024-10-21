using System.IO.Ports;

namespace Adgangskontroll.Kortleser
{
    internal class Program
    {
        static string? innlest_tekst = "";
        static string? enMelding = "";
        static int kortPinFraSentral; //Erik fikse innhentingen
        static int kortIDFraSentral; //Erik fikse innhentingen
        static bool dørlåst;     //oppgave 5
        static bool dørPosisjon; //oppgave 5

        static int tid = 0;

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            kortPinFraSentral = 1023; //Erik fikse innhentingen
            kortIDFraSentral = 1023; //Erik fikse innhentingen
            SerialPort sp = new SerialPort("COM5", 9600);

            //oppgave 1 registreres et kortlesernummer når prosessen 
            //Console.WriteLine("Skriv inn kortlesernummer i format '####' hvor '#' er et siffer.");
            //string? kortlesernummer = Console.ReadLine(); //Erik skal bruke denne for å gi til database.
            //Console.Title = kortlesernummer!;


            //oppgave 5 aktivere/stoppe «dør åpen for lenge» alarmer når det oppstår behov for det
            Thread tid = new Thread(DørTid);
            tid.Start();


            //Kode som definerer og teller antall instanser av programmet. Skal ikke taes i bruk.
            //int teller = 0;
            //int antallkortleser = 10;
            //for (int i = 0; i < antallkortleser; i++)
            //{
            //    var exists = System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly()!.Location)).Count() > i;
            //    if (exists) teller++;
            //}
            //Console.WriteLine(teller);
            //Console.Title = teller.ToString();
            //Console.ReadKey();

            string data = "";


            int kortid;       //oppgave 4
            int kortPin;       //oppgave 4

            //oppgave 3 kunne sende meldinger
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
                    innlest_tekst = "";


                    data = data + MottaData(sp);

                    //oppgave 2 kunne motta meldingene fra kortet
                    if (EnHelMeldingMotatt(data))
                    {
                        enMelding = HentUtEnMelding(ref data); // Ta ut meldingen (bevar eventuell rest)
                        Console.WriteLine(enMelding); // Skriv ut meldingen

                        kortid = KortID(enMelding); //Console.WriteLine($"{kortpin:0000}"); //må vi legge til 000 hvis svar under 1000? Nei.
                        kortPin = KortPin(enMelding);



                        dørPosisjon = DørPosisjon(enMelding);
                        dørlåst = Dørlåst(enMelding);

                        //kladd, trengs sikkert ikke, kanskje ugyldig dør posisjon.
                        //Håndtere hva skjer dersom dår er åpen og låst?
                        //if ((råDørÅpen == 0) && (råDørLåst == 1))
                        //{
                        //    tid = 0;
                        //    Console.WriteLine("Dør låst");
                        //}

                        //if (((råDørÅpen == 1) && (råDørLåst == 1)) || ((råDørÅpen == 0) && (råDørLåst == 0)))
                        //{
                        //    // Bør trigge alarm (?)
                        //    Console.WriteLine("Ugyldig dør posisjon");
                        //}

                        //oppgave 7 hvis døren lukkes, låses døren
                        if (!dørPosisjon && !dørlåst)
                        {
                            dørlåst = true;
                            enMelding = enMelding.Insert(enMelding.IndexOf('E') + 6, "1");
                            enMelding = enMelding.Remove(enMelding.IndexOf('E') + 7, 1);
                            Console.WriteLine("Døren ble lukket og låst "); //fiks, trenger ikke denne linjen, men har den for å teste for nå.
                            SendEnMelding("$O51", sp);
                        }

                        if (Adgangsforepørsel(kortPin, kortid))
                        {
                            dørlåst = false;
                            enMelding = enMelding.Insert(enMelding.IndexOf('E') + 6, "0");
                            enMelding = enMelding.Remove(enMelding.IndexOf('E') + 7, 1);
                            Console.WriteLine("Godkjent! Døren låses opp");
                            SendEnMelding("$O50", sp);
                        }

                        bool dørAlarm = DørAlarm(enMelding);
                        if (dørAlarm)
                        {
                            SendEnMelding("$O71", sp);
                        }
                        if(dørAlarm&&!dørPosisjon&& 500>Convert.ToInt32(enMelding.Substring(enMelding.IndexOf('G') + 1, 4)))
                        {
                            SendEnMelding("$O70", sp);
                            Console.WriteLine("Dør tidsalarm ble skrudd av");
                        }
                        //if(dørAlarm&&!dørPosisjon)
                        //{
                        //    SendEnMelding("$O70", sp);
                        //}

                    }

                    //oppgave 3 kunne sende meldinger til kortet
                    SendEnMelding(innlest_tekst, sp);

                    //oppgave 4 behandle adgangsforespørsler(motta kortID +PINkode fra bruker)
                    //Når det skal testes i mot sentral og skrives i Kortleser programmet så blir kommandoen
                    //Simsim må ikke sende meldinger for at det skal fungere.
                    //"$F1000" for ID 1000 og "$F3251" for ID 3251. Kun tall over 1023 kan endres ved å skrive i console, ellers må det endres i Simsim
                    EndreKortID(innlest_tekst);
                    //"$H1000" for pin 1000 og "$H3251" for pin 3251.                    
                    EndreKortPin(innlest_tekst);

                }
            }
        } // av Main

        //Vi må sette det opp slik at pinkode og kortpin blir lest fra SIMSIM
        //oppgave 4 behandle adgangsforespørsler(motta kortID +PINkode fra bruker) for å sende disse til(og motta svar fra) prosessen SENTRAL

        static int KortID(string EnMelding)
        {
            int indeksIDStart = EnMelding.IndexOf('F');
            int kortid = Convert.ToInt32(EnMelding.Substring(indeksIDStart + 1, 4));
            return kortid;
        }
        static void EndreKortID(string tekst)
        {
            //"$F3251"
            //$A001B20241014C104726D00000000E00000010F0500G0500H0500I020J020#
            //5000;
            //kortIDFraSentral = 4000;
            if (tekst.Length > 5 && tekst.Contains('F'))
            {
                int indeksIDStart = tekst.IndexOf('F');
                int kortid = Convert.ToInt32(tekst.Substring(indeksIDStart + 1, 4));

                if (tekst.Substring(0, 2) == "$F")
                {
                    if (kortid > 1000)
                    {
                        Console.WriteLine(enMelding);
                        enMelding = enMelding!.Insert(enMelding.IndexOf('F') + 1, kortid.ToString());
                        enMelding = enMelding.Remove(enMelding.IndexOf('F') + 5, 4);
                        Console.WriteLine(enMelding);
                    }
                }
            }

        }
        //oppgave 4
        static int KortPin(string EnMelding)
        {
            int indeksPinStart = EnMelding.IndexOf('H');
            int kortPin = Convert.ToInt32(EnMelding.Substring(indeksPinStart + 1, 4));
            return kortPin;

        }

        static void EndreKortPin(string tekst)
        {
            if (tekst.Length > 5 && tekst.Contains('H'))
            {
                int indeksIDStart = tekst.IndexOf('H');
                int kortpin = Convert.ToInt32(tekst.Substring(indeksIDStart + 1, 4));

                if (tekst.Substring(0, 2) == "$H")
                {
                    if (kortpin > 1000)
                    {
                        Console.WriteLine(enMelding);
                        enMelding = enMelding!.Insert(enMelding.IndexOf('H') + 1, kortpin.ToString());
                        enMelding = enMelding.Remove(enMelding.IndexOf('H') + 5, 4);
                        Console.WriteLine(enMelding);
                    }
                }
            }

        }



        static bool Adgangsforepørsel(int kortPin, int kortID)
        {
            bool adgang = false;
            if (kortID == kortIDFraSentral && kortPin == kortPinFraSentral)
            {
                adgang = true;

            }
            return adgang;
            //Erik skriver kode
        }



        //oppgave 5 aktivere/stoppe «dør åpen for lenge» alarmer når det oppstår behov for det
        //(blant annet skal prosessen passe på at døren ikke står åpen for lenge)
        static void DørTid()
        {
            while (true)
            {
                Thread.Sleep(1000);
                tid++;
            }
        }

        static bool DørPosisjon(string EnMelding)
        {
            //Utgang 6 (Dør åpen) (av/på):
            //$A001B20241014C104726D00000000E00000010F0500G0500H0500I020J020#
            //Temperatur: 0,0
            bool åpen = false;

            int indeksDørPosisjon = EnMelding.IndexOf('E');
            int råDørÅpen = Convert.ToInt32(EnMelding.Substring(indeksDørPosisjon + 7, 1));


            if (råDørÅpen == 1)
            {
                Console.WriteLine("Dør åpen"); //fiks, trenger ikke denne linjen, men har den for å teste for nå.
                åpen = true;
            }

            if (åpen == false)
            {
                tid = 0;
            }

            return åpen;
        }

        static bool Dørlåst(string EnMelding)
        {
            //Utgang 5 (Dør låst)(av/på):
            //$A001B20241014C104726D00000000E00000100F0500G0500H0500I020J020#
            //Temperatur: 0,0
            bool låst = false;

            int indeksDørPosisjon = EnMelding.IndexOf('E');
            int råDørLåst = Convert.ToInt32(EnMelding.Substring(indeksDørPosisjon + 6, 1));

            if (råDørLåst == 1)
            {
                låst = true;
                Console.WriteLine("Dør låst"); //fiks, trenger ikke denne linjen, men har den for å teste for nå.
            }

            return låst;
        }

        //oppgave 5+6
        // Erik må fikse kommunikasjon til sentral
        static bool DørAlarm(string EnMelding)
        {
            //Utgang 7 (Indikere alarm) (av/på):
            //$A001B20241014C104726D00000000E00000001F0500G0500H0500I020J020#

            //Potm 1 (verdi over 500 skal representere dør brutt opp):
            //$A001B20241014C104726D00000000E00000000F0500G0736H0500I020J020#

            bool alarm = false;

            int indeksDørAlarm = EnMelding.IndexOf('E');
            int indeksPotm = EnMelding.IndexOf('G');

            int råDørAlarm = Convert.ToInt32(EnMelding.Substring(indeksDørAlarm + 8, 1));
            int råPotm = Convert.ToInt32(EnMelding.Substring(indeksPotm + 1, 4));

            if ((råDørAlarm == 1) || (råPotm >= 500)|| (tid > 10))
            {
                alarm = true;
                Console.WriteLine("Alarm: På");
            }


            return alarm;
        }



        static void LestMelding()
        {
            while (true)
            {
                innlest_tekst = Console.ReadLine();
            }

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

        static void SendEnMelding(string EnMelding, SerialPort sp)
        {
            try
            {
                sp.Write(EnMelding);
            }
            catch (Exception u)
            {
                Console.WriteLine("Feil: " + u.Message);
            }
        }





    }
}
