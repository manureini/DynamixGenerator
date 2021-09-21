using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DynamixGenerator.NHibernate
{
    internal static class SqlHelper
    {
        private static Regex mTableNameRegex = new("table ([^ ]*?) ");

        public static string GetTableName(string pSql)
        {
            var name = mTableNameRegex.Match(pSql).Groups[1].Value;
            return name;
        }
    }
}
