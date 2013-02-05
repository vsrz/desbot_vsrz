using System;
using System.Collections.Generic;
namespace desBot
{
    class CountryCode
    {
        public string code;
        public string name;
    }

    //System.TimeZone is crap, and System.TimeZoneInfo requires .NET 3.5+
    //so just use this instead
    abstract class TimeZoneData
    {
        //ctor
        protected TimeZoneData(int offset, string name, string full, string summer, string summerfull)
        {
            this.offset = offset; this.name = name; this.fullname = full; this.summername = summer; this.summerfullname = summerfull;
            countries = new List<CountryCode>();
        }

        //offset, in minutes, from UTC (non-DST)
        protected int offset { get; private set; }

        //friendly name and full name
        string name;
        string fullname;

        //name during summertime
        string summername;
        string summerfullname;

        //countries in this zone
        List<CountryCode> countries;

        //queries the DST offset at the given UTC time, in minutes
        public abstract int QueryDSTOffset(DateTime utc);

        public void AddCountry(string code, string country)
        {
            CountryCode entry = new CountryCode();
            entry.code = code;
            entry.name = country;
            countries.Add(entry);
        }

        public CountryCode MatchesCode(string code)
        {
            foreach (CountryCode entry in countries)
            {
                if (entry.code == code) return entry;
            }
            return null;
        }
        public CountryCode MatchesFull(string needle)
        {
            foreach (CountryCode code in countries)
            {
                if (code.name.ToLower().Contains(needle))
                {
                    return code;
                }
            }
            return null;
        }

        public string GetCurrentShortName()
        {
            DateTime utc = DateTime.UtcNow;
            return QueryDSTOffset(utc) == 0 ? name : summername;
        }

        public string GetCurrentFullName()
        {
            DateTime utc = DateTime.UtcNow;
            return QueryDSTOffset(utc) == 0 ? fullname : summerfullname;
        }

        //get local time string
        public string GetLocalTime()
        {
            DateTime utc = DateTime.UtcNow;
            utc = utc.AddMinutes(offset + QueryDSTOffset(utc) + TimeZones.offset);
            return utc.ToString("HH:mm") + " (" + GetCurrentShortName() + ")";
        }

    }

    //timezone data for a zone that doesn't use DST
    class NonDSTTimeZoneData : TimeZoneData
    {
        public NonDSTTimeZoneData(int offset, string name, string full) : base(offset, name, full, name, full) {}
        
        public override int QueryDSTOffset(DateTime utc)
        {
            //DST never used
            return 0;
        }
    }

    //European timezone implementation
    class EuropeanDSTTimeZoneData : TimeZoneData
    {
        public EuropeanDSTTimeZoneData(int offset, string name, string full, string summer, string summerfull) : base(offset, name, full, summer, summerfull) {}

        public override int QueryDSTOffset(DateTime utc)
        {
            //european zones start at the last sunday of march, and end on the last sunday of october, at 01:00 UTC
            DateTime begin = new DateTime(utc.Year, 3, 31, 1, 0, 0, DateTimeKind.Utc);
            while (begin.DayOfWeek != DayOfWeek.Sunday) begin = begin.AddDays(-1);
            DateTime end = new DateTime(utc.Year, 10, 31, 1, 0, 0, DateTimeKind.Utc);
            while (end.DayOfWeek != DayOfWeek.Sunday) end = end.AddDays(-1);

            if (utc.CompareTo(begin) >= 0 && utc.CompareTo(end) < 0)
            {
                //we are in DST
                return 60;
            }
            else
            {
                //we are not in DST
                return 0;
            }
        }
    }
    
    //North-american timezone implementation
    class NorthAmericanDSTTimeZoneData : TimeZoneData
    {
        public NorthAmericanDSTTimeZoneData(int offset, string name, string full, string summer, string summerfull) : base(offset, name, full, summer, summerfull) {}

