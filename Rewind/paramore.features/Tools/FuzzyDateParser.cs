/*
 * Piotr Czalpa http://stackoverflow.com/users/80869/piotr-czapla
 * http://stackoverflow.com/questions/887189/fuzzy-date-time-picker-control-in-c-net
*/

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Paramore.Features.Tools
{
    class FuzzyDateTime
    {

        static readonly List<string> dayList = new List<string> { "sun", "mon", "tue", "wed", "thu", "fri", "sat" };
        static readonly List<IDateTimePattern> parsers = new List<IDateTimePattern>
                                                             {
       new RegexDateTimePattern (
            @"next +([2-9]\d*) +months",
            delegate (Match m) {
                var val = int.Parse(m.Groups[1].Value); 
                return DateTime.Now.AddMonths(val);
            }
       ),
       new RegexDateTimePattern (
            @"next +month",
            delegate { 
                return DateTime.Now.AddMonths(1);
            }
       ),           
       new RegexDateTimePattern (
            @"next +([2-9]\d*) +days",
            delegate (Match m) {
                var val = int.Parse(m.Groups[1].Value); 
                return DateTime.Now.AddDays(val);
            }
       ),

       new RegexDateTimePattern (
            @"([2-9]\d*) +months +ago",
            delegate (Match m) {
                var val = int.Parse(m.Groups[1].Value); 
                return DateTime.Now.AddMonths(-val);
            }
       ),
       new RegexDateTimePattern (
            @"([2-9]\d*) days +ago",
            delegate (Match m) {
                var val = int.Parse(m.Groups[1].Value); 
                return DateTime.Now.AddDays(-val);
            }
       ),
       new RegexDateTimePattern (
            @"([2-9]\d*) *h(ours)? +ago",
            delegate (Match m) {
                var val = int.Parse(m.Groups[1].Value); 
                return DateTime.Now.AddMonths(-val);
            }
       ),
       new RegexDateTimePattern (
            @"tomorrow",
            delegate {
                return DateTime.Now.AddDays(1);
            }
       ),
       new RegexDateTimePattern (
            @"today",
            delegate {
                return DateTime.Now;
            }
       ),
       new RegexDateTimePattern (
            @"yesterday",
            delegate {
                return DateTime.Now.AddDays(-1);
            }
       ),
       new RegexDateTimePattern (
            @"(last|next) *(year|month)",
            delegate (Match m) {
                int direction = (m.Groups[1].Value == "last")? -1 :1;
                switch(m.Groups[2].Value) 
                {
                    case "year":
                        return new DateTime(DateTime.Now.Year+direction, 1,1);
                    case "month":
                        return new DateTime(DateTime.Now.Year, DateTime.Now.Month+direction, 1);
                }
                return DateTime.MinValue;
            }
       ),
       new RegexDateTimePattern (
            String.Format(@"(last|next) *({0}).*", String.Join("|", dayList.ToArray())), //handle weekdays
            delegate (Match m) {
                var val = m.Groups[2].Value;
                var direction = (m.Groups[1].Value == "last")? -1 :1;
                var dayOfWeek = dayList.IndexOf(val.Substring(0,3));
                if (dayOfWeek >= 0) {
                    var diff = direction*(dayOfWeek - (int)DateTime.Today.DayOfWeek);
                    if (diff <= 0 ) { 
                        diff = 7 + diff;
                    }
                    return DateTime.Today.AddDays(direction * diff);
                }
                return DateTime.MinValue;
            }
       ),

       new RegexDateTimePattern (
            @"(last|next) *(.+)", // to parse months using DateTime.TryParse
            delegate (Match m) {
                DateTime dt;
                int direction = (m.Groups[1].Value == "last")? -1 :1;
                var s = String.Format("{0} {1}",m.Groups[2].Value, DateTime.Now.Year + direction);
                if (DateTime.TryParse(s, out dt)) {
                    return dt;
                }
               return DateTime.MinValue;
            }
       ),
       new RegexDateTimePattern (
            @".*", //as final resort parse using DateTime.TryParse
            delegate (Match m) {
                DateTime dt;
                var s = m.Groups[0].Value;
                if (DateTime.TryParse(s, out dt)) {
                    return dt;
                }
                return DateTime.MinValue;
            }
       ),
    };

        public static DateTime Parse(string text)
        {
            text = text.Trim().ToLower();
            var dt = DateTime.Now;
            foreach (var parser in parsers)
            {
                dt = parser.Parse(text);
                if (dt != DateTime.MinValue)
                    break;
            }
            return dt;
        }
    }
    interface IDateTimePattern
    {
        DateTime Parse(string text);
    }

    class RegexDateTimePattern : IDateTimePattern
    {
        public delegate DateTime Interpreter(Match m);
        protected Regex regEx;
        protected Interpreter inter;
        public RegexDateTimePattern(string re, Interpreter inter)
        {
            regEx = new Regex(re);
            this.inter = inter;
        }
        public DateTime Parse(string text)
        {
            var m = regEx.Match(text);

            if (m.Success)
            {
                return inter(m);
            }
            return DateTime.MinValue;
        }
    }
}
