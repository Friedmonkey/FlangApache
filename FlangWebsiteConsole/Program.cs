using FriedLang;
using FriedLanguage.BuiltinType;
using FriedLanguage;
using System;
using System.Collections.Generic;
using System.Linq;
using FriedLang.NativeLibraries;
using System.IO;
using Newtonsoft.Json;
using static System.Net.Mime.MediaTypeNames;
using System.Reflection;

namespace FlangWebsiteConsole
{
    public class webIO : IO
    {
        //public override List<FlangMethod> InjectMethods()
        //{
        //    List<FlangMethod> methods = new()
        //    {
        //        new FlangMethod("print", WebPrint, "string message"),
        //    };

        //    return methods;
        //}
        public override void Intercept()
        {
            InterRemoveMethod("read");
            InterReplaceMethod("print",WebPrint);
        }
        public static FValue WebPrint(Scope scope, List<FValue> arguments)
        {
            FValue val = arguments.FirstOrDefault();

            FValue Text = scope.Get("TEXT");
            FValue Pos = scope.Get("POSITION");
            if (Text is FDictionary Dict)
            {
                if (Pos is FInt Position)
                {
                    FValue ret = Dict.Idx(Position); //if it already exists get that otherwise return fnull
                    if (ret is not FNull)
                    {
                        ret = Dict.SetIndex(Position, new FString(ret.SpagToCsString() + val.SpagToCsString())); //concatenate old and new
                    }
                    else
                    {
                        ret = Dict.SetIndex(Position, val); //if it doest exist then make it exist
                    }
                    scope.SetAdmin("TEXT", Dict); //update it
                    return ret;
                }
            }
            return FValue.Null;
        }
        public static FValue PrintBlue(Scope scope, List<FValue> arguments)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(arguments.First().SpagToCsString());
            Console.ResetColor();
            return arguments.First();
        }
    }
    internal class Program
    {
        static void Main(string[] args)
        {
#if DEBUG
            args = new[]
            {
"--QzoveGFtcHAvaHRkb2NzL3dlYmxlc3Nlbi9sZWVyamFhcjMvZmxhbmdBcGkvaW5kZXg0LmZsYW5nP2NzRGVidWc9dHJ1ZQ==",
"--aW5kZXg0LmZsYW5nP2NzRGVidWc9dHJ1ZQ==",
"--L3dlYmxlc3Nlbi9sZWVyamFhcjMvZmxhbmdBcGkvaW5kZXg0LmZsYW5nP2NzRGVidWc9dHJ1ZQ==",
"--",
"--eyJjc0RlYnVnIjoidHJ1ZSJ9",
"--W10=",
            };
#endif


            try
            {
                //Send("executed<br>");
                if (args.Length < 6)
                {
                    string text = "Not enough arguments, expected 6 got " + args.Length;
                    //Send(text);
                    text += "<br>";
                    text += "<br>";
                    text += " - ";
                    text += args[0].Substring(2);
                    text += "<br>";
                    foreach (var item in args)
                    {
                        text += " - ";
                        text += Base64Decode(item.Substring(2));
                        text += "<br>";
                    }


                    Send(GenerateError("Argument error", text));
                    return;
                }
                string filePath = Base64Decode(args[0].Substring(2));

                filePath = filePath.Split('?').First();

                string page = Base64Decode(args[1].Substring(2));
                string url = Base64Decode(args[2].Substring(2));
                string content = Base64Decode(args[3].Substring(2));
                string GETstr = args[4].Substring(2);
                string POSTstr = args[5].Substring(2);


                var GET = ToDictionary(GETstr);
                var POST = ToDictionary(POSTstr);

                if (!File.Exists(filePath))
                {
                    Send(GenerateError("File not found", $"{filePath} not found"));
                    return;
                }
                string code = File.ReadAllText(filePath);
                string output = ParseFlang(code, filePath, page, url, content, GET, POST);
                Send(output);
            }
            catch (Exception e) 
            {
                Send(GenerateError("Something went wrong.",e.Message));
            }
        }
        private static string ParseFlang(string input, string filePath, string page,string url,string contentRaw, Dictionary<string, object> GET, Dictionary<string, object> POST)
        {
            FlangHTMLParser parser = new FlangHTMLParser();
            (var FinalText, var FinalCode) = parser.Parse(input);

            //Flang.ImportNative
            FLang Flang = new FLang();

            Flang.ImportNative<Lang>("lang");
            Flang.ImportNative<webIO>("io");
            Flang.AddVariable("POSITION", 0);
            Flang.AddVariable("PAGE", page);
            Flang.AddVariable("URL", url);
            Flang.AddVariable("CONTENT", contentRaw);
            Flang.AddDictionary<string, object>("GET", GET);
            Flang.AddDictionary<string, object>("POST",POST);
            Flang.AddDictionary<int, string>("TEXT", new Dictionary<int, string>());

            string Code = $"""
            import native io;
            import native lang;
            {FinalCode}
            return TEXT;
""";

            //file found, set the working directory for any code that might use it
            Directory.SetCurrentDirectory(Path.GetDirectoryName(filePath));
            object output = Flang.RunCode(Code, false);
            List<(object, object)> outputDict = FLang.ListFromFriedDictionary(output);

            if (outputDict is null)
            {
                if (FLang.FromFriedVar((FValue)output) is string str && str.StartsWith("$clientEvent$"))
                {
                    FinalText = str.Substring("$clientEvent$".Length);
                    return FinalText;
                }
                return GenerateError("An Error occured",Flang.LastError);
            }
            else
            {
                int backtrack = 0;
                for (int i = 0; i < outputDict.Count(); i++)
                {
                    var (key, value) = outputDict.ElementAt(i);
                    if (key is not int index)
                        continue;
                    FinalText = FinalText.Insert(index + backtrack, value.ToString());
                    backtrack += value.ToString().Length;
                }
                return FinalText;
            }

        }
        public static Dictionary<string, object> ToDictionary(string base64)
        {
            var dict = new Dictionary<string, object>();

            var jsonDict = Base64Decode(base64);
            try
            {
                dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonDict);
            }
            catch { }

            return dict;
        }
        public static string Base64Decode(string base64) 
        {
            //Send($"<br>Decoding:{base64};<br>");
            byte[] data = Convert.FromBase64String(base64);
            string decodedString = System.Text.Encoding.UTF8.GetString(data);
            return decodedString;
        }
        public static void Send(object obj)
        { 
            Console.WriteLine(obj);
        }
        public static string GenerateError(string errorTitle, string errorDescription, string script = "", string css = "")
        {
            string text = string.Empty;
            text += $"<html>\n";
            text += $"	<head>\n";
            text += $"		<title>{errorTitle}</title>\n";
            text += $"		<script>{script}</script>\n";
            text += $"		<style>{css}</style>\n";
            text += $"	</head>\n";
            text += $"	<body>\n";
            text += $"		<h1>{errorTitle}</h1>\n";
            text += $"		<h4>{errorDescription}</h4>\n";
            text += $"		<hr>\n";
            text += $"		<address>FriedWebHost {GetAppVersion()} on {Environment.OSVersion.Platform} {Environment.OSVersion.Version}</address>\n";
            text += $"	</body>\n";
            text += $"</html>";
            return text;
        }
        private static string GetAppVersion()
        {
            return "0.0.0.0";
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion;
        }
    }
}
