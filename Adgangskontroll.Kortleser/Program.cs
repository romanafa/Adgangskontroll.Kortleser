using System.IO.Ports;

namespace Adgangskontroll.Kortleser
{
    internal class Program
    {
        static string? enMelding = "";      //Oppgave 2     //linje 25 //må ha static hvis EndreKortIDPin metoden brukes
        static string? innlest_tekst = "";  //Oppgave 3        
        static int kortPinFraSentral;       //Oppgave 4
        static int kortIDFraSentral;        //Oppgave 4
        static int kortid;                  //Oppgave 4
        static int kortPin;                 //Oppgave 4
        static bool dørlåst;                //Oppgave 5
        static bool dørPosisjon;            //Oppgave 5
        static bool dørAlarm;               //Oppgave 6
        //static string data = "";                          //linje 24

        static int tid = 0;

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            SerialPort sp = new SerialPort("COM5", 9600);
            string data = "";               //linje 16
            //string? enMelding = "";       //linje 7

            /*Erik fiks*/
            //Oppgave 1 registreres et kortlesernummer når prosessen 
            //Console.WriteLine("Skriv inn kortlesernummer i format '####' hvor '#' er et siffer.");
            //string? kortlesernummer = Console.ReadLine(); //Erik skal bruke denne for å gi til database.
            //Console.Title = kortlesernummer!;

            //Oppgave 5 aktivere/stoppe «dør åpen for lenge» alarmer når det oppstår behov for det
            Thread tid = new Thread(DørTid);
            tid.Start();

            /* Kode som definerer og teller antall instanser av programmet. Skal ikke taes i bruk.
            int teller = 0;
            int antallkortleser = 10;
            for (int i = 0; i < antallkortleser; i++)
            
            var exists = System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly()!.Location)).Count() > i;
            if (exists) teller++;
            
            Console.WriteLine(teller);
            Console.Title = teller.ToString();
            Console.ReadKey();
            */

            //Oppgave 3 kunne sende meldinger flere ganger
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

                SendEnMelding("$S005", sp); //Interval 5sek
                SendEnMelding("$O90", sp);  //Slå alle utganger av
                SendEnMelding("$O51", sp);  //Slå låst på

