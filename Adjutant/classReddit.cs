using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Web;
using System.Threading.Tasks;

namespace Adjutant
{
    class Reddit
    {
        public struct Post
        {
            public string title, url, permalink, user, subreddit, image;
            public int score, nComments;
            public DateTime created;

            public Post(string title, string url, string permalink, string user, string subreddit, string score, string nComments, string created, string thumbnail)
            {
                this.title = title;
                this.url = url;
                this.permalink = "http://www.reddit.com" + permalink;
                this.user = user;
                this.subreddit = "/r/" + subreddit;
                this.score = int.Parse(score);
                this.nComments = int.Parse(nComments);
                this.created = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(double.Parse(created.Replace(".0", ""))).ToLocalTime();

                if (url.Contains("imgur") && !url.Contains("imgur.com/a/"))
                {
                    if (url[url.Length - 4] != '.')
                    {
                        //find image extension
                        if (!url.Contains("gallery"))
                            url = url.Replace("imgur.com/", "imgur.com/gallery/");
                        url += ".xml";

                        string xmlFile = new WebClient().DownloadString(url);

                        if (xmlFile.Contains("<ext>"))
                        {
                            int lb = xmlFile.IndexOf("<ext>") + 5;
                            int ub = xmlFile.IndexOf("</ext>", lb);
                            string ext = xmlFile.Substring(lb, ub - lb);

                            url = url.Replace("/gallery/", "/").Replace(".xml", ext);
                        }
                        else
                        {
                            //no extension data present in xml file
                            url = this.url; //reset url
                            image = "<image=" + thumbnail + ">"; //use thumbnail
                            return;
                        }
                    }
                    
                    if (!url.Contains("i."))
                        url = url.Replace("imgur.com", "i.imgur.com");

                    url = url.Insert(url.Length - 4, "l"); //download large version

                    image = "<image=" + url + ">";
                }
                else
                    image = "<image=" + thumbnail + ">";
            }
        }

        List<Post> posts;
        string prevSubreddit;
        int postInd;

        public Exception ReddException;


        public Reddit()
        {
            posts = new List<Post>();
            postInd = 0;
        }

        public Post GetNextPost()
        {
            return posts[postInd++];
        }

        public void GoToPreviousPost()
        {
            if (postInd > 1)
                postInd -= 2;
            else if (postInd > 0)
                postInd--;
        }

        public void PreparePosts(string subreddit)
        {
            string resp = webRequest("GET", "http://www.reddit.com/r/" + subreddit + "/hot.json", "");
            int lb = resp.IndexOf("[{") + 2;
            int ub = resp.IndexOf("}]", lb);
            resp = resp.Substring(lb, ub - lb);

            posts = new List<Post>();

            foreach (string post in resp.Split(new string[] { "}, {" }, StringSplitOptions.RemoveEmptyEntries))
            {
                //remove media substring
                string actualPost = "";

                lb = post.IndexOf("\"media\": {") + 10;
                ub = -1;

                if (lb != 9)
                    ub = post.IndexOf("}", lb);

                if (ub != -1)
                    actualPost = post.Remove(lb, ub - lb);
                else
                    actualPost = post;

                //add post
                posts.Add(new Post(
                    getValue(actualPost, "title"),
                    getValue(actualPost, "url"),
                    getValue(actualPost, "permalink"),
                    getValue(actualPost, "author"),
                    getValue(actualPost, "subreddit"),
                    getValue(actualPost, "score"),
                    getValue(actualPost, "num_comments"),
                    getValue(actualPost, "created_utc"),
                    getValue(actualPost, "thumbnail")));
            }
            
            prevSubreddit = subreddit;
        }

        string webRequest(string method, string baseURL, string parameters)
        {
            try
            {
                //if (parameters != "")
                //{
                //    //add parameters to baseURL
                //    baseURL += "?";

                //    foreach (string par in parameters.Replace("\"", "").Split(new string[] { ", " }, StringSplitOptions.None))
                //        if (!par.Contains("oauth")) //oauth parameters do not belong in url query
                //            baseURL += Uri.EscapeDataString(par.Replace("=", "%EQUALS%")).Replace("%25EQUALS%25", "=") + "&"; //ignore '=' when url encoding

                //    //remove last '&' (or, if no parameters were added, remove '?')
                //    baseURL = baseURL.Substring(0, baseURL.Length - 1);
                //}

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(baseURL);

                request.Method = method;
                request.UserAgent = "Adjutant by @Winterstark";
                request.ContentLength = 0;
                //request.Headers.Add("Authorization", constructAuthHeader(method, baseURL, parameters));

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
                this.ReddException = exc;
                return "";
            }
        }

        protected string getValue(string post, string value)
        {
            if (post == "")
                return "";

            int lb = post.IndexOf("\"" + value + "\": ") + value.Length + 4, ub;
            bool quotes = post[lb] == '"';

            if (quotes)
            {
                lb++;

                ub = post.IndexOf('"', lb);
                while (post[ub - 1] == '\\')
                    ub = post.IndexOf('"', ub + 1);
            }
            else
                ub = post.IndexOf(',', lb);

            return post.Substring(lb, ub - lb);
        }
    }
}
