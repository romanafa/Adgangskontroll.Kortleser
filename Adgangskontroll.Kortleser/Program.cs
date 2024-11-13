using Microsoft.VisualBasic;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;
namespace Adgangskontroll.Kortleser
{
    internal class Program
    {
        static string? innlest_tekst = "";
        static int kortPINFraSentral;
        static int kortIDFraSentral;
        static int kortID;
        static int kortPIN;
        static int tidulåst = 0;
        static bool dørLåst;
        static bool dørPosisjon;
        static bool dørAlarm;
        static bool dørBruttopp;
        static int tid = 0;
        static int døråpenalarmtid = 10;            //Hvor lenge døren er åpen før alarmen går. I sekunder.
        static int dørlåstopptid = 5;               //Hvor lenge døren er ulåst ved bruk av adgangskort. I sekunder.                
        static bool Avbryt = false;                 // Brukes for sikkerhetsmekanisme for lukking av kortleser
        static bool kommunikasjonMedSentral = true; // Sett denne til false eller true basert på din situasjon


        static void Main(string[] args)
        {
            //Mulig feil: hvis portnummer ikke stemmer eller den er opptatt så kjøres ikke programmet.
            int komnummer = 1;              //Bestemmer hvilket COM-nummer skal tas i bruk
            SerialPort sp = new SerialPort("COM"+komnummer, 9600);
            string data = "";
            string? enMelding = "";
            
            //Registrer et kortlesernummer når prosessen og sender det til sentralen.
            Console.WriteLine("Skriv inn kortlesernummer i format '####' hvor '#' er et siffer.");
            string? kortlesernummer = Console.ReadLine();
            Console.Title = kortlesernummer!;
            //Mangler å sende kortlesernummer til sentral

            //Starter en tidsteller som kjøres parallelt med resten av koden.
            //Som brukes når døren er åpen for lenge og tiden døren er ulåst.
            Thread tid = new Thread(DørTid);
            tid.Start();

            Console.WriteLine("Når bruker skal angi PIN-kode og kortID må det oppgies på formatet: $F3251$H1826\n" +
                "Hvor $F3251 er kort ID-en = 3251 og $H1826 er kort PIN-koden = 1826");

            // Etablerer IP-kommunikasjon
            // Socket klientSokkel;

            //Starter tråd for å lese meldinger bruker skriver i konsollvinduet.
            Thread lesing = new Thread(LestMelding);
            lesing.Start();

            //Sjekker om valgt seriel port åpen og gir feilmelding dersom den ikke er det.
            //Mulig feil: hvis kabelen kobles fra og kobles til igjen så vil den ikke fortsette programmet, den må startes på nytt.
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
                //Setter default verdier for simsim programmet.
                SendEnMelding("$S005", sp); //Interval 5sek
                SendEnMelding("$O90", sp);  //Slå alle utganger av
                SendEnMelding("$O51", sp);  //Slå dørlåst på

                while (true)
                {
                    innlest_tekst = "";             //Nullstiller innlest tekst.
                    data = data + MottaData(sp);    //Setter mottatt informasjon fra kort til data.

                    //Sjekker om kort har sendt data og utfører forskjellige oppgaver.
                    if (EnHelMeldingMotatt(data))
                    {
                        enMelding = HentUtEnMelding(ref data);  //Ta ut meldingen (bevar eventuell rest)
                        Console.WriteLine(enMelding);           //Skriver ut meldingen
                        //Henter informasjon fra kortet og deklarer variablene.                        
                        dørLåst = Dørlåst(enMelding);
                        dørPosisjon = DørPosisjon(enMelding);
                        dørBruttopp = DørBruttopp(enMelding);
                        dørAlarm = DørAlarm(enMelding);

                        //De to if else-ene trengs ikke, men er for visualisering av kode.
                        if (dørLåst)
                            Console.WriteLine("Dør låst");
                        else
                            Console.WriteLine("Dør ulåst");

                        if (dørPosisjon)
                            Console.WriteLine("Dør åpen");
                        else
                            Console.WriteLine("Dør lukket");
                        
                        //Hvis døren lukkes, låses døren
                        if (!dørPosisjon && !dørLåst && tidulåst <= 0)
                        {
                            dørLåst = true;
                            enMelding = enMelding.Insert(enMelding.IndexOf('E') + 6, "1");
                            enMelding = enMelding.Remove(enMelding.IndexOf('E') + 7, 1);
                            Console.WriteLine("Dør lukket og låses");       //Trengs ikke, men er for visualisering av kode.
                            SendEnMelding("$O51", sp);
                        }
                        
                        //Hvis alarmen er på, men døren er lukket og ikke brutt opp så skrues alarmen av. 
                        if (dørAlarm && !dørPosisjon && !dørBruttopp)
                        {
                            dørAlarm = false;
                            SendEnMelding("$O70", sp);
                            /*Skrive kode for å sende det til sentral*/
                            Console.WriteLine("Alarm: Av");
                        }

                        //Sjekker om døren er brutt opp.
                        if (dørBruttopp)
                        {
                            Console.WriteLine("Dør brutt opp");
                            /*Skrive kode for å sende det til sentral*/
                        }
                        
                        //Skrur på alarmen hvis døren er brutt opp eller vært åpen for lenge.
                        if (dørAlarm)
                        {
                            SendEnMelding("$O71", sp);
                            Console.WriteLine("Alarm: På");
                            /*Skrive kode for å sende det til sentral*/
                        }
                    }

                    //Sender meldinger til kortet
                    SendEnMelding(innlest_tekst, sp);

                    //Leser av oppgitt kort id og PIN-kode fra bruker.
                    //Når bruker skal angi PIN-kode og kortID må det oppgies på formatet: $F3251$H1826
                    //Hvor "$F3251" er kort ID-en = 3251 og $H1826 er kort PIN-koden = 1826.
                    BrukerSkriverInnKortIDPin(innlest_tekst);

                    //Sjekker PIN-kode og kortID, låser opp døren hvis det er riktig og lar den være ulåst i "dørlåstopptid" sekunder.
                    if (Adgangsforepørsel(kortPIN, kortID))
                    {
                        dørLåst = false;
                        tidulåst = dørlåstopptid;
                        kortPIN = 10000;                                //"Nullstiller" PIN-koden.
                        kortID = 10000;                                 //"Nullstiller" kort ID-en.
                        Console.WriteLine("Godkjent! Døren låses opp"); //Trengs ikke, men er for visualisering av kode.
                        SendEnMelding("$O50", sp);                        
                    }

                }
            }
        } // av Main