        public override int QueryDSTOffset(DateTime utc)
        {
            //american zones start at the second sunday of march, 02:00 local, and end on the first sunday of november, 01:00 local
            DateTime begin = new DateTime(utc.Year, 3, 8, 2, 0, 0, DateTimeKind.Utc);
            while (begin.DayOfWeek != DayOfWeek.Sunday) begin = begin.AddDays(+1);
            begin = begin.AddMinutes(offset);
            DateTime end = new DateTime(utc.Year, 11, 1, 1, 0, 0, DateTimeKind.Utc);
            while (end.DayOfWeek != DayOfWeek.Sunday) end = end.AddDays(+1);
            end = end.AddMinutes(offset);

            if (utc.CompareTo(begin) >= 0 && utc.CompareTo(end) < 0)
            {
                //we are in DST
                return 60;
            }
            else
            {
                //we are not in DST
                return 0;
            }
        }
    }

    class AustralianDSTTimeZoneData : TimeZoneData
    {
        public AustralianDSTTimeZoneData(int offset, string name, string full, string summer, string summerfull) : base(offset, name, full, summer, summerfull) {}

        public override int QueryDSTOffset(DateTime utc)
        {
            //australian zones start at the first sunday of october, and end on the first sunday in april
            DateTime begin = new DateTime(utc.Year, 10, 1, 2, 0, 0, DateTimeKind.Utc);
            while (begin.DayOfWeek != DayOfWeek.Sunday) begin = begin.AddDays(+1);
            DateTime end = new DateTime(utc.Year, 4, 1, 2, 0, 0, DateTimeKind.Utc);
            while (end.DayOfWeek != DayOfWeek.Sunday) end = end.AddDays(+1);

            if (utc.CompareTo(begin) >= 0 || utc.CompareTo(end) < 0)
            {
                //we are not in DST
                return 0;
            }
            else
            {
                //we are in DST
                return 60;
            }
        }
    }

    static class TimeZones
    {
        static TimeZoneData est;
        static TimeZoneData cet;
        static TimeZoneData kr;
        public static int offset = 0;

        static List<TimeZoneData> InitializeTimeZones()
        {
            List<TimeZoneData> result = new List<TimeZoneData>();

            //europe
            TimeZoneData zone = new EuropeanDSTTimeZoneData(+0 * 60, "WET", "Western European Time", "WEST", "Western European Summer Time");
            zone.AddCountry("ES", "Spain (Canary Islands)");
            zone.AddCountry("PT", "Portugal");
            zone.AddCountry("IE", "Ireland");
            zone.AddCountry("GB", "the United Kingdom");
            zone.AddCountry("DK", "Denmark (Faroe Islands)");
            zone.AddCountry("MA", "Marocco");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+0 * 60, "WET", "Western European Time");
            zone.AddCountry("IS", "Iceland");
            result.Add(zone);

            zone = new EuropeanDSTTimeZoneData(+1 * 60, "CET", "Central European Time", "CEST", "Central European Summer Time");
            zone.AddCountry("AL", "Albania");
            zone.AddCountry("AD", "Andorra");
            zone.AddCountry("AT", "Austria");
            zone.AddCountry("BE", "Belgium");
            zone.AddCountry("BA", "Bosnia and Herzegovina");
            zone.AddCountry("HR", "Croatia");
            zone.AddCountry("CZ", "Czech Republic");
            zone.AddCountry("DK", "Denmark");
            zone.AddCountry("FR", "France");
            zone.AddCountry("DE", "Germany");
            zone.AddCountry("GI", "Gibraltar");
            zone.AddCountry("HU", "Hungary");
            zone.AddCountry("IT", "Italy");
            zone.AddCountry("LI", "Lichtenstein");
            zone.AddCountry("LU", "Luxembourg");
            zone.AddCountry("MK", "Macedonia");
            zone.AddCountry("MT", "Malta");
            zone.AddCountry("MC", "Monaco");
            zone.AddCountry("ME", "Montenegro");
            zone.AddCountry("NL", "the Netherlands");
            zone.AddCountry("NO", "Norway");
            zone.AddCountry("PL", "Poland");
            zone.AddCountry("SM", "San Marino");
            zone.AddCountry("RS", "Serbia");
            zone.AddCountry("SK", "Slovakia");
            zone.AddCountry("SI", "Slovenia");
            zone.AddCountry("ES", "Spain");
            zone.AddCountry("SE", "Sweden");
            zone.AddCountry("CH", "Switzerland");
            zone.AddCountry("VA", "Vatican City");
            result.Add(zone);
            cet = zone;

