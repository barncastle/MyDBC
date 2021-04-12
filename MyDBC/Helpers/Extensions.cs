using DBFileReaderLib;
using System;
using System.Collections;

namespace MyDBC.Helpers
{
    internal static class Extensions
    {
        public static IDictionary GetRecords(this DBReader reader, Type type)
        {
            var methodInfo = typeof(DBReader).GetMethod("GetRecords");
            var generic = methodInfo.MakeGenericMethod(type);
            return (IDictionary)generic.Invoke(reader, null);
        }

        public static string Sql(this string value)
        {
            return value.Replace("'", "''");
        }
    }
}
