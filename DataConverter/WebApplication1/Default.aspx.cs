using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using LinqToExcel;
using LinqToExcel.Extensions;
using Microsoft.SqlServer.Server;
using Remotion.Data;
using Newtonsoft.Json;

namespace WebApplication1
{
    public partial class _Default : System.Web.UI.Page
    {
        private string jsonstatesHeader =
            "{ \"states\":{\"2005\":{\"IN-TN\": 15.97}, \"2006\":{\"IN-TN\": 58.97}, \"2007\":{\"IN-TN\": 158.97}, \"2008\":{\"IN-TN\": 358.97}, \"2009\":{\"IN-TN\": 758.97}}," +
            "\"metro\":{ \"coords\":[[11.0183, 76.9725,\"\",\"https://www.google.com\"],[9.9197, 78.1194,\"\",\"https://www.google.com\"]," +
            "[10.8050, 78.6856]," +
            "[8.7300,77.7000]," +
            "[11.6500,78.1600]," +
            "[11.3500, 77.7333]," +
            "[11.1075,77.3398]," +
            "[8.8100,78.1400]," +
            "[12.9202,79.1333]],";

        protected void Page_Load(object sender, EventArgs e)
        {
            //Population From File
            var parsedDemographics = ParseExcel(Server.MapPath("~/Sources/Demographics.xlsx"), 0);
            var distinctYear = (from x in parsedDemographics select x[6]).ToList().Distinct();
            StringBuilder jsontext = new StringBuilder();
            jsontext.Append(jsonstatesHeader);
            bool flg = false;
            foreach (var years in distinctYear)
            {
                var names = new List<string>();
                var rankList = new List<string>();
                //var DistinctYear = parsedDemographics[6].ToList().Distinct();
                foreach (var demographic in parsedDemographics)
                {
                    if (!demographic[1].Contains("( 1 )"))
                    {
                        names.Add(demographic[1]);
                        //Get the HealthRank
                        double healthRank = GetHealthRank(demographic[1], Convert.ToInt32((demographic[2])),years);
                        //Get the sanitaryvalue
                        double sanityRank = GetSanitaryRank(demographic[1], Convert.ToInt32((demographic[2])), years);
                        //Get EducationValue
                        double educationRank = GetEducationRank(demographic[1], Convert.ToInt32((demographic[2])),years);
                        //Get RoadsValue

                        double totalRank = ((0.5) * healthRank) + ((0.25) * sanityRank) + ((0.25) * educationRank);
                        rankList.Add(totalRank.ToString());
                    }
                }
                if (!flg)
                {
                    var namesJson = GetJson(names);
                    jsontext.Append("\"names\": ");
                    jsontext.Append(namesJson);
                    jsontext.Append(", \"ranking\":{ ");
                    flg = true;
                }
                jsontext.Append("\"");
                jsontext.Append(years);
                jsontext.Append("\": ");
                jsontext.Append(GetJson(rankList));
                jsontext.Append(",");
            }
            jsontext.Remove(jsontext.Length - 1, 1);
            var text = jsontext.ToString();
            jsontext.Append("}}}");
            //var roadrate = LookupExcel(Server.MapPath("~/Sources/Demographics.xlsx"), 1, "Roads laid within 6 months", "Roads Rate", 12, 4000);
        }

        private List<string[]> ParseCsv(string path)
        {
            var parsedData = new List<string[]>();

            using (var readFile = new StreamReader(path))
            {
                string line;
                while ((line = readFile.ReadLine()) != null)
                {
                    string[] row = line.Split(',');
                    parsedData.Add(row);
                }
            }



            return parsedData;
        }

        private List<string[]> ParseExcel(string path, int sheetno)
        {
            var excelfile = new ExcelQueryFactory(path);
            var parsedata = from x in excelfile.Worksheet(sheetno)
                            select x;
            var list = parsedata.ToList();
            var parsedList = new List<string[]>();
            foreach (var item in list)
            {
                var colvals = new string[item.Count];
                bool notempty = false;
                for (int i = 0; i < item.Count; i++)
                {
                    if (!string.IsNullOrEmpty(item[i]))
                    {
                        colvals[i] = item[i];
                        notempty = true;
                    }
                }
                if (notempty)
                {
                    parsedList.Add(colvals);
                }
            }
            return parsedList;
        }

