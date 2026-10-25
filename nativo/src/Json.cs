using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace CrosshairRoulette
{
    // Small helpers over JavaScriptSerializer's Dictionary/object[] trees.
    static class Json
    {
        static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static Dictionary<string, object> Parse(string text)
        {
            try { return Serializer.DeserializeObject(text) as Dictionary<string, object>; }
            catch { return null; }
        }

        public static string Write(object value) => Serializer.Serialize(value);

        public static object Get(object node, string key)
        {
            var d = node as Dictionary<string, object>;
            return d != null && d.TryGetValue(key, out var v) ? v : null;
        }

        public static double? Num(object v)
        {
            if (v is int || v is long || v is decimal || v is double || v is float)
                return Convert.ToDouble(v, CultureInfo.InvariantCulture);
            return null;
        }

        public static double Num(object node, string key, double fallback) => Num(Get(node, key)) ?? fallback;

        public static string Str(object v) => v == null ? null : Convert.ToString(v, CultureInfo.InvariantCulture);

        public static string Str(object node, string key, string fallback) => Str(Get(node, key)) ?? fallback;

        public static bool Bool(object node, string key, bool fallback) => Get(node, key) is bool b ? b : fallback;
    }
}