        //Behandler adgangsforespørsler(motta kortid +PINkode fra bruker)
        static void BrukerSkriverInnKortIDPin(string tekst)
        {
            if (tekst.Length > 11 && (tekst.Substring(0, 2) == "$F") && (tekst.Substring(6, 2) == "$H"))
            {
                int indeksIDStart = tekst.IndexOf('F');
                int indeksPINStart = tekst.IndexOf('H');
                kortID = Convert.ToInt32(tekst.Substring(indeksIDStart + 1, 4));
                kortPIN = Convert.ToInt32(tekst.Substring(indeksPINStart + 1, 4));
            }
        }

        //Behandler adgangsforespørsler fra kortet, sjekker om de samsvarer med det som er hentet fra sentral.
        static bool Adgangsforepørsel(int kortpin, int kortid)
        {
            bool adgang = false;
            //sendkortid(kortid);
            //Må også sjekke hva kortid sendt til sentral ikke er i DB            
            //kortPINFraSentral = mottarkortpinfrasentral();   //Fikse innhentingen og sending til sentral. Vi har bare satt det til 1023 for testing.

            kortPINFraSentral = 1023;   //Fikse innhentingen
            kortIDFraSentral = 1023;    //Fikse innhentingen
            if (kortid == kortIDFraSentral && kortpin == kortPINFraSentral)
            {
                adgang = true;
            }
            return adgang;
        }

