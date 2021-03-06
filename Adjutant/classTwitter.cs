﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Web;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.ComponentModel;

namespace Adjutant
{
    class Twitter
    {
        public struct Tweet
        {
            public string user, actualUsername, text, url; //url of the tweet
            public DateTime created;
            public long id;

            public Tweet(string user, string actualUsername, string text, DateTime created, long id, string urlField, string mediaField)
            {
                int lb, ub;

                //expand urls
                if (urlField != "[]")
                {
                    if (urlField.Length > 4)
                        urlField = urlField.Substring(2, urlField.Length - 4); //remove outermost brackets [{ ... }]

                    foreach (string urlSegment in urlField.Split(new string[] { "},{" }, StringSplitOptions.None))
                        text = text.Replace(getField(urlSegment, "url"), getField(urlSegment, "expanded_url"));
                }

                //parse mediaField
                if (DisplayPictures)
                {
                    string textURL = "";

                    if (mediaField.Contains("\"url\":\""))
                    {
                        lb = mediaField.IndexOf("\"url\":\"") + 7;
                        ub = mediaField.IndexOf('"', lb);

                        textURL = mediaField.Substring(lb, ub - lb);
                    }

                    if (mediaField.Contains("\"media_url\":\""))
                    {
                        lb = mediaField.IndexOf("\"media_url\":\"") + 13;
                        ub = mediaField.IndexOf('"', lb);

                        string imgInfo = "<image=" + mediaField.Substring(lb, ub - lb) + ">";

                        if (textURL != "")
                            text = text.Replace(textURL, imgInfo);
                        else
                            text += " " + imgInfo;
                    }
                }
                
                //decode and unescape tweet text
                text = HttpUtility.HtmlDecode(Regex.Unescape(text));

                //replace instagram links with direct image links
                if (DisplayInstagrams)
                {
                    ub = 0;

                    while (text.IndexOf("instagram.com/p/", ub) != -1)
                    {
                        lb = text.IndexOf("instagram.com/p/");

                        if (lb >= 4 && text.Substring(lb - 4, 4) == "www.")
                            lb -= 4;
                        if (lb >= 7 && text.Substring(lb - 7, 7) == "http://")
                            lb -= 7;

                        ub = text.IndexOf("/p/", lb);
                        ub = text.IndexOf("/", ub + 3);

                        if (ub != -1)
                            text = text.Insert(ub + 1, "media>").Insert(lb, "<image="); //add "/?size=l" for large. Other sizes: m for medium and t for thumbnail (default)
                        else
                            ub = text.IndexOf("/p/", lb); //invalid instagram link, move on
                    }
                }

                //save tweet args
                this.user = user;
                this.actualUsername = actualUsername;
                this.text = text.Replace("\n", Environment.NewLine);
                this.created = created;
                this.id = id;

                url = "https://twitter.com/" + user + "/status/" + id;
            }

            static string getField(string response, string fieldName)
            {
                try
                {
                    string tag = "\"" + fieldName + "\":\"";

                    int lb = response.IndexOf(tag) + tag.Length;
                    int ub = response.IndexOf('"', lb);

                    return response.Substring(lb, ub - lb);
                }
                catch
                {
                    return "";
                }
            }
        }

        class DataNode
        {
            public string name;
            public Dictionary<string, string> fields;
            public List<DataNode> childNodes;

            public DataNode(string name)
            {
                this.name = name;
                fields = new Dictionary<string, string>();
                childNodes = new List<DataNode>();
            }
        }

        const string consumerKey = "bQp3ytw07Ld9bmEBU4RI4w";
        const string consumerSecret = "IXS35cJodk8aUVQO26vTYUb61kYbIV6cznOYBd7k7AI";

        public static bool DisplayPictures, DisplayInstagrams;

        string oauthToken, oauthTokenSecret;
        List<Tweet> tweets;
        int twInd;

        public Exception TwException;


        public Twitter()
        {
            oauthToken = "";
            oauthTokenSecret = "";
            twInd = 0;
            tweets = new List<Tweet>();
        }

