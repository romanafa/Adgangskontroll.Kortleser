﻿using System.IO.Ports;

namespace Adgangskontroll.Kortleser //Fjerne "Oppgave x:" før innlevering
{
    internal class Program
    {
        static string? enMelding = "";      //Oppgave 2     //linje 27 //må ha static hvis BrukerSkriverInnKortIDPin metoden brukes
        static string? innlest_tekst = "";  //Oppgave 3        
        static int kortPINFraSentral;       //Oppgave 4
        static int kortIDFraSentral;        //Oppgave 4
        static int kortID;                  //Oppgave 4
        static int kortPIN;                 //Oppgave 4
        static int tidulåst = 0;            //Oppgave 4
        static bool dørLåst;                //Oppgave 5
        static bool dørPosisjon;            //Oppgave 5
        static bool dørAlarm;               //Oppgave 6
        static bool dørBruttopp;            //Oppgave 6
        //static string data = "";                          //linje 26
        static int døråpenalarmtid = 10;    //Hvor lenge døren er åpen før alarmen går. I sekunder.
        static int dørlåstopptid = 5;       //Hvor lenge døren er ulåst ved bruk av adgangskort. I sekunder.
        static int tid = 0;

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            //Mulig feil: hvis portnummer ikke stemmer eller den er opptatt så kjøres ikke programmet.
            SerialPort sp = new SerialPort("COM5", 9600);
            string data = "";               //linje 17
            //string? enMelding = "";       //linje 7

            /*Erik fiks*/
            //Oppgave 1: Registrer et kortlesernummer når prosessen og sender det til sentralen.
            //Console.WriteLine("Skriv inn kortlesernummer i format '####' hvor '#' er et siffer.");
            //string? kortlesernummer = Console.ReadLine(); //Erik skal bruke denne for å gi til database.
            //Console.Title = kortlesernummer!;

            Console.WriteLine("Når bruker skal angi PIN-kode og kortID må det oppgies på formatet: $F3251$H1826\n" +
                "Hvor $F3251 er kort ID-en = 3251 og $H1826 er kort PIN-koden = 1826");

            //Oppgave 5: Starter en tidsteller som kjøres parallelt med resten av koden.
            //Som brukes når døren er åpen for lenge eller tiden døren er ulåst.
            Thread tid = new Thread(DørTid);
            tid.Start();


            //Oppgave 3: Starter tråd for å lese meldinger bruker skriver i konsollvinduet.
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
                SendEnMelding("$O51", sp);  //Slå låst på