        //Sjekker om døren er åpen eller lukket fra innhentet data og hvis den er lukket så nullstilles tiden.
        static bool DørPosisjon(string EnMelding)
        {
            bool åpen = false;
            int indeksDørPosisjon = EnMelding.IndexOf('E'); //Utgang 6 (Dør åpen) (av/på): //$A001B20241014C104726D00000000E00000010F0500G0500H0500I020J020#
            int råDørÅpen = Convert.ToInt32(EnMelding.Substring(indeksDørPosisjon + 7, 1));

            if (råDørÅpen == 1)
            {
                åpen = true;
            }

            if (åpen == false)
            {
                tid = 0;
            }
            return åpen;
        }

        //Sjekker om døren er låst eller ulåst fra innhentet data.
        static bool Dørlåst(string EnMelding)
        {
            bool låst = false;
            int indeksDørPosisjon = EnMelding.IndexOf('E'); //Utgang 5 (Dør låst)(av/på): //$A001B20241014C104726D00000000E00000100F0500G0500H0500I020J020#
            int råDørLåst = Convert.ToInt32(EnMelding.Substring(indeksDørPosisjon + 6, 1));

            if (råDørLåst == 1)
            {
                låst = true;
            }
            return låst;
        }

        //Sjekker om døren har blitt brutt opp eller ikke fra innhentet data.
        static bool DørBruttopp(string EnMelding)
        {
            bool brudd = false;
            int indeksPotm = EnMelding.IndexOf('G'); //Potm 1 (verdi over 500 skal representere dør brutt opp): //$A001B20241014C104726D00000000E00000000F0500G0736H0500I020J020#
            int råPotm = Convert.ToInt32(EnMelding.Substring(indeksPotm + 1, 4));

            if (råPotm > 500)
            {
                brudd = true;
            }
            //Fikse sending til sentral.
            return brudd;
        }

        //Sjekker om dør alarmen er på fra innhentet data og skrur alarmen på hvis døren har blitt brutt opp eller har vært åpen for lenge. 
        static bool DørAlarm(string EnMelding)
        {
            bool alarm = false;
            int indeksDørAlarm = EnMelding.IndexOf('E'); //Utgang 7 (Indikere alarm) (av/på): //$A001B20241014C104726D00000000E00000001F0500G0500H0500I020J020#
            int råDørAlarm = Convert.ToInt32(EnMelding.Substring(indeksDørAlarm + 8, 1));

            if ((råDørAlarm == 1) || (dørBruttopp) || (tid > døråpenalarmtid))
            {
                alarm = true;
            }
            // Må fikse kommunikasjon til sentral
            return alarm;
        }


        //Definerer når informasjonen mottatt starter og slutter og
        //begrenser det til en melding om gangen slik at det ikke blir overlapp deretter sletter den ene meldingen.
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

        //Leser av informasjon mottatt fra kortet/seriell.
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

        //Sjekker om informasjon mottatt fra kortet/seriell er en helt melding.
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

        //Leser meldinger bruker skriver i konsollvinduet.
        static void LestMelding()
        {
            while (true)
            {
                innlest_tekst = Console.ReadLine();
            }
        }

        //Sender innlest melding fra bruker til kortet/seriell.
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

        //Øker og minker tiden (for åpnet dør alarm og være ulåst) som har gått siden programmet startet.
        static void DørTid()
        {
            while (true)
            {
                Thread.Sleep(1000);
                tid++;
                tidulåst--;
            }
        }








        //Ikke fungerende slik ønsket kode herfra og til bunnen.


        // Liste for å lagre koden som tastes inn
        List<int> kodeinput = new List<int>();

        // Datatypene for kortleser
        static string dataTilSentral;
        static string dataFraSentral;