        public Twitter(string oauthToken, string oauthSecret, long lastTweet)
        {
            this.oauthToken = oauthToken;
            this.oauthTokenSecret = oauthSecret;
            twInd = 0;
            tweets = new List<Tweet>();
        }

        public Uri GetAuthorizationUri()
        {
            oauthToken = "";
            oauthTokenSecret = "";

            List<string[]> respPars = parseParameters(webRequest("POST", "https://api.twitter.com/oauth/request_token", "oauth_callback=\"oob\""), "&", "=");

            oauthToken = getParameterValue(respPars, "oauth_token");            
            oauthTokenSecret = getParameterValue(respPars, "oauth_token_secret");
            
            return new Uri("https://api.twitter.com/oauth/authenticate?oauth_token=" + oauthToken);
        }

        public bool FinalizeAuthorization(string verifier, out string token, out string secret)
        {
            List<string[]> respPars = parseParameters(webRequest("POST", "https://api.twitter.com/oauth/access_token", "oauth_verifier=\"" + verifier + "\""), "&", "=");

            oauthToken = getParameterValue(respPars, "oauth_token");
            oauthTokenSecret = getParameterValue(respPars, "oauth_token_secret");
            
            token = oauthToken;
            secret = oauthTokenSecret;

            return true;
       }

        public bool VerifyCredentials()
        {
            string resp = webRequest("GET", "https://api.twitter.com/1.1/account/verify_credentials.json", "");
            return resp != "";
        }

        public bool EstablishStreamConnection()
        {
            try
            {
                BackgroundWorker service = new BackgroundWorker();

                service.DoWork += new DoWorkEventHandler(twitterStreamingWorker);
                service.RunWorkerAsync();

                return true;
            }
            catch (Exception exc)
            {
                TwException = exc;
                return false;
            }
        }

        private void twitterStreamingWorker(object sender, DoWorkEventArgs e)
        {
            string method = "GET", baseURL = "https://userstream.twitter.com/1.1/user.json";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(baseURL);

            request.Method = method;
            request.UserAgent = "Adjutant by @Winterstark";
            request.Headers.Add("Authorization", constructAuthHeader(method, baseURL, ""));

            using (var resp = (HttpWebResponse)request.GetResponse())
            {
                Encoding encode = System.Text.Encoding.GetEncoding("utf-8");

                using (var respStream = new StreamReader(resp.GetResponseStream(), encode))
                {
                    try
                    {
                        while (!respStream.EndOfStream)
                        {
                            System.Diagnostics.Debug.WriteLine("Still working - " + DateTime.Now);

                            string line = respStream.ReadLine();

                            if (line.Length > 11 && line.Substring(0, 11) != "{\"friends\":")
                                processNewTweets(line);

                            System.Threading.Thread.Sleep(1000);
                        }
                    }
                    catch
                    {
                        System.Diagnostics.Debug.WriteLine("EXCEPTION!");
                        System.Windows.Forms.MessageBox.Show("EXCEPTION!");
                    }

                    string endMsg = "Stopped tracking new tweets." + Environment.NewLine + DateTime.Now + Environment.NewLine + resp.StatusCode + ": " + resp.StatusDescription;
                    System.Diagnostics.Debug.WriteLine(endMsg);
                    System.Windows.Forms.MessageBox.Show(endMsg);
                }
            }
        }

        int getRateLimit(string resourceFamily, string resource)
        {
            try
            {
                string resp = webRequest("GET", "https://api.twitter.com/1.1/application/rate_limit_status.json", "resources=\"" + resourceFamily + "\"");

                //extract remaining number of uses of resource
                string blockStart = ",\"\\/" + resourceFamily + "\\/" + resource + "\":{\"";
                int lb = resp.IndexOf(blockStart);

                lb += blockStart.Length;

                string block = resp.Substring(lb, resp.IndexOf('}', lb) - lb);

                lb = block.IndexOf("\"limit\":") + 8;
                int ub = block.IndexOf(',', lb);

                return int.Parse(block.Substring(lb, ub - lb));
            }
            catch
            {
                return 0;
            }
        }