            zone = new EuropeanDSTTimeZoneData(+2 * 60, "EET", "Eastern European Time", "EEST", "Eastern European Summer Time"); 
            zone.AddCountry("BG", "Bulgaria");
            zone.AddCountry("CY", "Cyprus");
            zone.AddCountry("EE", "Estonia");
            zone.AddCountry("FI", "Finland");
            zone.AddCountry("GR", "Greece");
            zone.AddCountry("IL", "Israel");
            zone.AddCountry("JO", "Jordan");
            zone.AddCountry("LV", "Latvia");
            zone.AddCountry("LB", "Lebanon");
            zone.AddCountry("LT", "Lithuania");
            zone.AddCountry("MD", "Moldova");
            zone.AddCountry("PS", "Palestinian Territories");
            zone.AddCountry("RO", "Romania");
            zone.AddCountry("SY", "Syria");
            zone.AddCountry("TR", "Turkey");
            zone.AddCountry("UA", "Ukraine");
            result.Add(zone);

            //russia
            zone = new NonDSTTimeZoneData(+3 * 60, "FET", "Further-eastern European Time");
            zone.AddCountry("BY", "Belarus");
            zone.AddCountry("RU", "Russia (Kaliningrad)");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+4 * 60, "MSK", "Moscow Time");
            zone.AddCountry("RU", "Russia (Moscow)");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+6 * 60, "YEKT", "Yekaterinburg Time");
            zone.AddCountry("RU", "Russia (Yekaterinburg)");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+7 * 60, "OMST", "Omsk Time");
            zone.AddCountry("RU", "Russia (Omsk)");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+8 * 60, "KRAT", "Krasnoyarsk Time");
            zone.AddCountry("RU", "Russia (Krasnoyarsk)");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+9 * 60, "IRKT", "Irkutst Time");
            zone.AddCountry("RU", "Russia (Irkutsk)");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+10 * 60, "YAKT", "Yakutsk Time");
            zone.AddCountry("RU", "Russia (Yakutsk)");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+11 * 60, "VLAT", "Vladivostok Time");
            zone.AddCountry("RU", "Russia (Vladivostok)");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+12 * 60, "MAGT", "Magadan Time");
            zone.AddCountry("RU", "Russia (Magadan)");
            result.Add(zone);

            //africa
            zone = new NonDSTTimeZoneData(-1 * 60, "CVT", "Cape Verde Time");
            zone.AddCountry("CV", "Cape Verde");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+0 * 60, "GMT", "Greenwich Mean Time");
            zone.AddCountry("BF", "Burkina Faso");
            zone.AddCountry("GM", "Gambia");
            zone.AddCountry("GH", "Ghana");
            zone.AddCountry("GN", "Guinea");
            zone.AddCountry("GW", "Guinea-Bissau");
            zone.AddCountry("CI", "Ivory Coast");
            zone.AddCountry("LR", "Liberia");
            zone.AddCountry("ML", "Mali");
            zone.AddCountry("MR", "Mauritania");
            zone.AddCountry("SH", "Saint Helena");
            zone.AddCountry("AC", "Ascension Island");
            zone.AddCountry("TA", "Tristan da Cunha");
            zone.AddCountry("ST", "Sao Tome");
            zone.AddCountry("SN", "Senegal");
            zone.AddCountry("SL", "Sierra Leone");
            zone.AddCountry("TG", "Togo");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+1 * 60, "WAT", "West Africa Time");
            zone.AddCountry("DZ", "Algeria");
            zone.AddCountry("AO", "Angola");
            zone.AddCountry("BJ", "Benin");
            zone.AddCountry("CM", "Cameroon");
            zone.AddCountry("CF", "Central African Republic");
            zone.AddCountry("TD", "Chad");
            zone.AddCountry("CG", "Republic of the Congo");
            zone.AddCountry("CD", "Democratic Republic of the Congo (Western)");
            zone.AddCountry("GQ", "Equatorial Guinea");
            zone.AddCountry("GA", "Gabon");
            zone.AddCountry("NE", "Niger");
            zone.AddCountry("NG", "Nigeria");
            zone.AddCountry("TN", "Tunesia");
            zone.AddCountry("NA", "Namibia"); //actually uses summer time (WAST), UTC+02, september to april
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+2 * 60, "CAT", "Central Africa Time");
            zone.AddCountry("BI", "Burundi");
            zone.AddCountry("BW", "Botswana");
            zone.AddCountry("EG", "Egypt");
            zone.AddCountry("CD", "Democratic Republic of the Congo (Eastern)");
            zone.AddCountry("LS", "Lesotho");
            zone.AddCountry("LY", "Libya");
            zone.AddCountry("MW", "Malawi");
            zone.AddCountry("MZ", "Mozambique");
            zone.AddCountry("RW", "Rwanda");
            zone.AddCountry("ZM", "Zambia");
            zone.AddCountry("ZW", "Zimbabwe");
            zone.AddCountry("ZA", "South Africa");
            zone.AddCountry("SZ", "Swaziland");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+3 * 60, "EAT", "Eastern Africa Time");
            zone.AddCountry("KM", "Comoros");
            zone.AddCountry("DJ", "Djibouti");
            zone.AddCountry("ER", "Eritrea");
            zone.AddCountry("ET", "Ethiopia");
            zone.AddCountry("KE", "Kenia");
            zone.AddCountry("MG", "Madagascar");
            zone.AddCountry("SO", "Somalia");
            zone.AddCountry("SS", "South Sudan");
            zone.AddCountry("SD", "Sudan");
            zone.AddCountry("TZ", "Tanzania");
            zone.AddCountry("UG", "Uganda");
            zone.AddCountry("YT", "Mayotte");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+4 * 60, "MUT", "Mauritius Time");
            zone.AddCountry("MU", "Mauritius");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+4 * 60, "SCT", "Seychelles Time");
            zone.AddCountry("SC", "Seychelles");
            result.Add(zone);

            //asia
            zone = new EuropeanDSTTimeZoneData(+3 * 60 + 30, "IRST", "Iran Standard Time", "IRDT", "Iran Daylight Time");
            zone.AddCountry("IR", "Iran");
            result.Add(zone);

            zone = new EuropeanDSTTimeZoneData(+4 * 60, "AZT", "Azerbaijan Time", "AZST", "Azerbaijan Summer Time");
            zone.AddCountry("AZ", "Azerbaijan");
            zone.AddCountry("AM", "Armenia"); //not sure if they actually name it like AZT, but time is the same
            result.Add(zone);

            zone = new EuropeanDSTTimeZoneData(+5 * 60, "PST", "Pakistan Standard Time", "PDT", "Pakistan Daylight Time");
            zone.AddCountry("PK", "Pakistan");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+3 * 60, "UTC+03:00", "UTC+03:00");
            zone.AddCountry("BH", "Bahrain");
            zone.AddCountry("IQ", "Iraq");
            zone.AddCountry("KW", "Kuwait");
            zone.AddCountry("QA", "Qatar");
            zone.AddCountry("SA", "Saudi Arabia");
            zone.AddCountry("YE", "Yemen");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+4 * 60, "UTC+04:00", "UTC+04:00");
            zone.AddCountry("GE", "Georgia");
            zone.AddCountry("OM", "Oman");
            zone.AddCountry("AE", "United Arab Emirates");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+4 * 60 + 30, "UTC+04:30", "UTC+04:30");
            zone.AddCountry("AF", "Afghanistan");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+5 * 60, "UTC+05:00", "UTC+05:00");
            zone.AddCountry("MV", "Maldives");
            zone.AddCountry("TJ", "Tajikistan");
            zone.AddCountry("TM", "Turkmenistan");
            zone.AddCountry("UZ", "Uzbekistan");
            zone.AddCountry("AU", "Australia (Heard and McDonald Islands)");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+5 * 60 + 30, "UTC+05:30", "UTC+05:30");
            zone.AddCountry("IN", "India");
            zone.AddCountry("KZ", "Kazachstan (West)");
            zone.AddCountry("LK", "Sri Lanka");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+5 * 60 + 45, "UTC+05:45", "UTC+05:45");
            zone.AddCountry("NP", "Nepal");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+6 * 60, "UTC+06:00", "UTC+06:00");
            zone.AddCountry("BT", "Bhutan");
            zone.AddCountry("KZ", "Kazachstan (East)");
            zone.AddCountry("KG", "Kyrgyzstan");
            zone.AddCountry("BD", "Bangladesh");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+7 * 60, "UTC+07:00", "UTC+07:00");
            zone.AddCountry("TH", "Thailand");
            zone.AddCountry("VN", "Vietnam");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+7 * 60, "UTC+07:00", "UTC+07:00");
            zone.AddCountry("KH", "Cambodia");
            zone.AddCountry("ID", "Indonesia (West)");
            zone.AddCountry("LA", "Laos");
            zone.AddCountry("MN", "Mongolia (West)");
            zone.AddCountry("AU", "Australia (Christmas Island)");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+8 * 60, "UTC+08:00", "UTC+08:00");
            zone.AddCountry("BN", "Brunei");
            zone.AddCountry("CN", "China");
            zone.AddCountry("TW", "China (Taiwan)");
            zone.AddCountry("HK", "Hong Kong");
            zone.AddCountry("ID", "Indonesia (Central)");
            zone.AddCountry("MO", "Macau");
            zone.AddCountry("MY", "Malaysia");
            zone.AddCountry("MN", "Mongolia (East)");
            zone.AddCountry("PH", "Phillipines");
            zone.AddCountry("SG", "Singapore");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+9 * 60, "UTC+09:00", "UTC+09:00");
            zone.AddCountry("TL", "East Timor");
            zone.AddCountry("ID", "Indonesia (East)");
            zone.AddCountry("JP", "Japan");
            zone.AddCountry("KP", "North Korea");
            zone.AddCountry("KR", "South Korea");
            zone.AddCountry("PW", "Palau");
            result.Add(zone);
            kr = zone;

            //americas
            zone = new NorthAmericanDSTTimeZoneData(-11 * 60, "SST", "Samoa Standard Time", "SDT", "Samoa Daylight Time");
            zone.AddCountry("US", "United States (Zone X, Samoa)");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(-10 * 60, "HAST", "Hawaii Standard Time");
            zone.AddCountry("US", "United States (Zone W, Hawaii)");
            result.Add(zone);

            zone = new NorthAmericanDSTTimeZoneData(-9 * 60, "AKST", "Alaska Standard Time", "AKDT", "Alaska Daylight Time");
            zone.AddCountry("US", "United States (Zone V, Alaska)");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(-8 * 60, "UTC-08:00", "UTC-08:00");
            zone.AddCountry("MX", "Mexico (Clarion Island)");
            result.Add(zone);

            zone = new NorthAmericanDSTTimeZoneData(-8 * 60, "PST", "Pacific Standard Time", "PDT", "Pacific Daylight Time");
            zone.AddCountry("CA", "Canada (British Colombia, Yukon)");
            zone.AddCountry("MX", "Mexico (Northwest Zone)");
            zone.AddCountry("US", "United States (Zone U, Pacific Coast, Nevada)");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(-7 * 60, "MST", "Mountain Standard Time");
            zone.AddCountry("CA", "Canada (Peace River, Creston, Tungsten)");
            zone.AddCountry("MX", "Mexico (Sonora)");
            zone.AddCountry("US", "United States (Arizona)");
            result.Add(zone);

            zone = new NorthAmericanDSTTimeZoneData(-7 * 60, "MST", "Mountain Standard Time", "MDT", "Mountain Daylight Time");
            zone.AddCountry("CA", "Canada (Alberta, Kootenay, Northwest Territories)");
            zone.AddCountry("MX", "Mexico (Pacific Zone)");
            zone.AddCountry("US", "United States (Zone T, Rocky Mountains)");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(-6 * 60, "CST", "Central Standard Time");
            zone.AddCountry("CA", "Canada (Saskatchewan)");
            zone.AddCountry("GT", "Guatemala");
            zone.AddCountry("HN", "Honduras");
            zone.AddCountry("NI", "Nicaragua");
            zone.AddCountry("CL", "Chile(West)"); //actually uses DST
            zone.AddCountry("BZ", "Belize");
            zone.AddCountry("CR", "Costa Rica");
            zone.AddCountry("SV", "El Salvador");
            result.Add(zone);

            zone = new NorthAmericanDSTTimeZoneData(-6 * 60, "CST", "Central Standard Time", "CDT", "Central Daylight Time");
            zone.AddCountry("CA", "Canada (Nunavut West, Manitoba, Ontario NW)");
            zone.AddCountry("MX", "Mexico (Central Zone)");
            zone.AddCountry("US", "United States (Zone S, Gulf Coast, Great Plains, Mississippi Valley)");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(-5 * 60, "EST", "Eastern Standard Time");
            zone.AddCountry("CA", "Canada (Atikokan/Pickle Lake, Southampton Island)");
            zone.AddCountry("CL", "Chile (Central)"); //actually uses DST?
            zone.AddCountry("CO", "Colombia");
            zone.AddCountry("EC", "Ecuador/Equador"); //???
            zone.AddCountry("PE", "Peru");
            zone.AddCountry("HT", "Haiti");
            zone.AddCountry("JM", "Jamaica");
            zone.AddCountry("PA", "Panama");
            result.Add(zone);

            zone = new NorthAmericanDSTTimeZoneData(-5 * 60, "EST", "Eastern Standard Time", "EDT", "Eastern Daylight Time");
            zone.AddCountry("CA", "Canada (Nunavut East, Ontario East, Quebec West)");
            zone.AddCountry("CU", "Cuba");
            zone.AddCountry("US", "United States (Zone R, Atlantic Coast)");
            zone.AddCountry("BS", "Bahamas"); //actually uses DST?
            result.Add(zone);
            est = zone;

            zone = new NonDSTTimeZoneData(-4 * 60 - 30, "VET", "Venezuela Time");
            zone.AddCountry("VE", "Venezuela");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(-4 * 60, "AST", "Atlantic Standard Time");
            zone.AddCountry("CA", "Canada (Quebec East)");
            zone.AddCountry("DM", "Dominica");
            zone.AddCountry("BB", "Barbados");
            zone.AddCountry("BO", "Bolivia");
            zone.AddCountry("CL", "Chile (East)"); //actually uses DST?
            zone.AddCountry("PY", "Paraguay"); //actually uses DST?
            zone.AddCountry("GY", "Guyana");
            zone.AddCountry("AG", "Antigua");
            zone.AddCountry("DO", "Dominican Republic");
            zone.AddCountry("GD", "Grenada");
            zone.AddCountry("KN", "Saint Kitts and Nevis");
            zone.AddCountry("LC", "Saint Lucia");
            zone.AddCountry("VC", "Saint Vincent and the Grenadines");
            zone.AddCountry("TT", "Trinidad and Tobago");
            result.Add(zone);

            zone = new NorthAmericanDSTTimeZoneData(-4 * 60, "AST", "Atlantic Standard Time", "ADT", "Atlantic Daylight Time");
            zone.AddCountry("CA", "Canada (Labrador, New Brunswick, Nova Scotia, Prince Edward Island)");
            zone.AddCountry("US", "United States (Zone Q, Puerto Rico, Virgin Islands)");
            result.Add(zone);

            zone = new NorthAmericanDSTTimeZoneData(-3 * 60 + 30, "NST", "Newfoundland Standard Time", "NDT", "Newfoundland Daylight Time");
            zone.AddCountry("CA", "Canada (Labrador SE, Newfoundland)");
            result.Add(zone);

            zone = new EuropeanDSTTimeZoneData(-3 * 60, "GST", "Greenland Standard Time", "GDT", "Greenland Daylight Time");
            zone.AddCountry("GL", "Greenland (most populated parts)");
            zone.AddCountry("FK", "Falkland Islands");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(-3 * 60, "UTC-03:00", "UTC-03:00");
            zone.AddCountry("AR", "Argentina");
            zone.AddCountry("BR", "Brazil"); //actually uses DST?
            zone.AddCountry("UY", "Uruguay"); //actually uses DST?
            zone.AddCountry("FR", "France (French-Guinea)");
            zone.AddCountry("SR", "Suriname");
            result.Add(zone);

            //oceania
            zone = new NonDSTTimeZoneData(+6 * 60 + 30, "UTC+06:30", "UTC+06:30");
            zone.AddCountry("AU", "Australia (Cocos Islands)");
            zone.AddCountry("MM", "Myanmar");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+10 * 60, "UTC+10:00", "UTC+10:00");
            zone.AddCountry("PG", "Papua New Guinea");
            zone.AddCountry("FM", "Federated States of Micronesia (West)");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+11 * 60, "UTC+11:00", "UTC+11:00");
            zone.AddCountry("FM", "Federated States of Micronesia (East)");
            zone.AddCountry("SB", "Solomon Islands");
            result.Add(zone);

            zone = new NorthAmericanDSTTimeZoneData(+10 * 60, "CHST", "Chamorro Standard Time", "CHDT", "Chamorro Daylight Time");
            zone.AddCountry("US", "United States (Zone K, Guam, Northern Mariana Islands)");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+11 * 60 + 30, "UTC+11:30", "UTC+11:30");
            zone.AddCountry("AU", "Australia (Norfolk Island)");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+8 * 60, "WST", "West Standard Time");
            zone.AddCountry("AU", "Australia (Western)");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+9 * 60 + 30, "ACST", "Australian Central Standard Time");
            zone.AddCountry("AU", "Australia (Northern Territory)");
            result.Add(zone);

            zone = new AustralianDSTTimeZoneData(+9 * 60 + 30, "ACST", "Australian Central Standard Time", "ACDT", "Australian Central Daylight Time");
            zone.AddCountry("AU", "Australia (South)");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+10 * 60, "AEST", "Australian Eastern Standard Time");
            zone.AddCountry("AU", "Australia (Queensland)");
            result.Add(zone);

            zone = new AustralianDSTTimeZoneData(+10 * 60, "AEST", "Australian Eastern Standard Time", "AEDT", "Australian Eastern Daylight Time");
            zone.AddCountry("AU", "Australia (New South Wales, Capital Territory, Victoria, Tasmania)");
            result.Add(zone);

            zone = new AustralianDSTTimeZoneData(+12 * 60, "UTC+12:00", "UTC+12:00", "UTC+13:00", "UTC+13:00");
            zone.AddCountry("FJ", "Fiji");
            zone.AddCountry("NZ", "New Zealand");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+12 * 60, "UTC+12:00", "UTC+12:00");
            zone.AddCountry("KI", "Kiribati (West)");
            zone.AddCountry("MH", "Marshall Islands");
            zone.AddCountry("NR", "Nauru");
            zone.AddCountry("TV", "Tuvalu");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+13 * 60, "UTC+13:00", "UTC+13:00");
            zone.AddCountry("KI", "Kiribati (Central)");
            zone.AddCountry("TO", "Tonga");
            result.Add(zone);

            zone = new NonDSTTimeZoneData(+14 * 60, "UTC+14:00", "UTC+14:00");
            zone.AddCountry("KI", "Kiribati (East)");
            result.Add(zone);

            return result;
        }

        static Dictionary<string, string> InitializeAliasses()
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            result.Add("UK", "GB");
            return result;
        }

        static List<TimeZoneData> timezones = InitializeTimeZones();
        static Dictionary<string, string> aliasses = InitializeAliasses();

        static public string Lookup(string needle, bool oper)
        {
            if (needle.StartsWith("offset "))
            {
                if (!oper) throw new Exception("Only channel operators can set the global time offset");
                bool ok = int.TryParse(needle.Substring(7), out offset);
                if (!ok) throw new Exception("Failed to set time offset, parse failed");
                return "Time lookup: Global offset is now " + offset + " minutes";
            }
            if (string.IsNullOrEmpty(needle))
            {
                //look up common times
                string result = "It's now " + cet.GetLocalTime().Substring(0, 5) + " CET, " + est.GetLocalTime().Substring(0, 5) + " EST, and " + kr.GetLocalTime().Substring(0, 5) + " in Korea.";
                return result;
            }
            Dictionary<CountryCode, TimeZoneData> matches = new Dictionary<CountryCode,TimeZoneData>();
            bool exact = false;
            if(needle.Length == 2)
            {
                needle = needle.ToUpper();
                if(aliasses.ContainsKey(needle)) needle = aliasses[needle];
                foreach (TimeZoneData tz in timezones)
                {
                    CountryCode cc = tz.MatchesCode(needle);
                    if (cc != null)
                    {
                        matches.Add(cc, tz);
                        exact = true;
                    }
                }
            }
            else if (needle.Length > 2)
            {
                needle = needle.ToLower();
                foreach (TimeZoneData tz in timezones)
                {
                    CountryCode cc = tz.MatchesFull(needle);
                    if (cc != null)
                    {
                        matches.Add(cc, tz);
                    }
                }
            }
            if (matches.Count == 0) throw new Exception("Time lookup: country or region '" + needle + "' not recognized");
            if (!exact && matches.Count > 3) throw new Exception("Time lookup: too many matches for '" + needle + "', try to be more specific");
            if (matches.Count == 1)
            {
                DateTime utc = DateTime.UtcNow;
                foreach (KeyValuePair<CountryCode, TimeZoneData> match in matches) //foreach only loops once
                {
                    return "It's now " + match.Value.GetLocalTime() + " in " + match.Key.name;
                }
            }
            else
            {
                string result = "";
                foreach (KeyValuePair<CountryCode, TimeZoneData> match in matches)
                {
                    result += "It's now " + match.Value.GetLocalTime() + " in " + match.Key.name + "\n";
                }
                return result.Substring(0, result.Length - 1);
            }
            return ""; //cant happen, but compiler needs to know we have return on all paths
        }
    }


    /// <summary>
    /// Command that generates time
    /// </summary>
    class TimeCommand : Command
    {
        public static void AutoRegister()
        {
            new TimeCommand();
        }

        TimeCommand()
        {
            Privilege = PrivilegeLevel.OnChannel;
            TriggerOnPrivate = true;
        }

        public override string GetKeyword()
        {
            return "time";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " (<country>): Displays the current time in the specified country, or a few common countries if no country is specified";
        }

        public override void Execute(IrcMessage message, string args)
        {
            if (Limiter.AttemptOperation(message.Level))
            {
                message.ReplyAuto(TimeZones.Lookup(args, CommandHandler.GetPrivilegeLevel(message.From) >= PrivilegeLevel.Operator));
            }
        }
    }
}