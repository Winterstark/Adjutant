using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace Adjutant
{
    class Weather
    {
        const string WEATHERMAP_API_KEY = "c0a67a18f9d49ac012adf9f1e193bedf";


        public struct Report
        {
            public string[] descs, icons;
            public string timestamp, windDesc, rainPeriod;
            public double temp, tempMin, tempMax, wind, rain;

            public Report(string time, string descList, string iconList, string temp, string tempMin, string tempMax, string wind, string rain, string rainPeriod, bool hourly)
            {
                if (time == "")
                    timestamp = "Now";
                else
                {
                    DateTime dt = Misc.ConvertFromUnixTime(time);

                    if (!hourly)
                    {
                        if (dt.Date == DateTime.Now.Date)
                            timestamp = "Today";
                        else if (dt.Date == DateTime.Now.AddDays(1).Date)
                            timestamp = "Tomorrow";
                        else if (dt.Date < DateTime.Now.AddDays(7).Date)
                            timestamp = dt.DayOfWeek.ToString();
                        else
                            timestamp = dt.DayOfWeek.ToString() + " " + dt.ToShortDateString();
                    }
                    else
                    {
                        if (dt.Date == DateTime.Now.Date)
                            timestamp = dt.ToShortTimeString();
                        else if (dt.Date == DateTime.Now.AddDays(1).Date)
                            timestamp = "Tomorrow " + dt.ToShortTimeString();
                        else
                            timestamp = dt.ToShortDateString() + " " + dt.ToShortTimeString();
                    }
                }

                this.descs = descList.ToLower().Split('&');
                this.icons = iconList.Split('&');

                if (temp != "")
                    this.temp = double.Parse(temp.Replace('.', ','));
                else
                    this.temp = -1;
                if (tempMin != "")
                    this.tempMin = double.Parse(tempMin.Replace('.', ','));
                else
                    this.tempMin = -1;
                if (tempMax != "")
                    this.tempMax = double.Parse(tempMax.Replace('.', ','));
                else
                    this.tempMax = -1;

                if (wind != "")
                    this.wind = double.Parse(wind.Replace('.', ','));
                else
                    this.wind = -1;

                if (rain != "")
                    this.rain = double.Parse(rain.Replace('.', ','));
                else
                    this.rain = -1;

                this.rainPeriod = rainPeriod;

                //set wind description
                double kph = this.wind * 3.6;

                if (kph < 1)
                    windDesc = "calm";
                else if (kph < 6)
                    windDesc = "light air";
                else if (kph < 12)
                    windDesc = "light breeze";
                else if (kph < 20)
                    windDesc = "gentle breeze";
                else if (kph < 29)
                    windDesc = "moderate breeze";
                else if (kph < 39)
                    windDesc = "fresh breeze";
                else if (kph < 50)
                    windDesc = "strong breeze";
                else if (kph < 62)
                    windDesc = "near gale";
                else if (kph < 75)
                    windDesc = "gale";
                else if (kph < 89)
                    windDesc = "strong gale";
                else if (kph < 103)
                    windDesc = "storm";
                else if (kph < 118)
                    windDesc = "violent storm";
                else
                    windDesc = "hurricane";
            }
        }


        public static Report GetCurrentData(string location, string language, bool metric)
        {
            string url = constructWeathermapURL("http://api.openweathermap.org/data/2.5/weather?q=", location, language, metric, 0);
            string resp = new WebClient().DownloadString(url);
            return buildReport(resp, false);
        }

        public static Report[] GetForecast(string location, string language, bool metric, int nDays, bool hourly)
        {
            if (hourly)
                nDays = Math.Min(nDays, 5);
            else
                nDays = Math.Min(nDays, 16);

            string url;
            if (!hourly)
                url = constructWeathermapURL("http://api.openweathermap.org/data/2.5/forecast/daily?q=", location, language, metric, nDays);
            else
                url = constructWeathermapURL("http://api.openweathermap.org/data/2.5/forecast?q=", location, language, metric, 0);
            
            string resp = new WebClient().DownloadString(url);
            resp = resp.Substring(resp.IndexOf("\"weather\":[{"));

            string[] segments;
            if (!hourly)
                segments = resp.Split(new string[] { "\"weather\":[{" }, StringSplitOptions.RemoveEmptyEntries);
            else
                segments = resp.Split(new string[] { "},\"weather\":[{" }, StringSplitOptions.RemoveEmptyEntries);

            int n = segments.Length - 1; //first segment is a header
            Report[] reports = new Report[n];

            for (int i = 0; i < n; i++)
                reports[i] = buildReport(segments[i], hourly);

            return reports;
        }
        
        public static string GetWebcamImage(string webcamsTravelURL)
        {
            string page = new WebClient().DownloadString(webcamsTravelURL);

            int lb = page.IndexOf("http://images.webcams.travel/thumbnail/");
            if (lb == -1)
                return "";
            else
            {
                int ub = page.IndexOf('"', lb);
                if (ub == -1)
                    return "";
                else
                    return page.Substring(lb, ub - lb).Replace("/thumbnail/", "/webcam/");
            }
        }

        static string constructWeathermapURL(string baseURL, string location, string language, bool metric, int nDays)
        {
            string units;
            if (metric)
                units = "&units=metric";
            else
                units = "&units=imperial";

            language = "&lang=" + language.Substring(language.IndexOf('/') + 1);

            return baseURL + location + (nDays != 0 ? "&cnt=" + nDays : "") + language + units + "&APPID=" + WEATHERMAP_API_KEY;
        }

        static Report buildReport(string resp, bool hourly)
        {
            //extract rain values
            string rainValues = getValue(resp, "rain"), rain = "", rainPeriod = "";
            if (rainValues != "" && rainValues.Contains(':'))
            {
                string[] rainPair = rainValues.Split(':');
                rainPeriod = rainPair[0].Replace("\"", "");
                rain = rainPair[1];
            }

            //extract temperature values
            string temp = getValue(resp, "temp"), tempMin, tempMax;

            if (temp.Contains('{'))
            {
                tempMin = getValue(temp, "min");
                tempMax = getValue(temp, "max");
                temp = "";
            }
            else
            {
                tempMin = "";
                tempMax = "";
            }

            //build report
            return new Report(
                getValue(resp, "dt"),
                getValue(resp, "description"),
                getValue(resp, "icon"),
                temp,
                tempMin,
                tempMax,
                getValue(resp, "speed"),
                rain,
                rainPeriod,
                hourly);
        }

        static string getValue(string post, string value)
        {
            if (post == "")
                return "";

            string values = "";
            int ub = 0;

            while (true)
            {
                int lb = post.IndexOf("\"" + value + "\":", ub) + value.Length + 3;
                if (lb == value.Length + 3 - 1)
                    break;

                if (post[lb] == '"')
                {
                    lb++;

                    ub = post.IndexOf('"', lb);
                    if (ub == -1)
                        break;

                    while (post[ub - 1] == '\\')
                        ub = post.IndexOf('"', ub + 1);
                }
                else if (post[lb] == '{')
                {
                    ub = post.IndexOf('}', lb);
                    if (ub == -1)
                        break;

                    while (post[ub - 1] == '\\')
                        ub = post.IndexOf('"', ub + 1);
                }
                else
                {
                    ub = post.IndexOf(',', lb);
                    if (ub == -1)
                        break;
                }

                values += (values != "" ? "&" : "") + post.Substring(lb, ub - lb);
            }

            return values;
        }
    }
}
