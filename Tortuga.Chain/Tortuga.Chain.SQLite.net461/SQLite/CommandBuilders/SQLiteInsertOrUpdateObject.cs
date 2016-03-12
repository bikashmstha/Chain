﻿using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using Tortuga.Chain.Core;
using Tortuga.Chain.Materializers;
using Tortuga.Chain.Metadata;
using Tortuga.Chain.SQLite.SQLite.CommandBuilders;

namespace Tortuga.Chain.SQLite.CommandBuilders
{
    /// <summary>
    /// Class SQLiteInsertOrUpdateObject
    /// </summary>
    public class SQLiteInsertOrUpdateObject : SQLiteObjectCommand
    {
        private readonly InsertOrUpdateOptions m_Options;

        /// <summary>
        /// Initializes a new instance of the <see cref="SQLiteInsertOrUpdateObject"/> class.
        /// </summary>
        /// <param name="dataSource"></param>
        /// <param name="tableName"></param>
        /// <param name="argumentValue"></param>
        /// <param name="options"></param>
        public SQLiteInsertOrUpdateObject(SQLiteDataSourceBase dataSource, string tableName, object argumentValue, InsertOrUpdateOptions options)
            :base(dataSource, tableName, argumentValue)
        {
            m_Options = options;
        }

        /// <summary>
        /// Prepares the command for execution by generating any necessary SQL.
        /// </summary>
        /// <param name="materializer"></param>
        /// <returns><see cref="SQLiteExecutionToken" /></returns>
        public override ExecutionToken<SQLiteCommand, SQLiteParameter> Prepare(Materializer<SQLiteCommand, SQLiteParameter> materializer)
        {
            var parameters = new List<SQLiteParameter>();

            var where = WhereClause(parameters, m_Options.HasFlag(InsertOrUpdateOptions.UseKeyAttribute));
            var output = OutputClause(materializer, where);
            var update = UpdateClause(parameters, where);
            var insert = InsertClause(parameters);
            var sql = $"{update}; {insert}; {output};";

            return new SQLiteExecutionToken(DataSource, "Insert Or Update on " + TableName, sql, parameters);
        }

        private string UpdateClause(List<SQLiteParameter> parameters, string whereClause)
        {
            var set = SetClause(parameters);
            return $"UPDATE {TableName} {set} WHERE {whereClause}";
        }

        private string InsertClause(List<SQLiteParameter> parameters)
        {
            string columns;
            string values;
            ColumnsAndValuesClause(out columns, out values, parameters);
            return $"INSERT OR IGNORE INTO {TableName} {columns} {values}";
        }

        private void ColumnsAndValuesClause(out string columns, out string values, List<SQLiteParameter> parameters)
        {
            if (ArgumentDictionary != null)
            {
                var availableColumns = Metadata.GetKeysFor(ArgumentDictionary, GetKeysFilter.ThrowOnNoMatch | GetKeysFilter.UpdatableOnly);

                columns = "(" + string.Join(", ", availableColumns.Select(c => c.QuotedSqlName)) + ")";
                values = "VALUES (" + string.Join(", ", availableColumns.Select(c => c.SqlVariableName)) + ")";
                LoadDictionaryParameters(availableColumns, parameters);
            }
            else
            {
                var availableColumns = Metadata.GetPropertiesFor(ArgumentValue.GetType(),
                     GetPropertiesFilter.ThrowOnNoMatch | GetPropertiesFilter.UpdatableOnly | GetPropertiesFilter.ForInsert);

                columns = "(" + string.Join(", ", availableColumns.Select(c => c.Column.QuotedSqlName)) + ")";
                values = "VALUES (" + string.Join(", ", availableColumns.Select(c => c.Column.SqlVariableName)) + ")";
                LoadParameters(availableColumns, parameters);
            }
        }

        private string SetClause(List<SQLiteParameter> parameters)
        {
            if (ArgumentDictionary != null)
            {
                var filter = GetKeysFilter.ThrowOnNoMatch | GetKeysFilter.UpdatableOnly | GetKeysFilter.NonPrimaryKey;

                if (DataSource.StrictMode)
                    filter |= GetKeysFilter.ThrowOnMissingColumns;

                var availableColumns = Metadata.GetKeysFor(ArgumentDictionary, filter);

                var set = "SET " + string.Join(", ", availableColumns.Select(c => $"{c.QuotedSqlName} = {c.SqlVariableName}"));
                LoadDictionaryParameters(availableColumns, parameters);
                return set;
            }
            else
            {
                var filter = GetPropertiesFilter.ThrowOnNoMatch | GetPropertiesFilter.UpdatableOnly;

                if (m_Options.HasFlag(InsertOrUpdateOptions.UseKeyAttribute))
                    filter |= GetPropertiesFilter.ObjectDefinedNonKey;
                else
                    filter |= GetPropertiesFilter.NonPrimaryKey;

                if (DataSource.StrictMode)
                    filter |= GetPropertiesFilter.ThrowOnMissingColumns;

                var availableColumns = Metadata.GetPropertiesFor(ArgumentValue.GetType(), filter);

                var set = "SET " + string.Join(", ", availableColumns.Select(c => $"{c.Column.QuotedSqlName} = {c.Column.SqlVariableName}"));
                LoadParameters(availableColumns, parameters);
                return set;
            }
        }
    }
}