using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Reflection;

namespace TradeLib
{
    /// <summary>
    /// Utility class holding commonly used properties for TradeLinkSuite
    /// </summary>
    public class Util
    {
        public static string TLBaseDir { get { return @"c:\program files\tradelink\"; } }
        public static string TLProgramDir { get { return TLBaseDir + "TradeLinkSuite\\"; } }
        public static string TLTickDir { get { return TLBaseDir + "TickData\\"; } }


        public static TickFileInfo ParseFile(string filepath)
        {
            TickFileInfo tfi;
            string fn = System.IO.Path.GetFileNameWithoutExtension(filepath);
            string ext = System.IO.Path.GetExtension(filepath).Replace(".", "");
            string date = Regex.Match(fn, "[0-9]+$").Value;
            try
            {
                tfi.type = (TickFileType)Enum.Parse(typeof(TickFileType), ext.ToUpper());
            }
            catch (Exception) { tfi.type = TickFileType.Invalid; }
            tfi.date = ToDateTime(Convert.ToInt32(date));
            tfi.symbol = Regex.Match(fn, "^[A-Z]+").Value;
            return tfi;
        }
        public static DateTime ToDateTime(int TradeLinkDate)
        {
            if (TradeLinkDate < 10000) throw new Exception("Not a date, or invalid date provided");
            return ToDateTime(TradeLinkDate, 0, 0);
        }

        public static DateTime ToDateTime(int TradeLinkTime, int TradeLinkSec)
        {
            return ToDateTime(0, TradeLinkTime, TradeLinkSec);
        }


        public static DateTime ToDateTime(int TradeLinkDate, int TradeLinkTime, int TradeLinkSec)
        {
            int hour = (int)Math.Floor((decimal)TradeLinkTime / 100);
            int min = TradeLinkTime - (hour*100);
            int year = 1, day = 1, month = 1;
            if (TradeLinkDate != 0)
            {
                year = Convert.ToInt32(TradeLinkDate.ToString().Substring(0, 4));
                month = Convert.ToInt32(TradeLinkDate.ToString().Substring(4, 2));
                day = Convert.ToInt32(TradeLinkDate.ToString().Substring(6, 2));
            }
            return new DateTime(year, month, day, hour, min, TradeLinkSec);
        }

        public static int ToTLDate(DateTime dt)
        {
            return (dt.Year * 10000) + (dt.Month * 100) + dt.Day;
        }
        public static int ToTLDate(long DateTimeTicks)
        {
            return ToTLDate(new DateTime(DateTimeTicks));
        }
        public static int ToTLTime(DateTime dt)
        {
            return (dt.Hour * 100) + dt.Minute;
        }


        /// <summary>
        /// Converts a TLDate integer format into an array of ints
        /// </summary>
        /// <param name="fulltime">The fulltime.</param>
        /// <returns>int[3] { year, month, day}</returns>
        static int[] TLDateSplit(int fulltime)
        {
            int[] splittime = new int[3]; // year, month, day
            splittime[2] = (fulltime - (fulltime % 10000))/10000;
            splittime[1] = ((fulltime %10000) - ((fulltime % 10000) % 100));
            splittime[0] = (fulltime-splittime[2]-splittime[1]);
            return splittime;
        }



        /// <summary>
        /// Tests if two dates are the same, given a mask as DateMatchType.
        /// 
        /// ie, 20070605 will match 20070705 if DateMatchType is Day or Year.
        /// </summary>
        /// <param name="fulldate">The fulldate in TLDate format (int).</param>
        /// <param name="matchdate">The matchdate to test against (int).</param>
        /// <param name="dmt">The "mask" that says what to pay attention to when matching.</param>
        /// <returns></returns>
        public static bool TLDateMatch(int fulldate,int matchdate, DateMatchType dmt)
        {
            const int d = 0, m=1,y=2;
            if (dmt == DateMatchType.None) 
                return false;
            bool matched = true;
            // if we're requesting a day match,
            if ((dmt & DateMatchType.Day) == DateMatchType.Day)
                matched &= TLDateSplit(fulldate)[d] == TLDateSplit(matchdate)[d];
            if ((dmt & DateMatchType.Month)==DateMatchType.Month)
                matched &= TLDateSplit(fulldate)[m] == TLDateSplit(matchdate)[m];
            if ((dmt & DateMatchType.Year)== DateMatchType.Year)
                matched &= TLDateSplit(fulldate)[y] == TLDateSplit(matchdate)[y];
            return matched;
        }

        public static string CleanVer(string ver)
        {
            Regex re = new Regex("[0-9]+");
            Match m = re.Match(ver);
            if (m.Success) return m.Value;
            else return "0";
        }
		public static bool isEarlyClose(int today) 
        {
            try
            {
                return GetCloseTime().Contains(today);
            }
            catch (Exception) { return false; }
        }
        public static int GetEarlyClose(int today)
        {
            try
            {
                return (int)GetCloseTime()[today];
            }
            catch (Exception) { return 0; }
        }
        public static Hashtable GetCloseTime()
        {
            StreamReader f = new StreamReader("EarlyClose.csv");
            string[] r = new string[2];
            string line = "";
            Hashtable h = new Hashtable();
            while ((line = f.ReadLine())!=null)
            {
                r = line.Split(',');
                h.Add(Convert.ToInt32(r[0]),Convert.ToInt32(r[1]));
            }
            f.Close();
            return h; 
        }
        public static void FillsToText(List<Trade> stocktrades,char delimiter,string filepath)
        { // works on a queue of Trade objects
            StreamWriter sw = new StreamWriter(filepath, false);
            sw.WriteLine("Date,Time,Symbol,Side,xSize,xPrice,Comment");
            foreach (Trade t in stocktrades)
                sw.WriteLine(t.ToString(delimiter));
            sw.Close();
        }

        public static string[] TradesToClosedPL(List<Trade> tradelist) { return TradesToClosedPL(tradelist, ','); }

        public static string[] TradesToClosedPL(List<Trade> tradelist, char delimiter)
        {
            List<string> rowoutput = new List<string>();
            Dictionary<string, Position> posdict = new Dictionary<string, Position>();
            foreach (Trade t in tradelist)
            {
                string r = t.ToString(delimiter) + delimiter;
                string s = t.symbol;
                decimal cpl = 0;
                decimal opl = 0;
                int csize = 0;
                if (!posdict.ContainsKey(s))
                {
                    posdict.Add(s, new Position(t));
                }
                else
                {
                    cpl = posdict[s].Adjust(t); // update the trade and get any closed pl
                    opl = BoxMath.OpenPL(t.xprice, posdict[s]); // get any leftover open pl
                    if (cpl != 0) csize = t.xsize; // if we closed any pl, get the size
                }
                string[] pl = new string[] { opl.ToString("f2"), cpl.ToString("f2"), posdict[s].Size.ToString(), csize.ToString(), posdict[s].AvgPrice.ToString("f2") };
                r += string.Join(delimiter.ToString(), pl);
                rowoutput.Add(r);
            }
            return rowoutput.ToArray();

        }

        public static void ClosedPLToText(List<Trade> tradelist, char delimiter, string filepath)
        {
            StreamWriter sw = new StreamWriter(filepath, false);
            string header = string.Join(delimiter.ToString(), Enum.GetNames(typeof(TradePLField)));
            sw.WriteLine(header);
            string[] lines = TradesToClosedPL(tradelist, delimiter);
            foreach (string line in lines)
                sw.WriteLine(line);
            sw.Close();           
        }
            

        static bool IsBox(Type t) { return (t.BaseType.IsSubclassOf(typeof(Box))) || t.BaseType.Equals(typeof(Box)); }
        /// <summary>
        /// Gets teh fully qualified boxnames found in a given file.
        /// </summary>
        /// <param name="boxdll">The file path of the assembly containing the boxes.</param>
        /// <returns></returns>
        public static List<string> GetBoxList(string boxdll)
        {
            List<string> boxlist = new List<string>();
            Assembly a;
            try
            {
                a = Assembly.LoadFrom(boxdll);
            }
            catch (Exception ex) { boxlist.Add(ex.Message); return boxlist; }
            return GetBoxList(a);
        }
        /// <summary>
        /// Gets all the fully-qualified boxnames found in a given assembly.
        /// </summary>
        /// <param name="boxdll">The assembly.</param>
        /// <returns></returns>
        public static List<string> GetBoxList(Assembly boxdll)
        {
            List<string> boxlist = new List<string>();
            Type[] t;
            try
            {
                t = boxdll.GetTypes();
            }
            catch (Exception ex) { boxlist.Add(ex.Message); return boxlist; }
            for (int i = 0; i < t.GetLength(0); i++)
                if (IsBox(t[i])) boxlist.Add(t[i].FullName);
            return boxlist;
        }
        
    }
    public enum TickFileType
    {
        Invalid = 0,
        EPF,
        IDX,
    }

    public struct TickFileInfo
    {
        public string symbol;
        public DateTime date;
        public TickFileType type;
    }

    public enum DateMatchType
    {
        None = 0,
        Day = 1,
        Month = 2,
        Year = 4,
    }

    public enum TradePLField
    {
        Date = 0,
        Time,
        Symbol,
        Side,
        xSize,
        xPrice,
        Comment,
        OpenPL,
        ClosedPL,
        OpenSize,
        ClosedSize,
        AvgPrice,
    }
}
