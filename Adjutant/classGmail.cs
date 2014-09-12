using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Xml;
using System.ComponentModel;

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
        Action<int, MailCheckAction> finishedCheckingMail;

        public int MailCount;
        public List<string[]> emails;
        public Exception mailException;


        public Gmail(string username, string password, Action<int, MailCheckAction> finishedCheckingMail)
        {
            this.username = username;
            this.password = password;
            this.finishedCheckingMail = finishedCheckingMail;
        }

        public void ChangeLogin(string username, string password)
        {
            if (username != this.username || password != this.password)
            {
                this.username = username;
                this.password = password;

                Check(MailCheckAction.NoAction);
            }
        }

        public void Check(MailCheckAction action)
        {
            BackgroundWorker mailCheckWorker = new BackgroundWorker();
            mailCheckWorker.DoWork += new DoWorkEventHandler(mailCheckWorker_DoWork);
            mailCheckWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(mailCheckWorker_CompletedEvent);

            mailCheckWorker.RunWorkerAsync(action);
        }

        private void mailCheckWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                HttpWebRequest webReq = (HttpWebRequest)HttpWebRequest.Create("https://mail.google.com/mail/u/0/feed/atom");
                webReq.Credentials = new NetworkCredential(username, password);

                WebResponse response = webReq.GetResponse();

                Stream webStream = response.GetResponseStream();
                XmlReader reader = XmlReader.Create(webStream);

                List<string[]> newEmails = new List<string[]>();
                int newMailCount = -1;

                while (reader.Read())
                {
                    if (!reader.IsStartElement())
                        continue;

                    if (reader.Name == "fullcount")
                    {
                        if (reader.Read())
                            newMailCount = int.Parse(reader.Value);
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

                        newEmails.Add(newEmail);
                    }
                }
                
                reader.Close();
                webStream.Close();
                response.Close();

                e.Result = new Tuple<int, List<string[]>, MailCheckAction>(newMailCount, newEmails, (MailCheckAction)e.Argument);
            }
            catch (Exception exc)
            {
                mailException = exc;
                e.Result = new Tuple<int, List<string[]>, MailCheckAction>(-1, null, (MailCheckAction)e.Argument);
            }
        }

        private void mailCheckWorker_CompletedEvent(object sender, RunWorkerCompletedEventArgs e)
        {
            Tuple<int, List<string[]>, MailCheckAction> result = (Tuple<int, List<string[]>, MailCheckAction>)e.Result;

            MailCount = result.Item1;
            emails = result.Item2;

            finishedCheckingMail(MailCount, result.Item3);
        }
    }
}
