using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MyDBC.SQL
{
    public sealed class DBObjectReader
    {
        public static async Task<List<T>> ReadFromServer<T>(string connectionString, string table)
        {
            var resultSet = new List<T>(0x10000);

            using var conn = new MySqlConnection(connectionString);
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            using var cmd = new MySqlCommand($"SELECT * FROM `{table}`", conn);
            using var rdr = await cmd.ExecuteReaderAsync();

            var serializer = GenerateSerializer<T>(rdr);

            while (rdr.HasRows && await rdr.ReadAsync())
                resultSet.Add(serializer(rdr));

            return resultSet;
        }


        private static Func<MySqlDataReader, T> GenerateSerializer<T>(MySqlDataReader reader)
        {
            var type = typeof(T);
            var fields = type.GetFields();

            var expressions = new List<MemberAssignment>(fields.Length);
            var readerParameter = Expression.Parameter(typeof(MySqlDataReader), "r");
            var ownerParameter = Expression.Parameter(typeof(T), "o");

            // calls the MySqlDataReader.GetFieldValue method
            Expression ReadValue(Type t, int index) => Expression.Call(readerParameter, "GetFieldValue", new[] { t }, Expression.Constant(index));

            foreach (var column in GetSchema(reader))
            {
                var field = fields.Single(x => x.Name.Equals(column.Key, StringComparison.OrdinalIgnoreCase));
                var fieldType = field.FieldType;

                if (field.FieldType.IsArray)
                {
                    fieldType = fieldType.GetElementType();

                    // store a GetFieldValue call for each array item
                    var readExpressions = new Expression[column.Value.Length];
                    for (int i = 0; i < readExpressions.Length; i++)
                        readExpressions[i] = ReadValue(fieldType, column.Value[i]);

                    // create and bind a new array from the above
                    expressions.Add(Expression.Bind(field, Expression.NewArrayInit(fieldType, readExpressions)));
                }
                else
                {
                    // bind a direct GetFieldValue call
                    expressions.Add(Expression.Bind(field, ReadValue(fieldType, column.Value[0])));
                }
            }

            // create a new T: MemberInit can always be reduced
            var initExpression = Expression.MemberInit(Expression.New(typeof(T)), expressions).Reduce();
            var lambdaExpression = Expression.Lambda<Func<MySqlDataReader, T>>(initExpression, readerParameter);

            return lambdaExpression.Compile();
        }

        /// <summary>
        /// Creates a schema map that maps indicies to fields 
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private static Dictionary<string, int[]> GetSchema(MySqlDataReader reader)
        {
            var ordinalRemover = new Regex("(_\\d+$)", RegexOptions.Compiled);

            return Enumerable.Range(0, reader.FieldCount)
                             .Select(i => (Name: reader.GetName(i), Ordinal: i))
                             .OrderBy(n => n.Name)
                             .GroupBy(n => ordinalRemover.Replace(n.Name, ""))
                             .ToDictionary(n => n.Key, n => n.Select(x => x.Ordinal).ToArray());
        }
    }
}
