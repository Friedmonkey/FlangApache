using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using NetFwTypeLib;
using static System.Net.Mime.MediaTypeNames;

namespace FlangWebsite
{
    public delegate WebResponse WebsiteVisitHandeler(Website sender, WebsiteContext context);
    public class Website
    {
        //Fields
        private HttpListener listener;
        private string url = "http://*:{port}/";

        public event WebsiteVisitHandeler onVisit;

        public Website(int port = 8000, WebsiteVisitHandeler onVisit = null, bool OpenPort = false)
        {
            Initialize();
            this.SetPort(port);
            this.onVisit = onVisit;
            if (OpenPort)
            {
                if (isAdmin)
                    this.OpenFriedPort();
            }
        }
        public void Initialize()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                isAdmin = IsAdministrator();
                if (!isAdmin)
                {
                    url = "http://localhost:{port}/";
                    Console.ForegroundColor = ConsoleColor.Red;
                    string text = string.Empty;
                    text += "===============================================\n";
                    text += "ATTENTION: Your website will not be available\n";
                    text += "from outside your device/network, as the server\n";
                    text += "was not started with administrative privileges.\n";
                    text += "===============================================\n";
                    Console.WriteLine(text);
                    Console.ResetColor();
                }
            }

            if (!Directory.Exists("data")) Directory.CreateDirectory("data");
            if (!Directory.Exists("www")) Directory.CreateDirectory("www");


