﻿using DatabaseSchemaReader.DataSchema;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using DatabaseSchemaReader.ProviderSchemaReaders.ConnectionContext;

namespace DatabaseSchemaReader.ProviderSchemaReaders.Databases.Oracle
{
    class Columns : OracleSqlExecuter<DatabaseColumn>
    {
        private readonly string _tableName;

        public Columns(int? commandTimeout, string owner, string tableName) : base(commandTimeout, owner)
        {
            _tableName = tableName;
            Owner = owner;
            Sql = @"SELECT OWNER,
  TABLE_NAME,
  COLUMN_NAME,
  COLUMN_ID      AS ordinal_position,
  DATA_TYPE,
  CHAR_LENGTH,
  DATA_LENGTH,
  DATA_PRECISION,
  DATA_SCALE,
  NULLABLE,
  DATA_DEFAULT
FROM ALL_TAB_COLUMNS
WHERE
TABLE_NAME NOT LIKE 'BIN$%'
AND (OWNER = :OWNER OR :OWNER IS NULL)
AND OWNER NOT IN ('SYS', 'SYSMAN', 'CTXSYS', 'MDSYS', 'OLAPSYS', 'ORDSYS', 'OUTLN', 'WKSYS', 'WMSYS', 'XDB', 'ORDPLUGINS', 'SYSTEM')
AND (TABLE_NAME  = :TABLENAME OR :TABLENAME IS NULL)
ORDER BY OWNER, TABLE_NAME, COLUMN_ID";
        }

        public IList<DatabaseColumn> Execute(IConnectionAdapter connectionAdapter)
        {
            ExecuteDbReader(connectionAdapter);
            return Result;
        }

        protected override void AddParameters(DbCommand command)
        {
            EnsureOracleBindByName(command);
            AddDbParameter(command, "Owner", Owner);
            AddDbParameter(command, "TableName", _tableName);
        }

        protected override void Mapper(IDataRecord record)
        {
            var owner = record.GetString("OWNER");
            var tableName = record.GetString("TABLE_NAME");
            var name = record.GetString("COLUMN_NAME");

            // 19/06/2024 Specify the size of text fields
            var dbDataType = record.GetString("DATA_TYPE");
            var charLength = record.GetNullableInt("CHAR_LENGTH");
            var dataLength = record.GetNullableInt("DATA_LENGTH");
            var dbDataTypeWithLength = dbDataType;
            if (dbDataType == "NCHAR" || 
                dbDataType == "NVARCHAR2" || 
                dbDataType == "VARCHAR2")
                dbDataTypeWithLength = dbDataType + "(" + charLength + ")";
            // For the moment, DATA_LENGTH is not the actual length of NUMBER
            //if (dbDataType == "NUMBER")
            //    dbDataTypeWithLength = dbDataType + "(" + dataLength + ")";

            var col = new DatabaseColumn
            {
                SchemaOwner = owner,
                TableName = tableName,
                Name = name,
                Ordinal = record.GetNullableInt("ordinal_position").GetValueOrDefault(),
                DbDataType = dbDataTypeWithLength,
                Length = charLength,
                Precision = record.GetNullableInt("DATA_PRECISION"),
                Scale = record.GetNullableInt("DATA_SCALE"),
                Nullable = record.GetBoolean("NULLABLE"),
            };
            if (col.Length < 1)
            {
                col.Length = dataLength;
            }
            var d = record.GetString("DATA_DEFAULT");
            if (!string.IsNullOrEmpty(d))
            {
                d = d.Trim('\n', ' ', '\'', '=');
            }
            col.DefaultValue = d;
            Result.Add(col);
        }
    }
}