using Client.Model;
using RestSharp;
using RestSharp.Deserializers;
using System;
using System.Collections.Generic;


namespace Client
{
    public class Segment
    {
        public IList<DataPoint> Data { get; set; }
        public string NextUrl { get; set; }

        public Segment(IList<DataPoint> data, string next)
        {
            Data = data;
            NextUrl = next;
        }

        /// For now, build the Segment by passing in a RestResponse.
        /// This should be handled by a deserializer in the future
        public Segment(IRestResponse response)
        {
            /// Deserialize the data
            JsonDeserializer deserializer = new JsonDeserializer();
            List<DataPoint> data = deserializer.Deserialize<List<DataPoint>>(response);
            Data = data;

            /// Get the next link from the Link header
            Parameter header = null;
            foreach(Parameter h in response.Headers)
            {
                if(h.Name.ToLower().Equals("link"))
                {
                    header = h;
                    break;
                }
            }

            Dictionary<string, Dictionary<string, string>> links = new Dictionary<string, Dictionary<string, string>>();
            if(header != null)
            {
                List<Dictionary<string, string>> l = ParseHeaderLinks(header.Value as string);
                foreach(Dictionary<string, string> link in l)
                {
                    string key = link.ContainsKey("rel") ? link["rel"] : link["url"];
                    links.Add(key, link);
                }
            }

            Dictionary<string, string> next = new Dictionary<string, string>();
            string nextUrl = null;
            if(links.TryGetValue("next", out next))
            {
                next.TryGetValue("url", out nextUrl);
            }
            NextUrl = nextUrl;
        }

        public IEnumerator<DataPoint> GetEnumerator()
        {
            foreach(DataPoint dp in Data)
            {
                yield return dp;
            }
        }

        private List<Dictionary<string, string>> ParseHeaderLinks(string header)
        {
            char[] replaceChars = {' ', '\'', '"'};
            char[] replaceUrlChars = {'<', '>', ' ', '\'', '"'};
            List<Dictionary<string, string>> links = new List<Dictionary<string, string>>();
            foreach(string val in header.Split(','))
            {
                string[] items = val.Split(';');
                if(items.Length > 0)
                {
                    string url = items[0];
                    string parameters = items.Length > 1 ? items[1] : "";
                    Dictionary<string, string> link = new Dictionary<string, string>();
                    link.Add("url", url.Trim(replaceUrlChars));
                    foreach(string param in parameters.Split(';'))
                    {
                        string[] keys = param.Split('=');
                        if(keys.Length < 1) break;
                        string key = keys[0];
                        string item = keys[1];
                        link.Add(key.Trim(replaceChars).ToLower(), item.Trim(replaceChars));
                    }
                    links.Add(link);
                }
            }
            return links;
        }
    }
}