        public bool GetNewTweets(long lastTweet)
        {
            if (getRateLimit("statuses", "home_timeline") == 0)
            {
                TwException = new Exception("Twitter API rate limit exceeded.");
                return false;
            }

            //set parameters
            string parameters = "count=\"200\"";

            if (lastTweet != 0)
                parameters += ", since_id=\"" + lastTweet + "\"";

            string resp = webRequest("GET", "https://api.twitter.com/1.1/statuses/home_timeline.json", parameters);

            if (resp != "[]")
            {
                if (resp.Length > 2)
                    resp = resp.Substring(1, resp.Length - 2); //remove outermost pair of brackets [ ... ]

                return processNewTweets(resp); //return success/failure of processNewTweets function
            }
            else
                return true;
        }

        bool processNewTweets(string resp)
        {
            try
            {
                //extract tweets from response
                List<Tweet> newTweets = new List<Tweet>();

                resp = "," + resp; //add a comma at the beginning for string-splitting purposes

                string delimiter = "{\"created_at\":\"";
                string[] tweetBlocks = resp.Split(new string[] { "," + delimiter }, StringSplitOptions.RemoveEmptyEntries);

                //add delimiters back into blocks
                for (int i = 0; i < tweetBlocks.Length; i++)
                    tweetBlocks[i] = delimiter + tweetBlocks[i] + ",";

                //parse blocks into tweets
                foreach (string tweetBlock in tweetBlocks)
                {
                    string block = delimiter + tweetBlock; //return delimiter string to beginning of block

                    DataNode tweetRootNode = constructDataNode(ref block, "root");
                    DataNode userDetails = tweetRootNode.childNodes.Find(node => node.name == "user");

                    //find url field
                    string urlField = tweetRootNode.childNodes.Find(node => node.name == "entities").fields["urls"];

                    //find media field
                    string mediaField = "";
                    DataNode extEntities = tweetRootNode.childNodes.Find(node => node.name == "extended_entities");

                    if (extEntities != null && extEntities.fields.ContainsKey("media"))
                        mediaField = extEntities.fields["media"];

                    //convert created_at to DateTime
                    string created = tweetRootNode.fields["created_at"];
                    created = created.Substring(created.IndexOf(' '));
                    created = created.Substring(created.Length - 4, 4) + created.Substring(0, created.IndexOf(" +"));

                    DateTime time = DateTime.Parse(created);
                    time = TimeZone.CurrentTimeZone.ToLocalTime(time);

                    //check if retweeted tweet's text is complete
                    string text = tweetRootNode.fields["text"];

                    if (text.Contains("\\u2026")) //symbol for "..."
                    {
                        DataNode retweetedStatus = tweetRootNode.childNodes.Find(node => node.name == "retweeted_status");

                        if (retweetedStatus != null) //if not a false alarm
                        {

                            int ub = text.IndexOf("\\u2026");
                            text = text.Replace("\\u2026", "");

                            string fullText = retweetedStatus.fields["text"];

                            //find where the extra text begins
                            string searchSegment;
                            int lenSearchSegment = 0, lb;

                            do
                            {
                                lenSearchSegment = Math.Min(lenSearchSegment + 10, ub);
                                searchSegment = text.Substring(ub - lenSearchSegment, lenSearchSegment);

                                lb = fullText.IndexOf(searchSegment);
                            } while (fullText.IndexOf(searchSegment, lb + 1) != -1); //extend search segment until only 1 match is found in the full text

                            text += fullText.Substring(lb + lenSearchSegment);
                        }
                    }

                    //if (text.ToLower().Contains("cis in game 1"))
                    //{
                    //    int xxxx = 9;
                    //}
                    
                    //add tweet
                    newTweets.Add(new Tweet(userDetails.fields["name"], userDetails.fields["screen_name"], text, time, long.Parse(tweetRootNode.fields["id"]), urlField, mediaField));
                }

                //add new tweets, in reverse order (oldest to newest)
                newTweets.Reverse();
                tweets.AddRange(newTweets);

                return true;
            }
            catch (Exception exc)
            {
                TwException = exc;
                return false;
            }
        }

