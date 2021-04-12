using System.IO;
using System.Threading.Tasks;

namespace MyDBC.DBC
{
    internal class LocalDBCProvider
    {
        private readonly string Directory;

        public LocalDBCProvider(string directory)
        {
            Directory = directory;
        }

        public async Task<Stream> StreamForTableName(string tableName)
        {
            var filename = Path.Combine(Directory, tableName + ".db2");
            if (!File.Exists(filename))
                filename = Path.ChangeExtension(filename, ".dbc");
            if (!File.Exists(filename))
                throw new FileNotFoundException($"Unable to load {tableName}");

            return await Task.FromResult(File.OpenRead(filename));
        }
    }
}
