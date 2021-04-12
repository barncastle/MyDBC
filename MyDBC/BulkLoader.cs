using DBFileReaderLib.Attributes;
using MyDBC.Definition;
using MyDBC.SQL;
using MySqlConnector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MyDBC
{
    class BulkLoader
    {
        public IReadOnlyDictionary<string, SqlColumn> Columns => _columns;
        public string PrimaryKey { get; private set; }
        public (string, string) ForeignKey { get; private set; }

        private readonly string _tableName;
        private readonly IEnumerator _source;
        private readonly Dictionary<string, SqlColumn> _columns;
        private readonly Dictionary<string, string> _comments;

        public BulkLoader(string tableName, IEnumerable source)
        {
            _tableName = tableName;
            _source = source.GetEnumerator();
            _columns = new Dictionary<string, SqlColumn>();
            _comments = new Dictionary<string, string>();
        }

        public async Task ExportToMySql(MySqlConnection connection, string filename)
        {
            var loader = new MySqlBulkLoader(connection)
            {
                TableName = _tableName,
                FileName = filename,
                NumberOfLinesToSkip = 1,
                FieldTerminator = ",",
                LineTerminator = "\n",
                CharacterSet = "utf8",
                EscapeCharacter = '\b', // use an impossible value
            };

            await loader.LoadAsync();
            File.Delete(filename);
        }

        public async Task<string> ExportToFile(string filename = null)
        {
            filename ??= Path.GetTempFileName();

            using var fs = File.CreateText(filename);
            fs.NewLine = "\n";

            // validate the collecton isn't empty
            if (!_source.MoveNext() || _source.Current == null)
            {
                await fs.DisposeAsync();
                File.Delete(filename);
                throw new ArgumentException("Source IEnumerable is empty");
            }

            // generate the serializer
            var serializer = GenerateSerializer(_source.Current);

            // write header line
            await fs.WriteLineAsync(GetHeaderRow());

            // serialize the collection
            do await fs.WriteLineAsync(serializer(_source.Current));
            while (_source.MoveNext() && _source.Current != null);

            await fs.FlushAsync();
            await fs.DisposeAsync();

            return filename;
        }

        [Obsolete("Broken in the current version of MySqlConnector", true)]
        public Stream GetStream()
        {
            var ms = new MemoryStream(0x10000);
            using var sw = new StreamWriter(ms, Encoding.UTF8, 4096, true)
            {
                NewLine = "\n"
            };

            // validate the collecton isn't empty
            if (!_source.MoveNext() || _source.Current == null)
                throw new ArgumentException("Source IEnumerable is empty");

            // generate the serializer
            var serializer = GenerateSerializer(_source.Current);

            // serialize the collection
            do sw.WriteLine(serializer(_source.Current));
            while (_source.MoveNext() && _source.Current != null);

            ms.Position = 0;
            return ms;
        }

        private Func<object, string> GenerateSerializer(object current)
        {
            var type = current.GetType();
            var fields = type.GetFields();

            var expressions = new List<Expression>(fields.Length);
            var ownerParameter = Expression.Parameter(typeof(object), "o");

            foreach (var field in fields)
            {
                var isPrimaryKey = PrimaryKey == null && field.GetCustomAttribute<IndexAttribute>() != null;
                var foreignKeyAttr = field.GetCustomAttribute<ForeignKeyAttribute>();
                var commentAttr = field.GetCustomAttribute<CommentAttribute>();

                var fieldExpression = Expression.Field(Expression.Convert(ownerParameter, type), field);
                var fieldType = field.FieldType;

                if (isPrimaryKey)
                    PrimaryKey = field.Name;

                if (field.FieldType.IsArray)
                {
                    fieldType = fieldType.GetElementType();

                    // explode arrays into seperate fields
                    var cardinality = ((Array)field.GetValue(current)).Length;
                    var fieldName = field.Name + (cardinality > 1 ? "_{0}" : "");

                    for (var i = 0; i < cardinality; i++)
                    {
                        expressions.Add(EscapeOrConvert(Expression.ArrayIndex(fieldExpression, Expression.Constant(i))));
                        _columns[string.Format(fieldName, i + 1)] = new SqlColumn(fieldType, commentAttr?.Comment);
                    }
                }
                else
                {
                    expressions.Add(EscapeOrConvert(fieldExpression));
                    _columns[field.Name] = new SqlColumn(fieldType, commentAttr?.Comment);

                    if (foreignKeyAttr != null)
                        ForeignKey = (field.Name, foreignKeyAttr.ToString());
                }
            }

            // finally concat all the values into a single csv line
            var joinMethod = typeof(string).GetMethod("Join", new Type[] { typeof(char), typeof(string[]) });
            var arrayExpression = Expression.NewArrayInit(typeof(string), expressions);
            var concatExpression = Expression.Call(joinMethod, Expression.Constant(','), arrayExpression);
            var lambdaExpression = Expression.Lambda<Func<object, string>>(concatExpression, ownerParameter);

            return lambdaExpression.Compile();
        }

        #region Helpers

        private string GetHeaderRow()
        {
            var sb = new StringBuilder(0x100);
            foreach (var column in Columns)
                sb.Append($"{EscapeStringImpl(column.Key)},");

            return sb.ToString();
        }

        private static Expression EscapeOrConvert(Expression expression)
        {
            if (expression.Type == typeof(string))
                return Expression.Invoke(EscapeString, expression);
            else
                return Expression.Call(expression, "ToString", null);
        }

        private static readonly Expression<Func<string, string>> EscapeString = (s) => EscapeStringImpl(s);

        private static readonly char[] EscapeChars = new char[] { '"', ',', '\r', '\n' };

        private static string EscapeStringImpl(string value)
        {
            // starts/ends with space or has quoteable char
            var shouldQuote = value != "" &&
            (
                value[0] == ' ' || value[^1] == ' ' || value.IndexOfAny(EscapeChars) > -1
            );

            return shouldQuote ? '"' + value.Replace("\"", "\"\"") + '"' : value;
        }

        #endregion
    }
}
