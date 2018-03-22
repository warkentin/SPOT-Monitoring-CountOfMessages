﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Collections;
using System.ServiceProcess;

namespace SPOT_Monitoring
{
    class Program
    {
        static void Main(string[] args)
        {
            // SQL-Verbindung erstellen 
            SqlConnection myConnection = new SqlConnection("user id=sa; password=;server=dpn-svr-membrain\\SQLEXPRESS;" +
                                                           "Trusted_Connection=yes; database=SPOT; connection timeout=30");

            // SQL-Datenvariable erstellen
            SqlDataReader rdr = null, rdr_werk = null, rdr_schwellwert = null;

            // Variablen
            int schwellwert;

            logging("------------------------------------------------------------------- ");

            try
            {
                // Lizenzserver prüfen
                string serviceName = "Membrain License Server";
                string machineName = "foi-svr-spot01";
                string l_status = "";
                try
                {
                    ServiceController service = new ServiceController(serviceName, machineName);
                    l_status = service.Status.ToString();
                }
                catch (Exception e)
                {
                    logging("Lizenz - Fehler beim Prüfen des Dienstes\n");
                    logging(e.ToString());
                }

                // Log-Datei füllen
                logging(DateTime.Now + " - Lizenz-Status: " + l_status);

                if (l_status != "Running")
                {
                    if (System.IO.File.Exists(@"C:\Temp\SPOT-Monitor\alarm-Lizenz.txt"))
                    {
                        // Nix machen - eine Alarm-Mail wurde bereits verschickt
                    }
                    else
                    {
                        // E-Mail versenden
                        try
                        {
                            mail_versenden("spot-monitor@polipol.de", "m.warkentin@polipol.de,p.zenner@polipol.de", "Lizenzserver Membrain ",
                                "Hallo SPOT-Admin,\n" + "bitte nachschauen!\n\nAktuell läuft der Lizenzserver-Dienst in Foieni nicht!\n\n "
                                + "Viele Grüße\ndein SPOT");

                            // Log-Datei füllen
                            logging(DateTime.Now + " - Lizenz: Alarm-E-Mail wurde verschickt!");

                            // Log-Datei dazu anlegen
                            using (System.IO.StreamWriter file = new System.IO.StreamWriter(@"C:\Temp\SPOT-Monitor\alarm-Lizenz.txt", true))
                            {
                                file.WriteLine(DateTime.Now + " - Lizenz - E-Mail wurde verschickt!");
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log-Datei füllen                                    
                            logging(ex.ToString());
                        }
                    }
                }
                else
                {
                    // Prüfung, ob ein aktueller Fall vorlag
                    if (System.IO.File.Exists(@"C:\Temp\SPOT-Monitor\alarm-Lizenz.txt"))
                    {
                        // Mail verschicken
                        mail_versenden("spot-monitor@polipol.de", "m.warkentin@polipol.de,p.zenner@polipol.de", "GELÖST: Lizenzserver Membrain ",
                                "Hallo SPOT-Admin,\n" + "Der Lizenzserver läuft wieder!\n\n " + "Viele Grüße\ndein SPOT");

                        // Log-Datei füllen
                        logging(DateTime.Now + " - Lizenz: Entwarnungs-E-Mail wurde verschickt!");

                        // Alarm-Datei löschen
                        System.IO.File.Delete(@"C:\Temp\SPOT-Monitor\alarm-Lizenz.txt");
                    }
                }

                // SQL-Datenbank öffnen
                myConnection.Open();

                // Schwellwert auslesen
                SqlCommand cmd_schwellwert = new SqlCommand("select Wert from Parameter where Parameter = 'Schwellwert'", myConnection);
                rdr_schwellwert = cmd_schwellwert.ExecuteReader();
                if (rdr_schwellwert.HasRows)
                {
                    rdr_schwellwert.Read();
                    string schwelle = rdr_schwellwert[0].ToString();
                    if (int.TryParse(schwelle, out schwellwert) == false) schwellwert = 50;
                }
                else schwellwert = 50;
                rdr_schwellwert.Close();                

                // Werke auslesen                
                SqlCommand cmd_werk = new SqlCommand("select * from Werke", myConnection);
                rdr_werk = cmd_werk.ExecuteReader();
                Dictionary<string, string> werks = new Dictionary<string, string>();
                ArrayList werke = new ArrayList();                
                while (rdr_werk.Read())
                {
                    werks.Add(rdr_werk[0].ToString().Trim(), rdr_werk[1].ToString().Trim());
                }
                rdr_werk.Close();

                logging("------------------------------------------------------------------- ");
                logging("------------------------- Schwellwert " + schwellwert + " -------------------------");
                logging("------------------------------------------------------------------- ");

                // Jedes Werk abfragen
                foreach (KeyValuePair<string, string> kvp in werks)
                {
                    try
                    {
                        // SQL-Abfrage starten
                        SqlCommand cmd = new SqlCommand("select count(*) from " + kvp.Key + "_message where isprocessed = 0", myConnection);
                        rdr = cmd.ExecuteReader();

                        while (rdr.Read())
                        {
                            // Das Ergebnis ausgeben
                            int open_messages = (int)rdr[0];

                            // Log-Datei füllen
                            logging(DateTime.Now + " - " + kvp.Value.PadRight(10, ' ') + ": " + open_messages + " Messages offen");

                            // Wenn mehr als x Messages offen sind, soll eine Mail verschickt werden
                            if (open_messages > schwellwert)
                            {
                                if (System.IO.File.Exists(@"C:\Temp\SPOT-Monitor\alarm-" + kvp.Key + ".txt"))
                                {
                                    // Nix machen - eine Alarm-Mail wurde bereits verschickt
                                }
                                else
                                {
                                    // E-Mail versenden
                                    try
                                    {
                                        mail_versenden("spot-monitor@polipol.de", "m.warkentin@polipol.de,p.zenner@polipol.de", "PROBLEM im Werk: "
                                            + kvp.Value, "Hallo SPOT-Admin,\n" + "bitte nachschauen!\n\nAktuell sind "
                                            + open_messages + " Messages im Status 'Nicht Versendet'!\n\n" + "Viele Grüße\ndein SPOT");

                                        // Log-Datei füllen
                                        logging(DateTime.Now + " - " + kvp.Value + ": Alarm-E-Mail wurde verschickt!");

                                        // Log-Datei dazu anlegen
                                        using (System.IO.StreamWriter file = new System.IO.StreamWriter(@"C:\Temp\SPOT-Monitor\alarm-" + kvp.Key + ".txt", true))
                                        {
                                            file.WriteLine(DateTime.Now + " - " + kvp.Value.PadRight(10,' ') + ": " + open_messages + " Messages offen - E-Mail wurde verschickt!");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // Log-Datei füllen                                    
                                        logging(ex.ToString());
                                    }
                                }
                            }
                            else if (open_messages < schwellwert + 1)
                            {
                                // Prüfung, ob ein aktueller Fall vorlag
                                if (System.IO.File.Exists(@"C:\Temp\SPOT-Monitor\alarm-" + kvp.Key + ".txt"))
                                {
                                    // Mail verschicken
                                    mail_versenden("spot-monitor@polipol.de", "m.warkentin@polipol.de,p.zenner@polipol.de", "Problem GELÖST im Werk: "
                                        + kvp.Value, "Hallo SPOT-Admin,\n" + "das Problem ist behoben!\n\nAktuell sind "
                                        + open_messages + " Messages im Status 'Nicht Versendet'!\n\n" + "Viele Grüße\ndein SPOT");

                                    // Log-Datei füllen
                                    logging(DateTime.Now + " - " + kvp.Value.PadRight(10, ' ') + ": Entwarnungs-E-Mail wurde verschickt!");

                                    // Alarm-Datei löschen
                                    System.IO.File.Delete(@"C:\Temp\SPOT-Monitor\alarm-" + kvp.Key + ".txt");
                                }
                            }
                        }
                        // SQL-Reader muss geschlossen werden
                        if (rdr != null) rdr.Close();
                    }
                    catch (Exception b)
                    {
                        logging(kvp.Key + " - Fehler beim Öffnen der Abfrage im Werk\n");
                        logging(b.Message);
                    }
                }                                            
            }           
            catch (Exception e)
            {
                // SQL-Datenbank lässt sich nicht öffnen
                logging("Fehler beim Öffnen der Datenbank SPOT in Diepenau\n");
                logging(e.ToString());
            }
            finally
            {
                // SQL-Verbindung trennen / schließen                
                if (myConnection != null) myConnection.Close();
            }
            
            // Am Ende des Tages gehört diese Datei ins Archiv
            if (DateTime.Now.Hour == 23 && DateTime.Now.Minute > 51)
            {
                System.IO.File.Copy(@"C:\Temp\SPOT-Monitor\log.txt", @"C:\Temp\SPOT-Monitor\Archiv\" 
                                    + DateTime.Now.Year + "_" + DateTime.Now.Month + "_" + DateTime.Now.Day + "_" + "log.txt", true);
                System.IO.File.Delete(@"C:\Temp\SPOT-Monitor\log.txt");
            }            
        }

        static void logging(string message)
        {
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(@"C:\Temp\SPOT-Monitor\log.txt", true))
            {
                file.WriteLine(message);
            }
        }

        static void mail_versenden(string absender, string empfänger, string betreff, string mail_text)
        {
            SmtpClient m = new SmtpClient();
            m.Host = "192.168.48.19";
            m.Port = 25;
            m.Send(absender, empfänger, betreff, mail_text);
        }
    }    
}

/*
 * E-Mail-Empfänger
 * SMTP-Server
 * E-Mail-Texte
 * Pfade zu den LOG-Dateien 
 */