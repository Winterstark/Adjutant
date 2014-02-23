using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Xml;

namespace Adjutant
{
    class Gmail
    {
        public readonly int M_TITLE = 0;
        public readonly int M_SUMMARY = 1;
        public readonly int M_LINK = 2;
        public readonly int M_DATE = 3;
        public readonly int M_SENDER = 4;

        string username, password;

        public int MailCount;
        public List<string[]> emails;


        public Gmail(string username, string password)
        {
            this.username=username;
            this.password=password;
        }

        public void ChangeLogin(string username, string password)
        {
            if (username != this.username || password != this.password)
            {
                this.username = username;
                this.password = password;

                Check();
            }
        }

        public int Check()
        {
            try
            {
                HttpWebRequest webReq = (HttpWebRequest)HttpWebRequest.Create("https://mail.google.com/mail/u/0/feed/atom");
                webReq.Credentials = new NetworkCredential(username, password);

                WebResponse response = webReq.GetResponse();

                Stream webStream = response.GetResponseStream();
                XmlReader reader = XmlReader.Create(webStream);

                emails = new List<string[]>();

                while (reader.Read())
                {
                    if (!reader.IsStartElement())
                        continue;

                    if (reader.Name == "fullcount")
                    {
                        if (reader.Read())
                            MailCount = int.Parse(reader.Value);
                    }
                    else if (reader.Name == "entry")
                    {
                        string[] newEmail = new string[5];
                        bool readEntry = false;

                        reader.Read();

                        while (!readEntry)
                        {
                            if (!reader.IsStartElement())
                            {
                                reader.Read();
                                continue;
                            }

                            switch (reader.Name)
                            {
                                case "title":
                                    if (reader.Read())
                                        newEmail[M_TITLE] = reader.Value;
                                    break;
                                case "summary":
                                    if (reader.Read())
                                        newEmail[M_SUMMARY] = reader.Value;
                                    break;
                                case "link":
                                    newEmail[M_LINK] = reader.GetAttribute("href");
                                    break;
                                case "issued":
                                    if (reader.Read())
                                    {
                                        newEmail[M_DATE] = reader.Value;

                                        //parse and convert from UTC to local time
                                        DateTime issued = DateTime.Parse(newEmail[M_DATE].Replace("T", " ").Replace("Z", "")).ToLocalTime();

                                        if (issued.Date == DateTime.Now.Date)
                                            newEmail[M_DATE] = issued.ToShortTimeString();
                                        else if (issued.Date == DateTime.Now.AddDays(-1).Date)
                                            newEmail[M_DATE] = "Yesterday " + issued.ToShortTimeString();
                                        else
                                            newEmail[M_DATE] = issued.ToShortDateString();
                                    }
                                    break;
                                case "name":
                                    if (reader.Read())
                                    {
                                        newEmail[M_SENDER] = reader.Value;
                                        readEntry = true;
                                    }
                                    break;
                            }

                            reader.Read();
                        }

                        emails.Add(newEmail);
                    }
                }

                reader.Close();
                webStream.Close();
                response.Close();

                return MailCount;
            }
            catch (Exception e)
            {
                return -1;
            }
        }
    }
}
