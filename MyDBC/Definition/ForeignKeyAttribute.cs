using System;

namespace MyDBC.Definition
{
    public class ForeignKeyAttribute : Attribute
    {
        public readonly string Table;
        public readonly string Column;

        public ForeignKeyAttribute(string table, string column)
        {
            Table = table;
            Column = column;
        }

        public override string ToString()
        {
            return $"`{Table}` (`{Column}`)";
        }
    }
}
