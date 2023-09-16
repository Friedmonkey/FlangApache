using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;

namespace FlangWebsite
{
    public class WebsiteContext
    {
        public string Page { get; set; }
        public string IpAddress { get; set; }
        public WebsiteRequest Request { get; set; }
    }
    public class WebsiteRequest
    {
        public string HttpMethod { get; set; } = string.Empty;
        public Uri Url { get; set; }
        public string RawUrl { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string ContentRaw { get; set; } = string.Empty;
        public Dictionary<string,object> ContentDict { get; set; } = new Dictionary<string,object>(); 
        public CookieCollection Cookies { get; set; }
        public Stream InputStream { get; set; }
        public NameValueCollection Headers { get; set; }
        public NameValueCollection QueryString { get; set; }
        public bool KeepAlive { get; set; }
        public bool HasEntityBody { get; set; }

    }
}