        private object LookupExcel(string path, int sheetno, string lookupColumn, string selectcolumn, int value, int populationvalue)
        {
            var excelfile = new ExcelQueryFactory(path);
            var selecteddata = from x in excelfile.Worksheet(sheetno)
                               where x["Population Value"].Cast<int>() <= populationvalue
                               select x;
            var selecteditem = selecteddata.ToList().FirstOrDefault(row => row[lookupColumn].Value.Cast<int>() <= value);
            if (selecteditem != null) return selecteditem[selectcolumn].Value;
            else return new object();
        }

        private string GetJson(object list)
        {
            return JsonConvert.SerializeObject(list);
        }

        private double GetSanitaryRank(string corporationName, int populationcount, string year)
        {
            var sanitaryValues = ParseCsv(Server.MapPath("~/Sources/SWM_Vehicles_on_2.1.13.csv"));
            var corporationSanitaryValue = sanitaryValues.FirstOrDefault(row => row[1].ToUpper().Equals(corporationName.ToUpper()) && row[0].ToUpper().Equals(year));
            int totalvehicle = 0;
            for (int i = 2; i < 16; i++)
            {
                int parseout = corporationSanitaryValue != null && (Int32.TryParse(corporationSanitaryValue[i], out parseout))
                    ? Convert.ToInt32(corporationSanitaryValue[i])
                    : 0;
                totalvehicle += parseout;
            }
            int landavailable = corporationSanitaryValue != null && (Int32.TryParse(corporationSanitaryValue[19], out landavailable))
                ? Convert.ToInt32(corporationSanitaryValue[19])
                    : 0;
            var vehiclerate = LookupExcel(Server.MapPath("~/Sources/Demographics.xlsx"), 1, "Vehicles deployed Count", "Vehicle Rate", totalvehicle, populationcount);
            var landrate = LookupExcel(Server.MapPath("~/Sources/Demographics.xlsx"), 1, "Land available (in Sqft)", "Land Rate", landavailable, populationcount);

            return ((double)vehiclerate + (double)landrate) / 2;
        }

        private double GetHealthRank(string corporationName, int populationCount, string year)
        {
            var deathlydiseases = ParseExcel(Server.MapPath("~/Sources/Deaths due to disease.xls"), 2);
            var corporationDiseases = deathlydiseases.FirstOrDefault(row => row[1].ToUpper().Equals(corporationName.ToUpper()) && row[0].ToUpper().Equals(year));
            int totaldeath = 0;
            for (int i = 3; i < corporationDiseases.Length; i = i + 2)
            {
                int parseout = corporationDiseases != null && (Int32.TryParse(corporationDiseases[i], out parseout))
                    ? Convert.ToInt32(corporationDiseases[i])
                    : 0;
                totaldeath += parseout;
            }
            var hospitalsvalues = ParseCsv(Server.MapPath("~/Sources/HealthCenter.csv"));
            var corporationHospitals = hospitalsvalues.FirstOrDefault(row => row[1].ToUpper().Equals(corporationName.ToUpper()));
            int totalhospitals = 0;
            for (int j = 2; j < corporationHospitals.Length; j++)
            {
                int parseout = corporationHospitals != null && (Int32.TryParse(corporationHospitals[j], out parseout))
                    ? Convert.ToInt32(corporationHospitals[j])
                    : 0;
                totalhospitals += parseout;
            }

            var deathrate = LookupExcel(Server.MapPath("~/Sources/Demographics.xlsx"), 1, "Disease Count", "Disease Rate", totaldeath, populationCount);
            var healhtrate = LookupExcel(Server.MapPath("~/Sources/Demographics.xlsx"), 1, "Health Center", "Health Rate", totalhospitals, populationCount);

            return ((double)deathrate + (double)healhtrate) / 2;
        }

        private double GetEducationRank(string corporationName, int populationCount,string year)
        {
            var schoolsdetails = ParseExcel(Server.MapPath("~/Sources/schools.xls"), 0);
            //var corporationSchools = schoolsdetails.FirstOrDefault(row => row[0].ToUpper().Equals(corporationName.ToUpper()) && row[0].ToUpper().Equals(year));
            var corporationSchools = schoolsdetails.FirstOrDefault(row => row[0].ToUpper().Equals(corporationName.ToUpper()));
            int totalschool = 0;
            for (int i = 1; i < corporationSchools.Length; i++)
            {
                int parseout = corporationSchools != null && (Int32.TryParse(corporationSchools[i], out parseout))
                    ? Convert.ToInt32(corporationSchools[i])
                    : 0;
                totalschool += parseout;
            }

            var educationrate = LookupExcel(Server.MapPath("~/Sources/Demographics.xlsx"), 1, "Number of Schools", "Education Rate", totalschool, populationCount);
            return (double)educationrate;
        }
    }
}
