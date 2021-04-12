using CommandLine;
using DBFileReaderLib;
using MyDBC.CSV;
using MyDBC.DBC;
using MyDBC.Definition;
using MyDBC.Helpers;
using MyDBC.SQL;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MyDBC
{
    class Program
    {
        private static async Task Main(string[] args)
        {
            Utils.OverrideNumberFormat();

            using var parser = new Parser(s =>
            {
                s.HelpWriter = Console.Error;
                s.CaseInsensitiveEnumValues = true;
                s.AutoVersion = false;
            });

            await parser
                .ParseArguments<Options>(args)
                .MapResult(Run, Task.FromResult);

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static async Task Run(Options options)
        {
            options.Validate();

            var dbdProvider = new GithubDBDProvider();
            var dbcProvider = new LocalDBCProvider(options.Directory);

            var dbs = Utils.GetFiles(options.Directory, "*.db2|*.dbc")
                           .ToDictionary(Path.GetFileNameWithoutExtension);

            foreach (var db in dbs)
            {
                var dbcStream = await dbcProvider.StreamForTableName(db.Key);
                var dbdStream = await dbdProvider.StreamForTableName(db.Key);

                var dbReader = new DBReader(dbcStream);
                var builder = new DefinitionBuilder(db.Key, options.Build);
                var definition = builder.Generate(dbReader, dbdStream, dbs);
                var storage = dbReader.GetRecords(definition);

                if (storage.Count == 0)
                {
                    Console.WriteLine($"Skipping {db.Key} - empty");
                    continue;
                }

                if (options.ExportType.HasFlag(ExportType.SQL))
                    await DBObjectWriter.WriteToServer(options, builder, storage.Values);
                if (options.ExportType.HasFlag(ExportType.CSV))
                    await CSVObjectWriter.WriteToFile(options, builder, storage.Values);
            }

            // append foreign keys to the database
            if (options.ExportType.HasFlag(ExportType.SQL) && options.ExportForeignKeys)
            {
                Console.WriteLine("Generating foreign keys");
                await DBObjectWriter.WriteForeignKeys(options);
            }
        }
    }
}
