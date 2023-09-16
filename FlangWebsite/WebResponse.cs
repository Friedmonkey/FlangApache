using System;
using System.IO;
using System.Text;

namespace FlangWebsite
{
    public class WebResponse
    {
        public WebResponse() { }
        public WebResponse(string content) 
        {
            this.ContentAsString = content;
        }
        public WebResponse(byte[] rawBytes) 
        {
            this.RawBytes = rawBytes;
        }
        public string? ContentAsString
        {
            get
            {
                return Encoding.UTF8.GetString(RawBytes);
            }
            set
            {
                RawBytes = Encoding.UTF8.GetBytes(value);
            }
        }
        public byte[]? RawBytes { get; set; } = null;
        public static WebResponse FromFile(string path)
        {
            return new WebResponse(File.ReadAllBytes(path));
        }
        #region textGeneraters
        public static WebResponse FromGenerateError(string errorTitle, string errorDescription, string script = "", string css = "")
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
            return new WebResponse(text);
        }

        public static WebResponse FromGeneratePage(string Title, string Content, string script = "", string css = "")
        {
            string text = string.Empty;
            text += $"<html>\n";
            text += $"	<head>\n";
            text += $"		<title>{Title}</title>\n";
            text += $"		<script>{script}</script>\n";
            text += $"		<style>{css}</style>\n";
            text += $"	</head>\n";
            text += $"	<body>\n";
            text += $"		{Content}\n";
            text += $"	</body>\n";
            text += $"</html>";
            return new WebResponse(text);
        }
        private static string GetAppVersion()
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion;
        }
        #endregion
    }
}
