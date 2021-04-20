# MyDBC

A command line tool for exporting World of Warcraft's various DB files to SQL and/or CSV. This tool is powered by [DBCD](https://github.com/wowdev/DBCD) and [WoWDBDefs](https://github.com/wowdev/WoWDBDefs) so supports all DB formats which are almost all named.

#### Project Prerequisites
- .Net Core 5

#### Arguments

| Long Name | Short Name | Description |
| ------- | :---- | ----- |
| --directory | --d | Directory containing DB files, defaults to the current one |
| --build | --b | Client build string e.g. "0.5.3.3368" (see notes) |
| --connection | --c | SQL connection string for SQL exports |
| --output | --o | Output directory for CSV exports |
| --drop |  | Drops and recreates tables (SQL) |
| --fk |  | Exports [Relationship](https://github.com/wowdev/WoWDBDefs#column-annotations) fields as foreign keys (SQL) |
| --help |  | Shows this table |



#### Usage

Exporting the current directory to SQL with foreign keys and table drop:

`MyDBC.exe --c "Server=localhost;Database=test;Uid=root;Pwd=;" --drop --fk`

Exporting the current directory to CSV:

`MyDBC.exe --o "D:\Test"`

#### Notes

- All tables and CSV files will be named as per their source filename.
- `--connection` and `--output` can be used simultaneously.
- `--build` is required for all DBs before Legion so that DBCD can load the correct structure.
- The tool uses MySQL's `LOAD DATA` command which by default, appends to an existing table. You will need to use the `--drop` argument if this is not desired.
- Unfortunately WoWDBDef "foreign keys" are not supported, only "relations", due to them not lending themselves well to MySQL's optional foreign key constraints. In short; WoW uses `0` whereas MySQL uses `NULL` to dictate a missing reference.