                while (true)
                {
                    innlest_tekst = "";
                    data = data + MottaData(sp);

                    //Oppgave 2 kunne motta meldingene fra kortet
                    if (EnHelMeldingMotatt(data))
                    {
                        enMelding = HentUtEnMelding(ref data); // Ta ut meldingen (bevar eventuell rest)
                        Console.WriteLine(enMelding); // Skriv ut meldingen

                        kortid = KortID(enMelding); //Console.WriteLine($"{kortid:0000}"); //må vi legge til 000 hvis svar under 1000? Nei.
                        kortPin = KortPin(enMelding);
                        dørPosisjon = DørPosisjon(enMelding); 
                        dørlåst = Dørlåst(enMelding);
                        dørAlarm = DørAlarm(enMelding);
                        //EndreKortIDPin(innlest_tekst); //Må finne en løsning. Alexander/Nathalie fiks.

                        if (dørPosisjon)
                            Console.WriteLine("Dør åpen"); //fiks, trenger ikke denne linjen, men har den for å teste for nå.

                        if (dørlåst)
                            Console.WriteLine("Dør låst"); //fiks, trenger ikke denne linjen, men har den for å teste for nå.

                        //Oppgave 4
                        if (Adgangsforepørsel(kortPin, kortid))
                        {
                            dørlåst = false;
                            enMelding = enMelding.Insert(enMelding.IndexOf('E') + 6, "0");
                            enMelding = enMelding.Remove(enMelding.IndexOf('E') + 7, 1);
                            Console.WriteLine("Godkjent! Døren låses opp");
                            SendEnMelding("$O50", sp);
                        }

                        //Oppgave 7 hvis døren lukkes, låses døren
                        if (!dørPosisjon && !dørlåst)
                        {
                            dørlåst = true;
                            enMelding = enMelding.Insert(enMelding.IndexOf('E') + 6, "1");
                            enMelding = enMelding.Remove(enMelding.IndexOf('E') + 7, 1);
                            Console.WriteLine("Døren ble lukket og låst ");
                            SendEnMelding("$O51", sp);
                        }

                        /*Erik fiks*/
                        //Oppgave 6
                        if (dørAlarm)
                        {
                            SendEnMelding("$O71", sp);
                            Console.WriteLine("Alarm: På");
                            /*Erik skrive kode for å sende det til sentral*/
                        }

                        /*Erik fiks*/
                        //Oppgave 7
                        if (dørAlarm && !dørPosisjon && 500 >= Convert.ToInt32(enMelding.Substring(enMelding.IndexOf('G') + 1, 4)))
                        {
                            SendEnMelding("$O70", sp);
                            /*Erik skrive kode for å sende det til sentral*/
                            Console.WriteLine("Dør tidsalarm ble skrudd av");
                        }
                    }

                    //Oppgave 3 kunne sende meldinger til kortet
                    SendEnMelding(innlest_tekst, sp);

                    //Oppgave 4 behandle adgangsforespørsler(motta kortID +PINkode fra bruker)
                    //Når det skal testes i mot sentral og skrives i Kortleser programmet så blir kommandoen
                    //Simsim må ikke sende meldinger for at det skal fungere.
                    //"$F1000" for ID 1000 og "$F3251" for ID 3251. Kun tall over 1023 kan endres ved å skrive i console, ellers må det endres i Simsim
                    //"$H1000" for pin 1000 og "$H3251" for pin 3251.
                    EndreKortIDPin(innlest_tekst); //Må finne en løsning. Alexander/Nathalie fiks.
                    EndreKortIDPin(innlest_tekst); //Må finne en løsning. Alexander/Nathalie fiks.




                }
            }
        } // av Main



        //Vi må sette det opp slik at pinkode og kortpin blir lest fra SIMSIM
        //Oppgave 4 behandle adgangsforespørsler(motta kortID +PINkode fra bruker) for å sende disse til(og motta svar fra) prosessen SENTRAL
        static int KortID(string EnMelding)
        {
            int indeksIDStart = EnMelding.IndexOf('F');
            int kortid = Convert.ToInt32(EnMelding.Substring(indeksIDStart + 1, 4));
            return kortid;
        }

        //Oppgave 4
        static int KortPin(string EnMelding)
        {
            int indeksPinStart = EnMelding.IndexOf('H');
            int kortPin = Convert.ToInt32(EnMelding.Substring(indeksPinStart + 1, 4));
            return kortPin;
        }

        //Oppgave 4
        static void EndreKortIDPin(string tekst)
        {
            if (tekst.Length > 5 && (tekst.Substring(0, 2) == "$F"))
            {
                int indeksIDStart = tekst.IndexOf('F');
                int kortid = Convert.ToInt32(tekst.Substring(indeksIDStart + 1, 4));
                if (kortid > 1000)
                {
                    Console.WriteLine(enMelding);
                    enMelding = enMelding!.Insert(enMelding.IndexOf('F') + 1, kortid.ToString());
                    enMelding = enMelding.Remove(enMelding.IndexOf('F') + 5, 4);
                    //data = data + enMelding;
                    Console.WriteLine(enMelding);
                }
            }

            if (tekst.Length > 5 && (tekst.Substring(0, 2) == "$H"))
            {
                int indeksIDStart = tekst.IndexOf('H');
                int kortpin = Convert.ToInt32(tekst.Substring(indeksIDStart + 1, 4));
                if (kortpin > 1000)
                {
                    Console.WriteLine(enMelding);
                    enMelding = enMelding!.Insert(enMelding.IndexOf('H') + 1, kortpin.ToString());
                    enMelding = enMelding.Remove(enMelding.IndexOf('H') + 5, 4);
                    Console.WriteLine(enMelding);
                }
            }
        }

        /*Erik fiks*/
        //Oppgave 4
        static bool Adgangsforepørsel(int kortPin, int kortID)
        {
            bool adgang = false;
            kortPinFraSentral = 1023; //Erik fikse innhentingen
            kortIDFraSentral = 1023; //Erik fikse innhentingen

            if (kortID == kortIDFraSentral && kortPin == kortPinFraSentral)
            {
                adgang = true;
            }
            return adgang;
        }



        //Oppgave 5
        static bool DørPosisjon(string EnMelding)
        {
            bool åpen = false;
            int indeksDørPosisjon = EnMelding.IndexOf('E');
            int råDørÅpen = Convert.ToInt32(EnMelding.Substring(indeksDørPosisjon + 7, 1)); //Utgang 6 (Dør åpen) (av/på): //$A001B20241014C104726D00000000E00000010F0500G0500H0500I020J020#

            if (råDørÅpen == 1)
            {
                åpen = true;
                //Console.WriteLine("Dør åpen"); //fiks, trenger ikke denne linjen, men har den for å teste for nå. er i main while loop if (dørPosisjon)
            }

            if (åpen == false)
            {
                tid = 0;
            }
            return åpen;
        }

        //Oppgave 5
        static bool Dørlåst(string EnMelding)
        {
            bool låst = false;
            int indeksDørPosisjon = EnMelding.IndexOf('E');
            int råDørLåst = Convert.ToInt32(EnMelding.Substring(indeksDørPosisjon + 6, 1)); //Utgang 5 (Dør låst)(av/på): //$A001B20241014C104726D00000000E00000100F0500G0500H0500I020J020#

            if (råDørLåst == 1)
            {
                låst = true;
                //Console.WriteLine("Dør låst"); //fiks, trenger ikke denne linjen, men har den for å teste for nå. er i main while loop if (dørlåst)
            }
            return låst;
        }



        /*Erik fiks*/
        //Oppgave 6
        static bool DørAlarm(string EnMelding)
        {
            bool alarm = false;
            int indeksDørAlarm = EnMelding.IndexOf('E');
            int indeksPotm = EnMelding.IndexOf('G');
            int råDørAlarm = Convert.ToInt32(EnMelding.Substring(indeksDørAlarm + 8, 1)); //Utgang 7 (Indikere alarm) (av/på): //$A001B20241014C104726D00000000E00000001F0500G0500H0500I020J020#
            int råPotm = Convert.ToInt32(EnMelding.Substring(indeksPotm + 1, 4)); //Potm 1 (verdi over 500 skal representere dør brutt opp): //$A001B20241014C104726D00000000E00000000F0500G0736H0500I020J020#

            if ((råDørAlarm == 1) || (råPotm > 500) || (tid > 10))
            {
                alarm = true;
            }
            // Erik må fikse kommunikasjon til sentral
            return alarm;
        }





        //Koden under her skal ikke endres, men må kommenteres der nødvendig





        //Oppgave 2
        static string HentUtEnMelding(ref string data)
        {
            string svar = "";
            int indeksStart = data.IndexOf('$');
            int indeksSlutt = data.IndexOf('#');

            if (indeksStart > 0)
                data = data.Substring(indeksStart);

            svar = data.Substring(0, (indeksSlutt - indeksStart) + 1);
            data = data.Substring((indeksSlutt - indeksStart) + 1);
            return svar;
        }

        //Oppgave 2
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

        //Oppgave 2
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



        //Oppgave 3
        static void LestMelding()
        {
            while (true)
            {
                innlest_tekst = Console.ReadLine();
            }
        }

        //Oppgave 3
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



        //Oppgave 5 aktivere/stoppe «dør åpen for lenge» alarmer når det oppstår behov for det
        //(blant annet skal prosessen passe på at døren ikke står åpen for lenge)
        static void DørTid()
        {
            while (true)
            {
                Thread.Sleep(1000);
                tid++;
            }
        }
    }
}


//kladd, trengs sikkert ikke, kanskje ugyldig dør posisjon.
//Håndtere hva skjer dersom dør er åpen og låst?
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