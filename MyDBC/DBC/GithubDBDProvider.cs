using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MyDBC.DBC
{
    internal class GithubDBDProvider
    {
        private const string GitTreeApi = "https://api.github.com/repos/wowdev/WoWDBDefs/git/trees/";
        private const string BaseAddress = "https://raw.githubusercontent.com/wowdev/WoWDBDefs/master/definitions/";

        private readonly HttpClient Client = new();
        private readonly IReadOnlyDictionary<string, string> UriLookup;

        public GithubDBDProvider()
        {
            Client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            Client.DefaultRequestHeaders.Add("User-Agent", "Barncastle/MyDBC");

            // git is casesensitive and filenames are unreliable
            // so generate a lookup of dbd -> raw git url via git's api
            UriLookup = TryLoadUriLookup().Result;
        }

        public async Task<Stream> StreamForTableName(string tableName)
        {
            if (!UriLookup.TryGetValue(tableName, out var dbdName))
                dbdName = $"{BaseAddress}{tableName}.dbd"; // fallback url

            var buffer = await Client.GetByteArrayAsync(dbdName);
            return new MemoryStream(buffer);
        }

        private async Task<IReadOnlyDictionary<string, string>> TryLoadUriLookup()
        {
            var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // find the definition folder sha and the definition file list
                var tree = await Client.GetFromJsonAsync<Tree>(GitTreeApi + "master");
                var definition = Array.Find(tree.Elements, e => e.Path == "definitions");
                var definitionTree = await Client.GetFromJsonAsync<Tree>(definition.Url);

                // load all DBD files and their raw urls                    
                foreach (var ele in definitionTree.Elements)
                    lookup[Path.GetFileNameWithoutExtension(ele.Path)] = $"{BaseAddress}{ele.Path}";

                Console.WriteLine("Loaded DBD lookup");
            }
            catch
            {
                Console.WriteLine("Unable to load DBD lookup");
            }

            return lookup;
        }

        private class Tree
        {
            [JsonPropertyName("tree")]
            public TreeElement[] Elements { get; set; }

            public class TreeElement
            {
                [JsonPropertyName("path")]
                public string Path { get; set; }

                [JsonPropertyName("url")]
                public string Url { get; set; }
            }
        }
    }
}
