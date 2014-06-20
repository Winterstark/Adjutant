using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Web;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Adjutant
{
    class Twitter
    {
        public struct Tweet
        {
            public string user, actualUsername, text, url; //url of the tweet
            public DateTime created;
            public long id;
            public List<string> urls; //urls in tweet text

            public Tweet(string user, string actualUsername, string text, DateTime created, long id, string urlField)
            {
                this.user = user;
                this.actualUsername = actualUsername;
                this.text = HttpUtility.HtmlDecode(Regex.Unescape(text)).Replace("\n", Environment.NewLine);
                this.created = created;
                this.id = id;

                url = "https://twitter.com/" + user + "/status/" + id;

                //parse urlField
                urls = new List<string>();

                if (urlField != "[]")
                {
                    if (urlField.Length > 4)
                        urlField = urlField.Substring(2, urlField.Length - 4); //remove outermost brackets [{ ... }]

                    foreach (string urlSegment in urlField.Split(new string[] { "},{" }, StringSplitOptions.None))
                    {
                        int lb = urlSegment.IndexOf("\"expanded_url\":\"") + 16;
                        int ub = urlSegment.IndexOf("\"", lb);

                        urls.Add(Regex.Unescape(urlSegment.Substring(lb, ub - lb)));
                    }
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
   
            List<Tweet> newTweets = new List<Tweet>();

            //set parameters
            //string parameters = "count=\"200\"";

            //if (lastTweet != 0)
            //    parameters += ", since_id=\"" + lastTweet + "\"";

            string parameters;
            if (lastTweet != 0)
                parameters = "since_id=\"" + lastTweet + "\"";
            else
                parameters = "count=\"200\"";

            string resp = webRequest("GET", "https://api.twitter.com/1.1/statuses/home_timeline.json", parameters);

            if (resp != "[]")
            {
                //extract tweets from response
                if (resp.Length > 2)
                    resp = resp.Substring(1, resp.Length - 2); //remove outermost pair of brackets [ ... ]

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
                    string urls = tweetRootNode.childNodes.Find(node => node.name == "entities").fields["urls"];

                    //convert created_at to DateTime
                    string created = tweetRootNode.fields["created_at"];
                    created = created.Substring(created.IndexOf(' '));
                    created = created.Substring(created.Length - 4, 4) + created.Substring(0, created.IndexOf(" +"));

                    DateTime time = DateTime.Parse(created);
                    time = TimeZone.CurrentTimeZone.ToLocalTime(time);

                    //add tweet
                    newTweets.Add(new Tweet(userDetails.fields["name"], userDetails.fields["screen_name"], tweetRootNode.fields["text"], time, long.Parse(tweetRootNode.fields["id"]), urls));
                }

                //add new tweets, in reverse order (oldest to newest)
                newTweets.Reverse();
                tweets.AddRange(newTweets);
            }

            return true;
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
            int commaInd = response.IndexOf(',', lb);
            int bracketInd = response.IndexOf('}', lb);

            if (commaInd == -1)
                return bracketInd;
            else if (bracketInd == -1)
                return commaInd;
            else
                return Math.Min(commaInd, bracketInd);
        }

        //public bool Authenticate(string token, string secret)
        //{
        //    try
        //    {
        //        twitter.AuthenticateWith(token, secret);

        //        if (twitter.VerifyCredentials(new VerifyCredentialsOptions()) == null)
        //        {
        //            //authentication failed
        //            twitter = null;
        //            return false;
        //        }
        //        else
        //        {
        //            twitter.StreamUser(NewTweet);

        //            var options = new ListTweetsOnHomeTimelineOptions();

        //            //if (lastTweet != -1)
        //            //    options.SinceId = lastTweet;
        //            //options.Count = 1000;

        //            IEnumerable<TwitterStatus> tweetList = twitter.ListTweetsOnHomeTimeline(options);

        //            if (tweetList != null)
        //                foreach (var tweet in tweetList.Reverse<TwitterStatus>())
        //                    tweets.Add(new Tweet(tweet.User.Name, tweet.User.ScreenName, Regex.Unescape(WebUtility.HtmlDecode(tweet.Text)), tweet.CreatedDate, tweet.Id));

        //            return true;
        //        }
        //    }
        //    catch (Exception exc)
        //    {
        //        TwException = exc;
        //        return false;
        //    }
        //}

        //public bool Init()
        //{
        //    return false;
        //}

        public bool AnyNewTweets()
        {
            return tweets.Count > 0;
        }

        public bool AnyUnreadTweets()
        {
            return twInd < tweets.Count;
        }

        public string GetNewTweetCount()
        {
            switch (tweets.Count)
            {
                case 0:
                    return "No new tweets.";
                case 1:
                    return "1 new tweet.";
                default:
                    return tweets.Count + " new tweets.";
            }
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
            if (twInd > 0)
                twInd--;
        }

        public void RemoveReadTweets()
        {
            for (int i = 0; i < twInd; i++)
                tweets.RemoveAt(0);

            twInd = 0;
        }

        //private void NewTweet(TwitterStreamArtifact streamEvent, TwitterResponse response)
        //{
        //    if (!response.Response.Contains("text"))
        //        return;

        //    long id = long.Parse(getField(response.Response, "id_str"));

        //    string created = getField(response.Response, "created_at");
        //    created = created.Substring(created.IndexOf(' '));
        //    created = created.Substring(created.Length - 4, 4) + created.Substring(0, created.IndexOf(" +"));

        //    DateTime time = DateTime.Parse(created);
        //    time = TimeZone.CurrentTimeZone.ToLocalTime(time);

        //    string user = getField(response.Response, "name");
        //    string actualUsername = getField(response.Response, "screen_name");
        //    string tweet = Regex.Unescape(WebUtility.HtmlDecode(getField(response.Response, "text")));

        //    tweets.Add(new Tweet(user, actualUsername, tweet, time, id));
        //}

        //private string getField(string response, string key)
        //{
        //    int lb = response.IndexOf("\"" + key + "\":\"") + key.Length + 4;
        //    int ub = response.IndexOf("\",\"", lb);

        //    return response.Substring(lb, ub - lb);
        //}

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
                request.Headers.Add("Authorization", constructAuthHeader(method, baseURL, parameters)); //authorization
                
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
