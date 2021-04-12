using DBDefsLib;
using DBFileReaderLib;
using DBFileReaderLib.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

namespace MyDBC.Definition
{
    public class DefinitionBuilder
    {
        private static ModuleBuilder ModuleBuilder;

        public readonly string Name;
        public readonly string Build;
        public readonly Locale Locale;

        private int LocStringSize = 1;

        public DefinitionBuilder(string name, string build = null, Locale locale = Locale.None)
        {
            Name = name;
            Build = build;
            Locale = locale;

            if (ModuleBuilder == null)
            {
                var assemblyName = new AssemblyName("DBCDefinitons");
                var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
                ModuleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);
            }
        }

        public Type Generate(DBReader dbcReader, Stream dbd, Dictionary<string, string> dbs)
        {
            var dbdReader = new DBDReader();
            var databaseDefinition = dbdReader.Read(dbd);

            Structs.VersionDefinitions? versionDefinition = null;
            if (!string.IsNullOrWhiteSpace(Build))
            {
                var dbBuild = new Build(Build);
                LocStringSize = GetLocStringSize(dbBuild);
                Utils.GetVersionDefinitionByBuild(databaseDefinition, dbBuild, out versionDefinition);
            }

            if (versionDefinition == null && dbcReader.LayoutHash != 0)
            {
                var layoutHash = dbcReader.LayoutHash.ToString("X8");
                Utils.GetVersionDefinitionByLayoutHash(databaseDefinition, layoutHash, out versionDefinition);
            }

            if (versionDefinition == null)
                throw new FileNotFoundException("No definition found for this file.");

            if (LocStringSize > 1 && (int)Locale >= LocStringSize)
                throw new FormatException("Invalid locale for this file.");

            var typeBuilder = ModuleBuilder.DefineType(Name, TypeAttributes.Public);
            var fields = versionDefinition.Value.definitions;
            var localiseStrings = Locale != Locale.None;

            foreach (var fieldDefinition in fields)
            {
                var columnInfo = databaseDefinition.columnDefinitions[fieldDefinition.name];
                var isLocalisedString = columnInfo.type == "locstring" && LocStringSize > 1;

                var fieldType = FieldDefinitionToType(fieldDefinition, columnInfo, localiseStrings);
                var field = typeBuilder.DefineField(fieldDefinition.name, fieldType, FieldAttributes.Public);

                if (fieldDefinition.isID)
                    AddAttribute<IndexAttribute>(field, fieldDefinition.isNonInline);

                if (fieldDefinition.arrLength > 1)
                    AddAttribute<CardinalityAttribute>(field, fieldDefinition.arrLength);

                if (fieldDefinition.isRelation && fieldDefinition.isNonInline)
                {
                    var metaDataFieldType = FieldDefinitionToType(fieldDefinition, columnInfo, localiseStrings);
                    AddAttribute<NonInlineRelationAttribute>(field, metaDataFieldType);
                }

                if (isLocalisedString)
                {
                    if (localiseStrings)
                    {
                        AddAttribute<LocaleAttribute>(field, (int)Locale, LocStringSize);
                    }
                    else
                    {
                        AddAttribute<CardinalityAttribute>(field, LocStringSize);
                        typeBuilder.DefineField(fieldDefinition.name + "_mask", typeof(uint), FieldAttributes.Public);
                    }
                }

                // export comments
                if (!string.IsNullOrEmpty(columnInfo.comment))
                    AddAttribute<CommentAttribute>(field, columnInfo.comment);

                // only add foreign keys that can be created
                if (fieldDefinition.isRelation && 
                    columnInfo.foreignTable != null && 
                    dbs.ContainsKey(columnInfo.foreignTable))
                    AddAttribute<ForeignKeyAttribute>(field, columnInfo.foreignTable, columnInfo.foreignColumn);
            }

            return typeBuilder.CreateTypeInfo();
        }

        private static int GetLocStringSize(Build build)
        {
            if (build.expansion >= 4 || build.build > 12340) // post wotlk
                return 1;
            else if (build.build >= 6692) // tbc - wotlk
                return 16;
            else
                return 8; // alpha - vanilla
        }

        private static void AddAttribute<T>(FieldBuilder field, params object[] parameters) where T : Attribute
        {
            var constructorParameters = Array.ConvertAll(parameters, x => x.GetType());
            var constructorInfo = typeof(T).GetConstructor(constructorParameters);
            var attributeBuilder = new CustomAttributeBuilder(constructorInfo, parameters);
            field.SetCustomAttribute(attributeBuilder);
        }

        private Type FieldDefinitionToType(Structs.Definition field, Structs.ColumnDefinition column, bool localiseStrings)
        {
            var isArray = field.arrLength != 0;

            if (field.isRelation)
                return isArray ? typeof(int[]) : typeof(int);

            switch (column.type)
            {
                case "int":
                    {
                        var type = field.size switch
                        {
                            8 => field.isSigned ? typeof(sbyte) : typeof(byte),
                            16 => field.isSigned ? typeof(short) : typeof(ushort),
                            32 => field.isSigned ? typeof(int) : typeof(uint),
                            64 => field.isSigned ? typeof(long) : typeof(ulong),
                            _ => throw new NotImplementedException("Unhandled field size of " + field.size)
                        };

                        return isArray ? type.MakeArrayType() : type;
                    }
                case "string":
                    {
                        return isArray ? typeof(string[]) : typeof(string);
                    }
                case "locstring":
                    {
                        if (isArray && LocStringSize > 1)
                            throw new NotSupportedException("Localised string arrays are not supported");

                        return (!localiseStrings && LocStringSize > 1) || isArray ? typeof(string[]) : typeof(string);
                    }
                case "float":
                    {
                        return isArray ? typeof(float[]) : typeof(float);
                    }
                default:
                    throw new ArgumentException("Unable to construct C# type from " + column.type);
            }
        }
    }

    public enum Locale
    {
        None = -1,
        EnUS = 0,
        EnGB = EnUS,
        KoKR = 1,
        FrFR = 2,
        DeDE = 3,
        EnCN = 4,
        ZhCN = EnCN,
        EnTW = 5,
        ZhTW = EnTW,
        EsES = 6,
        EsMX = 7,
        /* Available from TBC 2.1.0.6692 */
        RuRU = 8,
        PtPT = 10,
        PtBR = PtPT,
        ItIT = 11,
    }
}
