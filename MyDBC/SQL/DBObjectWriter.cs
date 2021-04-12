using MyDBC.Definition;
using MyDBC.Helpers;
using MySqlConnector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading.Tasks;

namespace MyDBC.SQL
{
    public sealed class DBObjectWriter
    {
        private static readonly Dictionary<string, (string Col, string Ref)> ForeignKeys = new();

        public static async Task WriteToServer(Options options, DefinitionBuilder builder, IEnumerable source)
        {
            using var conn = new MySqlConnection(options.ConnectionString);
            var dbLoader = new BulkLoader(builder.Name, source);
            var fileName = await dbLoader.ExportToFile();

            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            // create the table
            var createTableQuery = GetCreateTableStatement(dbLoader, builder, options.DropAndCreate);
            using var cmd = new MySqlCommand(createTableQuery, conn);
            await cmd.ExecuteNonQueryAsync();

            // bulk insert
            await dbLoader.ExportToMySql(conn, fileName);
            await conn.CloseAsync();

            if (dbLoader.ForeignKey != default)
                ForeignKeys.Add(builder.Name, dbLoader.ForeignKey);

            Console.WriteLine($"Exported {builder.Name} to SQL");
        }

        public static async Task WriteForeignKeys(Options options)
        {
            if (ForeignKeys.Count == 0)
                return;

            using var conn = new MySqlConnection(options.ConnectionString);

            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            // alter the tables
            var alterTableQuery = GetAlterTableStatement();
            using var cmd = new MySqlCommand(alterTableQuery, conn);
            await cmd.ExecuteNonQueryAsync();

            await conn.CloseAsync();
        }

        private static string GetCreateTableStatement(BulkLoader loader, DefinitionBuilder builder, bool dropAndCreate)
        {
            var sb = new StringBuilder(0x200);

            // append drop if applicable
            if (dropAndCreate)
                sb.AppendLine($"DROP TABLE IF EXISTS `{builder.Name}`;");

            // build table structure
            sb.Append($"CREATE TABLE `{builder.Name}` (");

            foreach (var column in loader.Columns)
            {
                if (!DataTypeMap.TryGetValue(column.Value.Type, out string dataType))
                    throw new ArgumentException($"Unable to map {column.Value} to a MySQL type");

                // append this field
                sb.Append($" `{column.Key}` {dataType} NOT NULL,");

                // insert default field value
                if (column.Key == loader.PrimaryKey)
                    sb.Insert(sb.Length - 1, " AUTO_INCREMENT"); // PK
                else if (dataType != "TEXT")
                    sb.Insert(sb.Length - 1, " DEFAULT '0'"); // numeric

                if (!string.IsNullOrWhiteSpace(column.Value.Comment))
                    sb.Insert(sb.Length - 1, $" COMMENT '{column.Value.Comment.Sql()}'");
            }

            // remove the trailing comma
            sb[^1] = ' ';

            // append primary key
            if (!string.IsNullOrEmpty(loader.PrimaryKey))
                sb.AppendLine($", PRIMARY KEY ({loader.PrimaryKey})");

            // add engine and charset
            sb.AppendLine(") ENGINE=InnoDB DEFAULT CHARSET=utf8;");

            return sb.ToString();
        }

        private static string GetAlterTableStatement()
        {
            var sb = new StringBuilder(0x200);

            foreach (var table in ForeignKeys)
            {
                sb.Append($"ALTER TABLE `{table.Key}`");
                sb.AppendLine($"ADD FOREIGN KEY (`{table.Value.Col}`) REFERENCES {table.Value.Ref};");
            }

            return sb.ToString();
        }

        private static readonly Dictionary<Type, string> DataTypeMap = new()
        {
            [typeof(ulong)] = "BIGINT UNSIGNED",
            [typeof(long)] = "BIGINT",
            [typeof(float)] = "DOUBLE",
            [typeof(int)] = "INT",
            [typeof(uint)] = "INT UNSIGNED",
            [typeof(short)] = "SMALLINT",
            [typeof(ushort)] = "SMALLINT UNSIGNED",
            [typeof(sbyte)] = "TINYINT",
            [typeof(byte)] = "TINYINT UNSIGNED",
            [typeof(string)] = "TEXT",
        };
    }
}