            if (!File.Exists("data/sessiondb.json"))
            {
                File.Create("data/sessiondb.json").Close();
                File.WriteAllText("data/sessiondb.json", "{}");
            }
        }
        //idek
        public int Port { get; protected set; } = 8000;
        public bool Running { get; protected set; } = false;
        public bool isAdmin { get; protected set; } = false;
        public string PotentialAddress => GetPossibleAddresses().First();
        public string runLocation { get; protected set; } = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        public List<string> GetPossibleAddresses()
        {
            var localIPs = Dns.GetHostAddresses(Dns.GetHostName())
                .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(ip => ip.ToString())
                .ToList();
            for (int i = 0; i < localIPs.Count; i++)
            {
                localIPs[i] = localIPs[i] + ":" + Port;
            }
            localIPs.Reverse();
            return localIPs;
        }

        public void Start()
        {
            if (Running)
                throw new Exception("Website is already running.");
            Running = true;
            listener = new HttpListener();
            listener.Prefixes.Add(url.Replace("{port}", Port.ToString()));
            try
            {
                listener.Start();
            }
            catch (System.Net.HttpListenerException ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }
            Console.WriteLine("Listening for connections on " + url.Replace("{port}", Port.ToString()));

            // Handle requests
            Task listenTask = HandleIncomingConnections();
            listenTask.GetAwaiter().GetResult();
        }
        public async Task StartAsync()
        {
            if (Running)
                throw new Exception("Website is already running.");
            Running = true;
            listener = new HttpListener();
            listener.Prefixes.Add(url.Replace("{port}", Port.ToString()));
            try
            {
                listener.Start();
            }
            catch (System.Net.HttpListenerException ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }
            Console.WriteLine("Listening for connections on " + url.Replace("{port}", Port.ToString()));

            // Handle requests
            Task listenTask = HandleIncomingConnections();
            await listenTask;
        }
        public void Stop()
        {
            Running = false;
        }
        public async Task HandleIncomingConnections()
        {
            bool runServer = true;

            while (runServer)
            {
                try
                {
                    HttpListenerContext ctx = await listener.GetContextAsync();

                    // Peel out the requests and response objects
                    var req = ctx.Request;
                    var res = ctx.Response;
                    //string page = req.Path.Value.Substring(1);
                    string page = req.Url.LocalPath.Substring(1);
                    string filePath = Path.Combine("www", page);

                    WebsiteRequest request = new WebsiteRequest();
                    request.HttpMethod = req.HttpMethod;
                    request.Url = req.Url;
                    request.RawUrl = req.RawUrl;
                    request.ContentType = req.ContentType;
                    request.Cookies = req.Cookies;
                    request.Headers = req.Headers;
                    request.QueryString = req.QueryString;
                    request.KeepAlive = req.KeepAlive;
                    request.HasEntityBody = req.HasEntityBody;
                    request.InputStream = req.InputStream;
                    request.ContentDict = new Dictionary<string, object>();

                    if (req.HasEntityBody)
                    {
                        using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                        {
                            request.ContentRaw = reader.ReadToEnd();
                        }

                        if (req.ContentType == "application/x-www-form-urlencoded")
                        {
                            string[] parts = request.ContentRaw.Split('&');

                            foreach (string part in parts)
                            {   
                                string[] urlEncodedData = part.Split('=');
                                if (urlEncodedData.Length == 0) continue;
                                if (urlEncodedData.Length == 1) request.ContentDict.Add(urlEncodedData[0], 0);
                                if (urlEncodedData.Length == 2) request.ContentDict.Add(urlEncodedData[0], HttpUtility.UrlDecode(urlEncodedData[1]));
                            }
                        }
                    }

                    WebsiteContext context = new WebsiteContext();
                    context.Request = request;

                    context.Page = page;
                    string ipAddres = req.RemoteEndPoint.Address.ToString();
                    if (ipAddres == "::1")
                        ipAddres = "127.0.0.1";
                    context.IpAddress = ipAddres;
                    WebResponse webResponse = onVisit?.Invoke(this, context);

                    if (webResponse == null)
                        continue;


                    await res.OutputStream.WriteAsync((byte[])webResponse.RawBytes);
                    res.Close();
                    continue;

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Request failed: " + ex.Message);
                }
            }
        }
        #region helpers
        public bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
        #endregion
        #region firewall bs
        private static void AddRule(String name, String Description,
   NET_FW_ACTION_ Action, NET_FW_RULE_DIRECTION_ Direction, String LocalPort,
   bool Enabled = true, int Protocole = 6, String RemoteAdresses = "localsubnet", String ApplicationName = "ScreenTask")
        {
            Type Policy2 = Type.GetTypeFromProgID("HNetCfg.FwPolicy2", false);
            INetFwPolicy2 FwPolicy = (INetFwPolicy2)Activator.CreateInstance(Policy2);
            INetFwRules rules = FwPolicy.Rules;
            //Delete if exist to avoid deplicated rules
            DeleteRule(name);
            Type RuleType = Type.GetTypeFromProgID("HNetCfg.FWRule");
            INetFwRule rule = (INetFwRule)Activator.CreateInstance(RuleType);

            rule.Name = name;
            rule.Description = Description;
            rule.Protocol = Protocole;// TCP/IP
            rule.LocalPorts = LocalPort;
            rule.RemoteAddresses = RemoteAdresses;
            rule.Action = Action;
            rule.Direction = Direction;
            rule.ApplicationName = ApplicationName;
            rule.Enabled = true;
            //Add Rule
            rules.Add(rule);
        }
        private static INetFwRules GetRules()
        {
            Type Policy2 = Type.GetTypeFromProgID("HNetCfg.FwPolicy2", false);
            INetFwPolicy2 FwPolicy = (INetFwPolicy2)Activator.CreateInstance(Policy2);
            INetFwRules rules = FwPolicy.Rules;
            return rules;
        }
        private static void DeleteRule(String RuleName)
        {
            var rules = GetRules();
            foreach (INetFwRule rule in rules)
            {
                if (rule.Name == RuleName)
                {
                    rules.Remove(rule.Name);
                    Console.WriteLine("rule \"" + rule.Name + "\" has been removed.");
                    return;
                }
            }
            Console.WriteLine("rule \"" + RuleName + "\" does not exist.");
        }
        private static void DeleteAllRules()
        {
            var rules = GetRules();
            int removedCount = 0;
            foreach (INetFwRule rule in rules)
            {
                if (rule.Name.StartsWith("FriedWebsite open port"))
                {
                    rules.Remove(rule.Name);
                    removedCount++;
                }
            }
            Console.WriteLine($"Removed {removedCount} rules out of {rules.Count}.");
        }
        #endregion
        #region Ports
        public void SetPort(int newPort)
        {
            if (Running)
            {
                //cant
            }
            else
            {
                Port = newPort;
            }
        }
        public void OpenFriedPort(int? OverridePort = null)
        {
            int port = Port; // Assuming Port is a property in the Website class
            if (OverridePort != null)
                port = (int)OverridePort;
            OpenFriedPort(port);
        }
        public static void OpenFriedPort(int port)
        {
            try
            {
                string name = $"FriedWebsite open port {port}";
                string desc = $"This rule was generated by FriedWebsite Class, what it does is open the port \"{port}\" \nso you can visit this port from diffrent devices on the same network.\n Made by FriedMonkey.";
                var action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
                var dir = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN;


                AddRule(name, desc, action, dir, port.ToString(), ApplicationName: null);
                Console.WriteLine($"Firewall rule added: {name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while opening the FriedWebsite port:");
                Console.WriteLine(ex.Message);
            }
        }
        public void CloseFriedPort(int? OverridePort = null)
        {
            int port = Port; // Assuming Port is a property in the Website class
            if (OverridePort != null)
                port = (int)OverridePort;
            CloseFriedPort(port);
        }
        public static void CloseFriedPort(int port)
        {
            try
            {
                string name = $"FriedWebsite open port {port}";
                DeleteRule(name);
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while closing the FriedWebsite port:");
                Console.WriteLine(ex.Message);
            }
        }

        public void closeAllFriedPorts() => Website.CloseAllFriedPorts();
        public static void CloseAllFriedPorts()
        {
            DeleteAllRules();
        }
        #endregion

    }
    public static class WebExtensionMethods
    {
        public static byte[] GetBytes(this string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

        public static Task WriteAsync(this Stream stream, string str)
        {
            return stream.WriteAsync(str.GetBytes(), 0, str.Length);
        }
        public static Task WriteAsync(this Stream stream, byte[] bytes)
        {
            return stream.WriteAsync(bytes, 0, bytes.Length);
        }

        public static TValue GetOrNull<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key)
        {
            if (dict.ContainsKey(key)) return dict[key];
            return default(TValue);
        }

        /// <summary>
        ///     A NameValueCollection extension method that converts the @this to a dictionary.
        /// </summary>
        /// <param name="this">The @this to act on.</param>
        /// <returns>@this as an IDictionary&lt;string,object&gt;</returns>
        public static Dictionary<string, object> ToDictionary(this NameValueCollection @this)
        {
            var dict = new Dictionary<string, object>();

            if (@this != null)
            {
                foreach (string key in @this.AllKeys)
                {
                    if (key is not null)
                        dict.Add(key, @this[key]);
                }
            }

            return dict;
        }
        public static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
            {
                return text;
            }
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }
    }
}