        DataNode constructDataNode(ref string response, string name)
        {
            response = response.Substring(1); //remove first '{'

            //preparse new DataNode
            DataNode newNode = new DataNode(name);

            while (response[0] != '}')
            {
                if (response[0] == '"')
                {
                    //read next field
                    int ub = response.IndexOf('"', 1);
                    string field = response.Substring(1, ub - 1);

                    if (response[ub + 1] == ':')
                    {
                        if (response[ub + 2] == '{')
                        {
                            //create child node
                            response = response.Substring(ub + 2);
                            newNode.childNodes.Add(constructDataNode(ref response, field));
                        }
                        else
                        {
                            int lb = ub + 2;

                            if (response[lb] == '"')
                            {
                                //find closing quotation mark
                                ub = response.IndexOf('"', lb + 1) + 1;

                                while (ub >= 2 && response[ub - 2] == '\\')
                                    ub = response.IndexOf('"', ub) + 1;

                                ub = getNextDelimiter(response, ub);
                            }
                            else if (response[lb] == '[')
                            {
                                //ignore the complexities and just find the closing bracket and wrap it all up as a string value
                                int bracketCount = 1;

                                for (int i = lb + 1; i < response.Length; i++)
                                    if (response[i] == '[')
                                        bracketCount++;
                                    else if (response[i] == ']')
                                    {
                                        bracketCount--;

                                        if (bracketCount == 0)
                                        {
                                            //found closing bracket
                                            ub = i;
                                            break;
                                        }
                                    }

                                ub = getNextDelimiter(response, ub);
                            }
                            else
                                ub = getNextDelimiter(response, lb);

                            string value = response.Substring(lb, ub - lb);

                            if (value[0] == '"') //remove quotes
                                value = value.Substring(1, value.Length - 2);

                            //add new field
                            newNode.fields.Add(field, value);

                            //remove parsed segment
                            response = response.Substring(ub);

                            if (response[0] == ',') //skip comma
                                response = response.Substring(1);
                        }
                    }
                    else
                        throw new Exception("Response was in an invalid format.");
                }
                else
                    throw new Exception("Response was in an invalid format.");
            }

            response = response.Substring(1); //skip '}'

            if (response.Length >= 1 && response[0] == ',')
                response = response.Substring(1); //and ',' if present

            return newNode;
        }

        int getNextDelimiter(string response, int lb)
        {
            return Misc.GetNextDelimiter(response, lb, ',', '}');
        }

        public bool AnyNewTweets()
        {
            return tweets.Count > 0;
        }

        public bool AnyUnreadTweets()
        {
            return twInd < tweets.Count;
        }

        public int GetNewTweetCount()
        {
            return tweets.Count;
        }

        public long GetLastTweet()
        {
            if (tweets.Count > 0)
                return tweets[tweets.Count - 1].id;
            else
                return 0;
        }

        public Tweet GetNextUnreadTweet()
        {
            return tweets[twInd++];
        }

        public void GoToPreviousTweet()
        {
            if (twInd > 1)
                twInd -= 2;
            else if (twInd > 0)
                twInd--;
        }

        public void RemoveReadTweets()
        {
            for (int i = 0; i < twInd; i++)
                tweets.RemoveAt(0);

            twInd = 0;
        }

        string webRequest(string method, string baseURL, string parameters)
        {
            try
            {
                if (parameters != "")
                {
                    //add parameters to baseURL
                    baseURL += "?";

                    foreach (string par in parameters.Replace("\"", "").Split(new string[] { ", " }, StringSplitOptions.None))
                        if (!par.Contains("oauth")) //oauth parameters do not belong in url query
                            baseURL += Uri.EscapeDataString(par.Replace("=", "%EQUALS%")).Replace("%25EQUALS%25", "=") + "&"; //ignore '=' when url encoding

                    //remove last '&' (or, if no parameters were added, remove '?')
                    baseURL = baseURL.Substring(0, baseURL.Length - 1);
                }

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(baseURL);

                request.Method = method;
                request.UserAgent = "Adjutant by @Winterstark";
                request.Headers.Add("Authorization", constructAuthHeader(method, baseURL, parameters));
                
                //get response
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                Stream respStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(respStream);
                
                string respMsg = reader.ReadToEnd();

                reader.Close();
                respStream.Close();
                
                return respMsg;
            }
            catch (Exception exc)
            {
                this.TwException = exc;
                return "";
            }
        }

