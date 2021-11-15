using CsvHelper;
using HtmlAgilityPack;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace hcad.scraper.byapn
{
    public class DataModelAPN
    {
        public string SearchedAPN { get; set; }
        public string Address { get; set; }
        public string SubjectCity { get; set; }
        public string SubjectState { get; set; }
        public string SubjectZip { get; set; }
        public string Name { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FirstName2 { get; set; }
        public string LastName2 { get; set; }
        public bool ShouldDelete { get; set; }
        public string MailingAddress { get; set; }
        public string MailingCity { get; set; }
        public string MailingState { get; set; }
        public string MailingZip { get; set; }
        public string StateClassCode { get; set; }
        public string LandArea { get; set; }
        public string TotalLivingArea { get; set; }
        public string LandMarketValue { get; set; }
        public string ImprovementMarketValue { get; set; }
        public string TotalAppraisedMarketValue { get; set; }
        public string BuiltYear { get; set; }
        public string Reason { get; set; }
    }
    public class Settings
    {
        public int threads { get; set; } = 1;
        public List<string> ownerKeys { get; set; } = new List<string>();
        public List<string> classCodeKeys { get; set; } = new List<string>();
        public bool checkMultipleEntries { get; set; }
        public bool showGUI { get; set; }
        public int entriesProcessedInSingleThread { get; set; } = 1;
        public string searchByAddress { get; set; }
        public string chromePath { get; set; }
    }

    class Program
    {
        static Settings settings = new Settings();
        static List<DataModelAPN> entries = new List<DataModelAPN>();
        public static bool CheckForLatestDrivers()
        {
            KillAlreadyRunningDriver();
            bool driverDownloaded = false;
            //Let get the chrome version
            var versionInfo = FileVersionInfo.GetVersionInfo(settings.chromePath);
            string currentChromeVersion = versionInfo.FileVersion;

            //Let check our driver version
            string driverVersion = string.Empty;

            string versionFileName = "version.txt";
            if (File.Exists(versionFileName))
                driverVersion = File.ReadAllText(versionFileName);


            if (driverVersion == currentChromeVersion)
            {
                Console.WriteLine("You already have the latest chromedriver installed.");
            }
            else
            {

                HtmlWeb web = new HtmlWeb();
                var doc = web.Load("https://chromedriver.chromium.org/downloads");
                var listNode = doc.DocumentNode.SelectSingleNode("/html/body/div[1]/div/div[2]/div[2]/div[1]/section[2]/div[2]/div/div/div/div/div/div/div/div/ul[1]");
                if (listNode != null)
                {
                    foreach (var li in listNode.ChildNodes.Where(x => x.Name == "li"))
                    {

                        var anchor = li.ChildNodes[0].ChildNodes.LastOrDefault();
                        if (anchor != null)
                        {
                            var version = anchor.InnerText.Replace("ChromeDriver ", "");
                            if (version == currentChromeVersion)
                            {
                                try
                                {
                                    using (var client = new WebClient())
                                    {
                                        client.DownloadFile($"https://chromedriver.storage.googleapis.com/{currentChromeVersion}/chromedriver_win32.zip", "chromedriver_win32.zip");
                                        if (File.Exists("chromedriver.exe"))
                                        {
                                            KillAlreadyRunningDriver();

                                            File.Delete("chromedriver.exe");
                                        }
                                        ZipFile.ExtractToDirectory("chromedriver_win32.zip", Environment.CurrentDirectory);
                                        if (!File.Exists(versionFileName))
                                            File.Create(versionFileName);
                                        File.WriteAllText(versionFileName, currentChromeVersion);
                                        driverDownloaded = true;
                                        Console.WriteLine($"Chrome driver version: {currentChromeVersion} has been downloaded.");
                                    }
                                    break;

                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Unable to download chrome driver version: {currentChromeVersion}. Reason: " + ex.Message);
                                    break;
                                }
                            }
                            else
                                Console.WriteLine($"Version not matched. Chrome Version: {currentChromeVersion}, Driver Version: {version}");
                        }
                    }
                }
            }
            return driverDownloaded;
        }

        private static void KillAlreadyRunningDriver()
        {
            Process[] chromeDriverProcesses = Process.GetProcessesByName("chromedriver");
            foreach (var chromeDriverProcess in chromeDriverProcesses)
            {
                var path = chromeDriverProcess.MainModule.FileName;
                if (path == Path.Combine(Environment.CurrentDirectory, "chromedriver.exe"))
                {
                    chromeDriverProcess.Kill();
                }
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine(DateTime.Now);
            List<string> apns = new List<string>();
            settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText("settings.json"));
            apns = File.ReadAllLines("file.csv").ToList();

            try
            {
                CheckForLatestDrivers();
                int num2 = (apns.Count() + settings.entriesProcessedInSingleThread - 1) / settings.entriesProcessedInSingleThread;
                List<Task> taskList = new List<Task>();
                for (int index = 1; index <= num2; ++index)
                {
                    int x = index - 1;
                    Task task = Task.Factory.StartNew(() => ScrapeByAPN(apns.Skip(x * settings.entriesProcessedInSingleThread).Take(settings.entriesProcessedInSingleThread).ToList()));
                    taskList.Add(task);
                    if (index % settings.threads == 0 || index == num2)
                    {
                        using (List<Task>.Enumerator enumerator = taskList.GetEnumerator())
                        {
                        label_11:
                            if (enumerator.MoveNext())
                            {
                                Task current = enumerator.Current;
                                while (!current.IsCompleted)
                                    ;
                                goto label_11;
                            }
                        }
                    }
                }
                entries = entries.OrderBy(x => x.Reason).ToList();
                var path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var subFolderPath = Path.Combine(path, "Scrapers/hcad");
                Directory.CreateDirectory(subFolderPath);
                var today = DateTime.Now;
                var span = string.Format("{0}{1}{2}{3}{4}{5}.csv", today.Year, today.Month, today.Day, today.Hour, today.Minute, today.Second);
                using (var writer = new StreamWriter(Path.Combine(subFolderPath, span)))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(entries);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            Console.WriteLine("Operation completed At: " + DateTime.Now);
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
        private static bool SplitName(string name, out string fn, out string ln, out string fn1, out string ln1)
        {
            string firstName = string.Empty, lastName = string.Empty, firstName1 = string.Empty, lastName1 = string.Empty;
            bool converted = false;
            if (!string.IsNullOrEmpty(name))
            {
                try
                {
                    if (name.Contains("&"))
                    {
                        var pieces = name.Split('&');
                        if (pieces.Length == 2)
                        {
                            var parts = pieces[0].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).Where(x => x.Length > 2).ToArray();
                            switch (parts.Length)
                            {
                                case 2:
                                    firstName = parts[1];
                                    lastName = parts[0];
                                    converted = true;

                                    break;
                                case 3:
                                    firstName = parts[1];
                                    lastName = parts[0];
                                    converted = true;

                                    break;
                                default:
                                    break;
                            }

                            var parts1 = pieces[1].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).Where(x => x.Length > 2).ToArray();
                            switch (parts1.Length)
                            {
                                case 1:
                                    firstName1 = parts1[0];
                                    lastName1 = parts[0];
                                    converted = true;
                                    break;
                                case 2:
                                    firstName1 = parts1[1];
                                    lastName1 = parts1[0];
                                    converted = true;
                                    break;
                                case 3:
                                    firstName = parts[1];
                                    lastName = parts[0];
                                    converted = true;

                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                    else
                    {
                        var parts = name.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).Where(x => x.Length > 2).ToArray();
                        switch (parts.Length)
                        {
                            case 2:
                                firstName = parts[1];
                                lastName = parts[0];
                                converted = true;
                                break;
                            case 3:
                                firstName = parts[1];
                                lastName = parts[0];
                                converted = true;

                                break;
                            case 4:
                                firstName = parts[1];
                                lastName = parts[0];
                                firstName1 = parts[3];
                                lastName1 = parts[2];
                                converted = true;
                                break;
                            default:
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to split name \"{0}\". Reason: {1}", name, ex.Message);
                }
            }

            fn = firstName;
            ln = lastName;
            fn1 = firstName1;
            ln1 = lastName1;
            return converted;
        }
        private static void ScrapeByAPN(List<string> apns)
        {
            try
            {
                ChromeOptions options = new ChromeOptions();
                options.AddArguments((IEnumerable<string>)new List<string>()
            {
                "--silent-launch",
                "--no-startup-window",
                "no-sandbox",
                "headless",
            });

                ChromeDriverService defaultService = ChromeDriverService.CreateDefaultService();
                defaultService.HideCommandPromptWindow = true;

                using (IWebDriver driver = (!settings.showGUI) ? ((IWebDriver)new ChromeDriver(defaultService, options)) : ((IWebDriver)new ChromeDriver()))
                {
                    foreach (var apn in apns)
                    {
                        string message = "";
                        DataModelAPN model = new DataModelAPN() { SearchedAPN = apn };
                        bool scraped = false;
                        int tryCount = 1;
                        do
                        {
                            message = "";
                            Console.WriteLine("Searching for : \"{0}\"", apn);
                            try
                            {
                                var url = "https://public.hcad.org/records/QuickSearch.asp";
                                driver.Navigate().GoToUrl(url);

                                IWait<IWebDriver> wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30.00));
                                wait.Until(driver1 => ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").Equals("complete"));

                                driver.FindElement(By.Id("acct")).SendKeys(apn);
                                driver.FindElement(By.XPath("//*[@id=\"Real_acct\"]/table/tbody/tr[3]/td[3]/nobr/input")).Click();

                                int waitCount = 0;
                                do
                                {
                                    try
                                    {
                                        driver.SwitchTo().Frame(driver.FindElement(By.Id("quickframe")));
                                    }
                                    catch { }
                                    Thread.Sleep(500);
                                    waitCount += 1;
                                } while (waitCount < 60 && !(driver.PageSource.Contains("Ownership History") || (driver.PageSource.Contains("tax year :") && driver.PageSource.Contains("record(s).")) || driver.PageSource.Contains("Currently, there are NO") || driver.PageSource.Contains("Please enter additional search criteria to reduce the number of records returned.")));

                                if (waitCount == 60)
                                {
                                    Console.WriteLine("Waited too long. but nothing found");
                                    message = "Waited too long. but nothing found";
                                    tryCount = 4;
                                }
                                bool entryfound = false;
                                if (driver.PageSource.Contains("tax year :") && driver.PageSource.Contains("record(s)."))
                                {
                                    var multiText = driver.FindElement(By.XPath("/html/body/table/tbody/tr[1]/td/p")).Text;
                                    Console.WriteLine(multiText);
                                    message = multiText;

                                    if (settings.checkMultipleEntries)
                                    {
                                        int total = Convert.ToInt32(driver.FindElement(By.XPath("/html/body/table/tbody/tr[1]/td/p/a/font[2]/b")).Text);

                                        try
                                        {
                                            driver.FindElement(By.Id("submit2")).Click();
                                        }
                                        catch { }
                                        for (int i = 0; i < total; i++)
                                        {

                                            driver.FindElement(By.XPath("/html/body/table/tbody/tr[2]/td/table/tbody/tr[" + (i + 2) + "]/td[1]/span/a")).Click();

                                            Thread.Sleep(3000);
                                            HtmlDocument page = new HtmlDocument();
                                            page.LoadHtml(driver.PageSource);

                                            var node = page.DocumentNode.SelectSingleNode("/html/body/table/tbody/tr/td/table[5]/tbody/tr[2]/td[2]/table/tbody/tr[1]/th");
                                            if (node != null)
                                            {
                                                var parts = node.InnerHtml.Split(new string[] { "<br>" }, StringSplitOptions.RemoveEmptyEntries);

                                                var sub1 = new HtmlDocument();
                                                sub1.LoadHtml(parts[0]);
                                                var desc = sub1.DocumentNode.InnerText.Replace("\n", "").Replace("\r", "").Trim();
                                                //Console.WriteLine("{0}================{1}", address.description, HttpUtility.HtmlDecode(desc));
                                                //if (address.description.Contains(HttpUtility.HtmlDecode(desc)))
                                                //{
                                                //    entryfound = true;
                                                //}
                                            }

                                            if (entryfound)
                                                break;
                                            else
                                            {
                                                driver.Navigate().Back();
                                                Thread.Sleep(1500);
                                                driver.SwitchTo().Frame(driver.FindElement(By.Id("quickframe")));
                                            }
                                        }

                                    }

                                    tryCount = 4;

                                }
                                if (driver.PageSource.Contains("Please enter additional search criteria to reduce the number of records returned."))
                                {
                                    var multiText = driver.FindElement(By.XPath("/html/body/table/tbody/tr/td/p[1]/a")).Text;
                                    Console.WriteLine(multiText);
                                    message = multiText;
                                    tryCount = 4;

                                }

                                else if (driver.PageSource.Contains("Currently, there are NO"))
                                {
                                    var errorText = driver.FindElement(By.XPath("/html/body/table/tbody/tr/td/table/tbody/tr[1]/td")).Text;
                                    Console.WriteLine(errorText);
                                    message = errorText;
                                    tryCount = 4;

                                }
                                else if (entryfound || driver.PageSource.Contains("Ownership History"))
                                {
                                    if (entryfound)
                                        message = "";
                                    HtmlDocument doc = new HtmlDocument();
                                    doc.LoadHtml(driver.PageSource);
                                    var AddressNode = doc.DocumentNode.SelectSingleNode("/html/body/table/tbody/tr/td/table[5]/tbody/tr[2]/td[2]/table/tbody/tr[2]/th");
                                    if (AddressNode != null)
                                    {
                                        var pieces = AddressNode.InnerHtml.Split(new string[] { "<br>" }, StringSplitOptions.RemoveEmptyEntries);
                                        if (pieces.Length > 0)
                                        {
                                            var sub2 = new HtmlDocument();
                                            sub2.LoadHtml(pieces.LastOrDefault());
                                            model.Address = pieces.FirstOrDefault();
                                            var citystatezip = sub2.DocumentNode.InnerText.Replace("\n", "").Replace("\r", "").Trim();
                                            var cszParts = citystatezip.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                                            if (cszParts.Length == 3)
                                            {
                                                Console.WriteLine("City: {0}", cszParts[0]);
                                                Console.WriteLine("State: {0}", cszParts[1]);
                                                Console.WriteLine("Zip: {0}", cszParts[2]);
                                                model.SubjectCity = cszParts[0];
                                                model.SubjectState = cszParts[1];
                                                model.SubjectZip = cszParts[2];
                                            }
                                            else if (cszParts.Length == 4)
                                            {
                                                Console.WriteLine("City: {0} {1}", cszParts[0], cszParts[1]);
                                                Console.WriteLine("State: {0}", cszParts[2]);
                                                Console.WriteLine("Zip: {0}", cszParts[3]);
                                                model.SubjectCity = cszParts[0] + " " + cszParts[1];
                                                model.SubjectState = cszParts[2];
                                                model.SubjectZip = cszParts[3];
                                            }
                                            else if (cszParts.Length == 5)
                                            {
                                                Console.WriteLine("City: {0} {1} {2}", cszParts[0], cszParts[1], cszParts[2]);
                                                Console.WriteLine("State: {0}", cszParts[3]);
                                                Console.WriteLine("Zip: {0}", cszParts[4]);
                                                model.SubjectCity = cszParts[0] + " " + cszParts[1] + " " + cszParts[2];
                                                model.SubjectState = cszParts[3];
                                                model.SubjectZip = cszParts[4];
                                            }
                                        }
                                    }
                                    var ownerNameAddressNode = doc.DocumentNode.SelectSingleNode("/html/body/table/tbody/tr/td/table[5]/tbody/tr[2]/td[1]/table/tbody/tr/th");
                                    if (ownerNameAddressNode != null)
                                    {
                                        var pieces = ownerNameAddressNode.InnerHtml.Split(new string[] { "<br>" }, StringSplitOptions.RemoveEmptyEntries);
                                        if (pieces.Length == 3)
                                        {
                                            var sub = new HtmlDocument();
                                            sub.LoadHtml(pieces[0]);

                                            var name = HttpUtility.HtmlDecode(sub.DocumentNode.InnerText.Replace("\n", "").Replace("\r", "").Trim());
                                            Console.WriteLine("Owner Name: {0}", name);
                                            model.Name = name;

                                            var sub1 = new HtmlDocument();
                                            sub1.LoadHtml(pieces[1]);

                                            var staddress = sub1.DocumentNode.InnerText.Replace("\n", "").Replace("\r", "").Trim();
                                            Console.WriteLine("Street Address: {0}", staddress);
                                            model.MailingAddress = staddress;
                                        }
                                        else if (pieces.Length == 4)
                                        {
                                            var sub = new HtmlDocument();
                                            sub.LoadHtml(pieces[0]);

                                            var name = HttpUtility.HtmlDecode(sub.DocumentNode.InnerText.Replace("\n", "").Replace("\r", "").Trim());
                                            Console.WriteLine("Owner Name: {0}", name);
                                            model.Name = name;

                                            var sub1 = new HtmlDocument();
                                            sub1.LoadHtml(pieces[1]);

                                            var staddress = sub1.DocumentNode.InnerText.Replace("\n", "").Replace("\r", "").Trim();
                                            Console.WriteLine("Street Address: {0}", staddress);
                                            model.MailingAddress = staddress;

                                            var sub2 = new HtmlDocument();
                                            sub2.LoadHtml(pieces[2]);

                                            var citystatezip = sub2.DocumentNode.InnerText.Replace("\n", "").Replace("\r", "").Trim();
                                            var cszParts = citystatezip.Split(new string[] { "&nbsp;" }, StringSplitOptions.RemoveEmptyEntries);
                                            if (cszParts.Length == 3)
                                            {
                                                Console.WriteLine("City: {0}", cszParts[0]);
                                                Console.WriteLine("State: {0}", cszParts[1]);
                                                Console.WriteLine("Zip: {0}", cszParts[2]);
                                                model.MailingCity = cszParts[0];
                                                model.MailingState = cszParts[1];
                                                model.MailingZip = cszParts[2];
                                            }
                                        }
                                        else if (pieces.Length == 5)
                                        {
                                            var sub = new HtmlDocument();
                                            sub.LoadHtml(pieces[0]);

                                            var name = HttpUtility.HtmlDecode(sub.DocumentNode.InnerText.Replace("\n", "").Replace("\r", "").Trim());

                                            model.Name = name;

                                            var sub4 = new HtmlDocument();
                                            sub4.LoadHtml(pieces[1]);

                                            var name4 = HttpUtility.HtmlDecode(sub4.DocumentNode.InnerText.Replace("\n", "").Replace("\r", "").Trim());
                                            model.Name += " " + name4;
                                            Console.WriteLine("Owner Name: {0}", model.Name);


                                            var sub1 = new HtmlDocument();
                                            sub1.LoadHtml(pieces[2]);

                                            var staddress = sub1.DocumentNode.InnerText.Replace("\n", "").Replace("\r", "").Trim();
                                            Console.WriteLine("Street Address: {0}", staddress);
                                            model.MailingAddress = staddress;

                                            var sub2 = new HtmlDocument();
                                            sub2.LoadHtml(pieces[3]);

                                            var citystatezip = sub2.DocumentNode.InnerText.Replace("\n", "").Replace("\r", "").Trim();
                                            var cszParts = citystatezip.Split(new string[] { "&nbsp;" }, StringSplitOptions.RemoveEmptyEntries);
                                            if (cszParts.Length == 3)
                                            {
                                                Console.WriteLine("City: {0}", cszParts[0]);
                                                Console.WriteLine("State: {0}", cszParts[1]);
                                                Console.WriteLine("Zip: {0}", cszParts[2]);
                                                model.MailingCity = cszParts[0];
                                                model.MailingState = cszParts[1];
                                                model.MailingZip = cszParts[2];
                                            }
                                        }
                                        else if (pieces.Length == 6)
                                        {
                                            var sub = new HtmlDocument();
                                            sub.LoadHtml(pieces[0]);

                                            var name = HttpUtility.HtmlDecode(sub.DocumentNode.InnerText.Replace("\n", "").Replace("\r", "").Trim());

                                            model.Name = name;

                                            var subx = new HtmlDocument();
                                            subx.LoadHtml(pieces[1]);

                                            var namex = HttpUtility.HtmlDecode(subx.DocumentNode.InnerText.Replace("\n", "").Replace("\r", "").Trim());

                                            model.Name += " " + namex;

                                            var sub4 = new HtmlDocument();
                                            sub4.LoadHtml(pieces[2]);

                                            var name4 = HttpUtility.HtmlDecode(sub4.DocumentNode.InnerText.Replace("\n", "").Replace("\r", "").Trim());
                                            model.Name += " " + name4;
                                            Console.WriteLine("Owner Name: {0}", model.Name);

                                            var sub1 = new HtmlDocument();
                                            sub1.LoadHtml(pieces[3]);

                                            var staddress = sub1.DocumentNode.InnerText.Replace("\n", "").Replace("\r", "").Trim();
                                            Console.WriteLine("Street Address: {0}", staddress);
                                            model.MailingAddress = staddress;

                                            var sub2 = new HtmlDocument();
                                            sub2.LoadHtml(pieces[4]);

                                            var citystatezip = sub2.DocumentNode.InnerText.Replace("\n", "").Replace("\r", "").Trim();
                                            var cszParts = citystatezip.Split(new string[] { "&nbsp;" }, StringSplitOptions.RemoveEmptyEntries);
                                            if (cszParts.Length == 3)
                                            {
                                                Console.WriteLine("City: {0}", cszParts[0]);
                                                Console.WriteLine("State: {0}", cszParts[1]);
                                                Console.WriteLine("Zip: {0}", cszParts[2]);
                                                model.MailingCity = cszParts[0];
                                                model.MailingState = cszParts[1];
                                                model.MailingZip = cszParts[2];
                                            }
                                        }

                                        string fn, ln, fn1, ln1;
                                        fn = ln = fn1 = ln1 = string.Empty;
                                        if (SplitName(model.Name, out fn, out ln, out fn1, out ln1))
                                        {
                                            Console.Write("{0}\n{1}\n{2}\n{3}", fn, ln, fn1, ln1);
                                        }
                                        model.FirstName = fn;
                                        model.LastName = ln;
                                        model.FirstName2 = fn1;
                                        model.LastName2 = ln1;
                                    }

                                    var classCode = doc.DocumentNode.SelectSingleNode("/html/body/table/tbody/tr/td/table[6]/tbody/tr[2]/td[1]");
                                    if (classCode != null)
                                    {
                                        var code = HttpUtility.HtmlDecode(classCode.InnerText.Trim());
                                        Console.WriteLine("Class Code: {0}", code);
                                        model.StateClassCode = code;
                                    }

                                    var landArea = doc.DocumentNode.SelectSingleNode("/html/body/table/tbody/tr/td/table[6]/tbody/tr[4]/td[1]");
                                    if (landArea != null)
                                    {
                                        var code = HttpUtility.HtmlDecode(landArea.InnerText.Trim());
                                        Console.WriteLine("Land Area: {0}", code);
                                        model.LandArea = code;
                                    }

                                    var totalLivingArea = doc.DocumentNode.SelectSingleNode("/html/body/table/tbody/tr/td/table[6]/tbody/tr[4]/td[2]");
                                    if (totalLivingArea != null)
                                    {
                                        var code = HttpUtility.HtmlDecode(totalLivingArea.InnerText.Trim());
                                        Console.WriteLine("Total Living Area: {0}", code);
                                        model.TotalLivingArea = code;
                                    }

                                    var valuation2020 = doc.DocumentNode.SelectSingleNode("/html/body/table/tbody/tr/td/table[12]/tbody/tr[6]/td[5]");
                                    if (valuation2020 != null)
                                    {
                                        var code = HttpUtility.HtmlDecode(valuation2020.InnerText.Trim());
                                        if (code == "Pending")
                                        {
                                            //Use 2019

                                            var land = doc.DocumentNode.SelectSingleNode("/html/body/table/tbody/tr/td/table[12]/tbody/tr[4]/td[2]");
                                            if (land != null)
                                            {
                                                var landText = HttpUtility.HtmlDecode(land.InnerText.Trim());
                                                Console.WriteLine("2019 Land: {0}", landText);
                                                model.LandMarketValue = landText;
                                            }

                                            var improvement = doc.DocumentNode.SelectSingleNode("/html/body/table/tbody/tr/td/table[12]/tbody/tr[5]/td[2]");
                                            if (improvement != null)
                                            {
                                                var imp = HttpUtility.HtmlDecode(improvement.InnerText.Trim());
                                                Console.WriteLine("2019 Improvement: {0}", imp);
                                                model.ImprovementMarketValue = imp;
                                            }

                                            var total = doc.DocumentNode.SelectSingleNode("/html/body/table/tbody/tr/td/table[12]/tbody/tr[6]/td[2]");
                                            if (total != null)
                                            {
                                                var to = HttpUtility.HtmlDecode(total.InnerText.Trim());
                                                Console.WriteLine("2019 Total : {0}", to);
                                                model.TotalAppraisedMarketValue = to;
                                            }
                                        }
                                        else
                                        {
                                            var land = doc.DocumentNode.SelectSingleNode("/html/body/table/tbody/tr/td/table[12]/tbody/tr[4]/td[5]");
                                            if (land != null)
                                            {
                                                var landText = HttpUtility.HtmlDecode(land.InnerText.Trim());
                                                Console.WriteLine("2020 Land: {0}", landText);
                                                model.LandMarketValue = landText;
                                            }

                                            var improvement = doc.DocumentNode.SelectSingleNode("/html/body/table/tbody/tr/td/table[12]/tbody/tr[5]/td[5]");
                                            if (improvement != null)
                                            {
                                                var imp = HttpUtility.HtmlDecode(improvement.InnerText.Trim());
                                                Console.WriteLine("2020 Improvement: {0}", imp);
                                                model.ImprovementMarketValue = imp;
                                            }

                                            Console.WriteLine("2020 Total: {0}", code);
                                            model.TotalAppraisedMarketValue = code;
                                        }
                                    }


                                    var builtYear = doc.DocumentNode.SelectSingleNode("/html/body/table/tbody/tr/td/table[15]/tbody/tr[3]/td[2]");
                                    //if (builtYear == null)
                                    //{
                                    //    doc.DocumentNode.SelectSingleNode("/html/body/table/tbody/tr/td/table[16]/tbody/tr[4]/td[6]");
                                    //}
                                    if (builtYear != null)
                                    {
                                        var code = HttpUtility.HtmlDecode(builtYear.InnerText.Trim());
                                        Console.WriteLine("Year Built: {0}", code);
                                        model.BuiltYear = code;
                                    }

                                }



                                if (!string.IsNullOrEmpty(model.Name))
                                {
                                    if (model.Name.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).Any(x => settings.ownerKeys.Exists(m => m.ToLower() == x.ToLower())))
                                    {
                                        Console.WriteLine("Will not add to export because owner name contains special key");
                                        message = "Will not add to export because owner name contains special key";
                                    }
                                    else if (model.Name.ToUpper() == "CURRENT OWNER")
                                    {
                                        Console.WriteLine("Will not add to export because owner name is current owner");
                                        message = "Will not add to export because owner name is current owner";
                                    }
                                    else
                                    {
                                        if (!settings.classCodeKeys.Any(x => model.StateClassCode.ToLower().Contains(x.ToLower())))
                                        {
                                            scraped = true;
                                        }
                                        else
                                        {
                                            Console.WriteLine("Will not add to export because State Class Code contains special key");
                                            message = "Will not add to export because State Class Code contains special key";
                                        }
                                    }
                                }
                                else if (string.IsNullOrEmpty(model.Name) && string.IsNullOrEmpty(message))
                                    message = "Owner name not found";
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Unable to process \"" + apn + "\". Reason: " + ex.Message);
                                // message = "Unable to process \"" + address + "\". Reason: " + ex.Message;
                            }
                            tryCount += 1;
                        } while (!scraped && tryCount < 4);
                        model.Reason = message;
                        entries.Add(model);
                        Console.WriteLine("=================================================================");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to continue. Reason: " + ex.Message);
            }

        }
    }
}