        // Metode for å lese inn kode
        public void Kode(int inn)
        {
            kodeinput.Add(inn);
            try
            {
                if (kodeinput.Count == 4 && kommunikasjonMedSentral == true)
                {
                    //kortID = TB_KortInput.Text;
                    //TB_KortInput.Clear();
                    //TB_KortInput.Visible = false;
                    //pin = kodeinput[0].ToString() + kodeinput[1].ToString() + kodeinput[2].ToString() + kodeinput[3].ToString();
                    kodeinput.Clear();

                    // Sender informasjon til sentral for å autentisere brukeren
                    dataTilSentral = $"K:{kortID}"; //P:{pin} L:{kortleserID}";

                    if (kommunikasjonMedSentral == true)
                    {
                        //if (!BW_SendKvittering.IsBusy)     // BackgroundWorker skal i prinsipp aldri være opptatt, men den kan, derfor må vi sjekke for dette
                        //{
                        //    BW_SendKvittering.RunWorkerAsync();
                        //}
                        //else
                        //{
                        //    Console.WriteLine("feil");
                        //}
                        ///*MessageBox.Show("Lokal info i kortleser:\n" + dataTilSentral);*/     //debug: sjekker at informasjon lagres korrekt
                    }

                    //pin = "";
                }
            }
            catch (Exception)
            {
                throw;
            }
        }


        private void TCP()
        {
            Socket klientSokkel = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // oppkobling mot sentral
            void Sokkel_startup(object sender, EventArgs e)
            {
                // Oppretter tilkobling mot sentral ved bruk av TCP/IP

                // klientSokkel = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint serverEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9050);
                // må sette opp noe for å lese kortleser ID fra simsim


                // prøver å "få tak i" tilkoblingen fra sentral
                try
                {
                    klientSokkel.Connect(serverEP);
                    kommunikasjonMedSentral = true;
                    try
                    {
                        // Kortleser vil spørre etter sin ID fra sentralen
                        dataTilSentral = "RequestID";
                        // BW_SendKvittering.RunWorkerAsync();
                    }
                    catch (Exception)
                    {
                        throw;
                    }

                }
                catch (SocketException)
                {
                    Console.WriteLine("Fikk ikke kontakt med sentral!");
                    kommunikasjonMedSentral = false;
                }
            }

            void Avslutt()
            {



                if (!Avbryt)
                {
                    Console.WriteLine($"Er du sikker på at du vil fjerne denne kortleseren? (Ja/Nei)");
                    var input = Console.ReadLine();

                    if (!string.IsNullOrEmpty(input) && input.Equals("Nei", StringComparison.OrdinalIgnoreCase))
                    {
                        // Avbryter lukking og frakobling
                        Console.WriteLine("Avslutting avbrutt.");
                        return;
                    }
                    else if (kommunikasjonMedSentral)
                    {
                        // Lukker tilkoblingen
                        klientSokkel.Shutdown(SocketShutdown.Both); //Klient sokkel må kobles opp mot sender.cs
                        klientSokkel.Close();
                        Console.WriteLine("Tilkoblingen ble lukket.");
                    }
                }

                Console.WriteLine("Programmet avsluttes...");
            }
            // Generelle funksjoner for å motta og sende data til sentral
            static string MottaData(Socket s, out bool gjennomført)
            {
                string svar = "";
                try
                {
                    byte[] dataSomBytes = new byte[1024];
                    int recv = s.Receive(dataSomBytes);
                    if (recv > 0)
                    {
                        svar = Encoding.ASCII.GetString(dataSomBytes, 0, recv);
                        gjennomført = true;
                    }
                    else
                        gjennomført = false;
                }
                catch (Exception)
                {
                    throw;
                }
                return svar;
            }
            static void SendData(Socket s, string data, out bool gjennomført)
            {
                try
                {
                    byte[] dataSomBytes = Encoding.ASCII.GetBytes(data);
                    s.Send(dataSomBytes, dataSomBytes.Length, SocketFlags.None);
                    gjennomført = true;
                }
                catch (Exception)
                {
                    gjennomført = false;
                }
            }
        }
    }
}


/* Kode som definerer og teller antall instanser av programmet. Skal ikke taes i bruk siden vi definerer kortlesernummer i konsollvinduet.
            int teller = 0;
            int antallkortleser = 10;
            for (int i = 0; i < antallkortleser; i++)
            
            var exists = System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly()!.Location)).Count() > i;
            if (exists) teller++;
            
            Console.WriteLine(teller);
            Console.Title = teller.ToString();
            Console.ReadKey();
            */