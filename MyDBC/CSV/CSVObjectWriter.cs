using MyDBC.Definition;
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;

namespace MyDBC.CSV
{
    public sealed class CSVObjectWriter
    {
        public static async Task WriteToFile(Options options, DefinitionBuilder builder, IEnumerable source)
        {
            var filename = Path.Combine(options.OutputDirectory, builder.Name + ".csv");
            var directory = new DirectoryInfo(options.OutputDirectory);

            if (!directory.Exists && directory.Parent != null)
                directory.Create();

            var dbLoader = new BulkLoader(builder.Name, source);
            await dbLoader.ExportToFile(filename);

            Console.WriteLine($"Exported {builder.Name} to CSV");
        }
    }
}
