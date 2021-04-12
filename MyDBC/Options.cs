using CommandLine;
using System;

namespace MyDBC
{
    public class Options
    {
        public ExportType ExportType { get; private set; }

        [Option('d', "directory", HelpText = "Location of the DB files")]
        public string Directory { get; set; } = System.IO.Directory.GetCurrentDirectory();

        [Option('b', "build", HelpText = "Client build number if targeting pre-Legion e.g. 0.5.3.3368")]
        public string Build { get; set; }

        #region SQL Options

        [Option('c', "connection", HelpText = "SQL connection string")]
        public string ConnectionString { get; set; }

        [Option("drop", HelpText = "Drops existing tables if conflicts occur")]
        public bool DropAndCreate { get; set; }

        [Option("fk", HelpText = "Exports Relationship fields as foreign keys")]
        public bool ExportForeignKeys { get; set; }

        #endregion

        #region CSV Options

        [Option('o', "output", HelpText = "CSV export directory")]
        public string OutputDirectory { get; set; }

        #endregion

        public void Validate()
        {
            if (!System.IO.Directory.Exists(Directory))
                throw new ArgumentException("Directory not found", nameof(Directory));

            if (!string.IsNullOrWhiteSpace(ConnectionString))
            {
                ExportType |= ExportType.SQL;

                // append file import conn string setting
                if (!ConnectionString.Contains("AllowLoadLocalInfile", StringComparison.OrdinalIgnoreCase))
                    ConnectionString += "AllowLoadLocalInfile=true;";
            }

            if(!string.IsNullOrWhiteSpace(OutputDirectory))
                ExportType |= ExportType.CSV;

            if (ExportType == 0)
                throw new ArgumentException("Either ConnectionString or FileName must be set");
        }
    }

    [Flags]
    public enum ExportType
    {
        SQL = 1,
        CSV = 2
    }
}