                while (true)
                {
                    innlest_tekst = "";             //Nullstiller innlest tekst.
                    data = data + MottaData(sp);    //Setter mottatt informasjon fra kort til data.

                    //Oppgave 2: Sjekker om kort har sendt data og utfører forskjellige oppgaver.
                    if (EnHelMeldingMotatt(data))
                    {
                        enMelding = HentUtEnMelding(ref data);  //Ta ut meldingen (bevar eventuell rest)
                        Console.WriteLine(enMelding);           //Skriver ut meldingen
                        //Henter informasjon fra kortet og deklarer variablene.
                        kortID = KortID(enMelding);     //trengs ikke lenger
                        kortPIN = KortPin(enMelding);   //trengs ikke lenger
                        dørLåst = Dørlåst(enMelding);
                        dørPosisjon = DørPosisjon(enMelding);
                        dørBruttopp = DørBruttopp(enMelding);
                        dørAlarm = DørAlarm(enMelding);
                        //BrukerSkriverInnKortIDPin(innlest_tekst); //Må finne en løsning. Alexander/Nathalie fiks.

                        //De to if else-ene trengs ikke, men er for visualisering av kode.
                        if (dørLåst)
                            Console.WriteLine("Dør låst");
                        else
                            Console.WriteLine("Dør ulåst");

                        if (dørPosisjon)
                            Console.WriteLine("Dør åpen");
                        else
                            Console.WriteLine("Dør lukket");

                        //Oppgave 4: Sjekker PIN-kode og kortID, låser opp døren hvis det er riktig og lar den være ulåst i "dørlåstopptid" sekunder.
                        //trengs ikke lenger
                        if (Adgangsforepørsel(kortPIN, kortID))
                        {
                            dørLåst = false;
                            tidulåst = dørlåstopptid;
                            enMelding = enMelding.Insert(enMelding.IndexOf('E') + 6, "0");
                            enMelding = enMelding.Remove(enMelding.IndexOf('E') + 7, 1);
                            Console.WriteLine("Godkjent! Døren låses opp");//Trengs ikke, men er for visualisering av kode.
                            SendEnMelding("$O50", sp);
                        }

                        //Oppgave 7: Hvis døren lukkes, låses døren
                        if (!dørPosisjon && !dørLåst && tidulåst <= 0)
                        {
                            dørLåst = true;
                            enMelding = enMelding.Insert(enMelding.IndexOf('E') + 6, "1");
                            enMelding = enMelding.Remove(enMelding.IndexOf('E') + 7, 1);
                            Console.WriteLine("Dør lukket og låses");//De to if else-ene trengs ikke, men er for visualisering av kode.
                            SendEnMelding("$O51", sp);
                        }

                        /*Erik fiks*/
                        //Oppgave 7: Hvis alarmen er på, men døren er lukket og ikke brutt opp så skrues alarmen av. 
                        if (dørAlarm && !dørPosisjon && !dørBruttopp)
                        {
                            dørAlarm = false;
                            SendEnMelding("$O70", sp);
                            /*Erik skrive kode for å sende det til sentral*/
                            Console.WriteLine("Alarm: Av");
                        }

                        /*Erik fiks*/
                        //Oppgave 6: Sjekker om døren er brutt opp.
                        if (dørBruttopp)
                        {
                            Console.WriteLine("Dør brutt opp");
                            /*Erik skrive kode for å sende det til sentral*/
                        }
                        /*Erik fiks*/
                        //Oppgave 6: Skrur på alarmen hvis døren er brutt opp eller vært åpen for lenge.
                        if (dørAlarm)
                        {
                            SendEnMelding("$O71", sp);
                            Console.WriteLine("Alarm: På");
                            /*Erik skrive kode for å sende det til sentral*/
                        }
                    }

                    //Oppgave 3: Sender meldinger til kortet
                    SendEnMelding(innlest_tekst, sp);

                    //Oppgave 4 behandle adgangsforespørsler(motta kortid +PINkode fra bruker)
                    //Når bruker skal angi PIN-kode og kortID må det oppgies på formatet: $F3251$H1826
                    //Hvor "$F3251" er kort ID-en = 3251 og $H1826 er kort PIN-koden = 1826.                    
                    //Oppgave 4: Leser av oppgitt kortid fra bruker.
                    //Oppgave 4: Leser av oppgitt kort PIN-kode fra bruker.
                    BrukerSkriverInnKortIDPin(innlest_tekst);

                    //Oppgave 4: Sjekker PIN-kode og kortID, låser opp døren hvis det er riktig og lar den være ulåst i "dørlåstopptid" sekunder.
                    if (Adgangsforepørsel(kortPIN, kortID))
                    {
                        dørLåst = false;
                        tidulåst = dørlåstopptid;
                        Console.WriteLine("Godkjent! Døren låses opp");//Trengs ikke, men er for visualisering av kode.
                        SendEnMelding("$O50", sp);
                    }



                }
            }
        } // av Main


        //Oppgave 4: Leser av oppgitt kortid fra kortet/simsim.
        static int KortID(string EnMelding)     //trengs ikke lenger
        {
            int indeksIDStart = EnMelding.IndexOf('F');
            int kortid = Convert.ToInt32(EnMelding.Substring(indeksIDStart + 1, 4));
            return kortid;
        }

        //Oppgave 4: Leser av oppgitt kort PIN-kode fra kortet/simsim.
        static int KortPin(string EnMelding)    //trengs ikke lenger
        {
            int indeksPinStart = EnMelding.IndexOf('H');
            int kortpin = Convert.ToInt32(EnMelding.Substring(indeksPinStart + 1, 4));
            return kortpin;
        }

        //Oppgave 4
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

        /*Erik fiks*/
        //Oppgave 4: Behandler adgangsforespørsler fra kortet, sjekker om de samsvarer med det som er hentet fra sentral.
        static bool Adgangsforepørsel(int kortpin, int kortid)
        {
            bool adgang = false;
            kortPINFraSentral = 1023;   //Erik fikse innhentingen og sending til sentral. Vi har bare satt det til 1023 for testing.
            kortIDFraSentral = 1023;    //Erik fikse innhentingen

            if (kortid == kortIDFraSentral && kortpin == kortPINFraSentral)
            {
                adgang = true;
            }
            return adgang;
        }

        //Oppgave 5: Sjekker om døren er åpen eller lukket fra innhentet data og hvis den er lukket så nullstilles tiden.
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

        //Oppgave 5: Sjekker om døren er låst eller ulåst fra innhentet data.
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

        /*Erik fiks*/
        //Oppgave 6: Sjekker om døren har blitt brutt opp eller ikke fra innhentet data.
        static bool DørBruttopp(string EnMelding)
        {
            bool brudd = false;
            int indeksPotm = EnMelding.IndexOf('G'); //Potm 1 (verdi over 500 skal representere dør brutt opp): //$A001B20241014C104726D00000000E00000000F0500G0736H0500I020J020#
            int råPotm = Convert.ToInt32(EnMelding.Substring(indeksPotm + 1, 4));

            if (råPotm > 500)
            {
                brudd = true;
            }
            //Erik fikse sending til sentral.
            return brudd;
        }

        /*Erik fiks*/
        //Oppgave 6: Sjekker om dør alarmen er på fra innhentet data og skrur alarmen på hvis døren har blitt brutt opp eller har vært åpen for lenge. 
        static bool DørAlarm(string EnMelding)
        {
            bool alarm = false;
            int indeksDørAlarm = EnMelding.IndexOf('E'); //Utgang 7 (Indikere alarm) (av/på): //$A001B20241014C104726D00000000E00000001F0500G0500H0500I020J020#
            int råDørAlarm = Convert.ToInt32(EnMelding.Substring(indeksDørAlarm + 8, 1));

            if ((råDørAlarm == 1) || (dørBruttopp) || (tid > døråpenalarmtid))
            {
                alarm = true;
            }
            // Erik må fikse kommunikasjon til sentral
            return alarm;
        }


        //Oppgave 2: Definerer når informasjonen mottatt starter og slutter og
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

        //Oppgave 2: Leser av informasjon mottatt fra kortet/seriell.
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

        //Oppgave 2: Sjekker om informasjon mottatt fra kortet/seriell er en helt melding.
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

        //Oppgave 3: Leser meldinger bruker skriver i konsollvinduet.
        static void LestMelding()
        {
            while (true)
            {
                innlest_tekst = Console.ReadLine();
            }
        }

        //Oppgave 3: Sender innlest melding fra bruker til kortet/seriell.
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

        //Oppgave 5: Øker og minker tiden (for åpnet dør alarm og være ulåst) som har gått siden programmet startet.
        static void DørTid()
        {
            while (true)
            {
                Thread.Sleep(1000);
                tid++;
                tidulåst--;
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