        List<string[]> parseParameters(string parameters, string parameterDelimiter, string keyValueDelimiter)
        {
            List<string[]> pairs = new List<string[]>();

            foreach (string p in parameters.Split(new string[] { parameterDelimiter }, StringSplitOptions.None))
            {
                pairs.Add(p.Split(new string[] { keyValueDelimiter }, StringSplitOptions.None));

                //remove second quotation mark
                string tmp = pairs[pairs.Count - 1][1];

                if (tmp[tmp.Length - 1] == '"')
                {
                    tmp = tmp.Substring(0, tmp.Length - 1);
                    pairs[pairs.Count - 1][1] = tmp;
                }
            }

            return pairs;
        }

        string getParameterValue(List<string[]> parameters, string parameterKey)
        {
            return parameters.Find(p => p[0] == parameterKey)[1];
        }

        string constructAuthHeader(string method, string baseURL, string parameters)
        {
            //remove parameters from baseURL
            if (baseURL.Contains('?'))
                baseURL = baseURL.Substring(0, baseURL.IndexOf('?'));

            //add oauth parameters
            if (parameters != "")
                parameters += ", ";

            if (oauthToken != "")
                parameters += "oauth_token=\"" + oauthToken + "\", ";

            parameters += "oauth_consumer_key=\"" + consumerKey + "\", oauth_nonce=\"" + generateNonce() + "\", oauth_signature_method=\"HMAC-SHA1\", oauth_timestamp=\"" + getTimestamp() + "\", oauth_version=\"1.0\"";
            parameters += ", oauth_signature=\"" + constructSignature(method, baseURL, parameters) + "\"";

            //remove non-OAuth parameters
            foreach (string par in parameters.Split(new string[] { ", " }, StringSplitOptions.None))
                if (!par.Contains("oauth"))
                {
                    int lb = parameters.IndexOf(par);
                    parameters = parameters.Remove(lb, par.Length);

                    //cleanup commas
                    if (lb >= 2 && parameters.Substring(lb - 2, 2) == ", ") //check left
                        parameters = parameters.Remove(lb - 2, 2);
                    else if (parameters.Length >= lb + 2 && parameters.Substring(lb, 2) == ", ") //check right
                        parameters = parameters.Remove(lb, 2);
                }

            return "OAuth " + parameters;
        }

        string constructSignature(string method, string baseURL, string parameters)
        {
            string baseString = method + "&" + Uri.EscapeDataString(baseURL) + "&";

            //encode and sort parameters
            List<string[]> pairs = parseParameters(parameters, ", ", "=\"");

            pairs.Sort((a, b) => string.Compare(a[0], b[0]));

            string encParameters = "";
            foreach (string[] p in pairs)
                //if (p[0].Contains("oauth")) //only include oauth parameters
                encParameters += p[0] + "=" + p[1] + "&";

            if (encParameters.Length > 0)
                encParameters = encParameters.Substring(0, encParameters.Length - 1); //remove last '&'

            baseString += Uri.EscapeDataString(encParameters);

            //hash
            HMACSHA1 hasher = new HMACSHA1(Encoding.ASCII.GetBytes(Uri.EscapeDataString(consumerSecret) + "&" + Uri.EscapeDataString(oauthTokenSecret)));
            string x = Uri.EscapeDataString(oauthTokenSecret);
            return Uri.EscapeDataString(Convert.ToBase64String(hasher.ComputeHash(Encoding.ASCII.GetBytes(baseString))));
        }

        string generateNonce()
        {
            //generate base64 string from random bytes
            Random rnd = new Random((int)DateTime.Now.Ticks);
            byte[] bytes = new byte[32];
            rnd.NextBytes(bytes);

            string nonce = Convert.ToBase64String(bytes);

            //strip non-alphanumeric chars
            for (int i = 0; i < nonce.Length; i++)
                if (!char.IsLetterOrDigit(nonce[i]))
                    nonce = nonce.Remove(i--, 1);

            return nonce;
        }

        string getTimestamp()
        {
            return ((int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds)).ToString();
        }
    }
}
