using System;

namespace MyDBC.SQL
{
    internal struct SqlColumn
    {
        public Type Type;
        public string Comment;

        public SqlColumn(Type type, string comment)
        {
            Type = type;
            Comment = comment;
        }
    }
}
