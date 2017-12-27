﻿using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tortuga.Chain.CommandBuilders;
using Tortuga.Chain.Core;
using Tortuga.Chain.Materializers;
using Tortuga.Chain.Metadata;

namespace Tortuga.Chain.MySql.CommandBuilders
{

    /// <summary>
    /// Class MySqlTableOrView
    /// </summary>
    public class MySqlTableOrView : TableDbCommandBuilder<MySqlCommand, MySqlParameter, MySqlLimitOption>
    {
        readonly TableOrViewMetadata<MySqlObjectName, MySqlDbType> m_Table;
        object m_ArgumentValue;
        FilterOptions m_FilterOptions;
        object m_FilterValue;
        MySqlLimitOption m_LimitOptions;
        int? m_Seed;
        string m_SelectClause;
        int? m_Skip;
        IEnumerable<SortExpression> m_SortExpressions = Enumerable.Empty<SortExpression>();
        int? m_Take;
        string m_WhereClause;
        /// <summary>
        /// Initializes a new instance of the <see cref="MySqlTableOrView" /> class.
        /// </summary>
        /// <param name="dataSource">The data source.</param>
        /// <param name="tableOrViewName">Name of the table or view.</param>
        /// <exception cref="ArgumentException"></exception>
        public MySqlTableOrView(MySqlDataSourceBase dataSource, MySqlObjectName tableOrViewName) :
            base(dataSource)
        {
            if (tableOrViewName == MySqlObjectName.Empty)
                throw new ArgumentException($"{nameof(tableOrViewName)} is empty", nameof(tableOrViewName));

            m_Table = DataSource.DatabaseMetadata.GetTableOrView(tableOrViewName);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MySqlTableOrView" /> class.
        /// </summary>
        /// <param name="dataSource">The data source.</param>
        /// <param name="tableOrViewName">Name of the table or view.</param>
        /// <param name="filterValue">The filter value.</param>
        /// <exception cref="ArgumentException"></exception>
        public MySqlTableOrView(MySqlDataSourceBase dataSource, MySqlObjectName tableOrViewName, object filterValue, FilterOptions filterOptions = FilterOptions.None) :
            base(dataSource)
        {
            if (tableOrViewName == MySqlObjectName.Empty)
                throw new ArgumentException($"{nameof(tableOrViewName)} is empty", nameof(tableOrViewName));

            m_FilterValue = filterValue;
            m_Table = DataSource.DatabaseMetadata.GetTableOrView(tableOrViewName);
            m_FilterOptions = filterOptions;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MySqlTableOrView"/> class.
        /// </summary>
        /// <param name="dataSource">The data source.</param>
        /// <param name="tableOrViewName">Name of the table or view.</param>
        /// <param name="whereClause">The where clause.</param>
        /// <param name="argumentValue">The argument value.</param>
        /// <exception cref="ArgumentException"></exception>
        public MySqlTableOrView(MySqlDataSourceBase dataSource, MySqlObjectName tableOrViewName, string whereClause, object argumentValue)
            : base(dataSource)
        {
            if (tableOrViewName == MySqlObjectName.Empty)
                throw new ArgumentException($"{nameof(tableOrViewName)} is empty", nameof(tableOrViewName));

            m_WhereClause = whereClause;
            m_ArgumentValue = argumentValue;
            m_Table = DataSource.DatabaseMetadata.GetTableOrView(tableOrViewName);
        }

        /// <summary>
        /// Gets the data source.
        /// </summary>
        /// <value>The data source.</value>
        public new MySqlDataSourceBase DataSource
        {
            get { return (MySqlDataSourceBase)base.DataSource; }
        }

        /// <summary>
        /// Returns the row count using a <c>SELECT COUNT_BIG(*)</c> style query.
        /// </summary>
        /// <returns></returns>
        public override ILink<long> AsCount()
        {
            throw new NotImplementedException("AsCount");
        }

        /// <summary>
        /// Returns the row count for a given column. <c>SELECT COUNT_BIG(columnName)</c>
        /// </summary>
        /// <param name="columnName">Name of the column.</param>
        /// <param name="distinct">if set to <c>true</c> use <c>SELECT COUNT_BIG(DISTINCT columnName)</c>.</param>
        /// <returns></returns>
        public override ILink<long> AsCount(string columnName, bool distinct = false)
        {
            throw new NotImplementedException("AsCount(string, bool)");
        }

        /// <summary>
        /// Prepares the command for execution by generating any necessary SQL.
        /// </summary>
        /// <param name="materializer">The materializer.</param>
        /// <returns>
        /// ExecutionToken&lt;TCommand&gt;.
        /// </returns>
        public override CommandExecutionToken<MySqlCommand, MySqlParameter> Prepare(Materializer<MySqlCommand, MySqlParameter> materializer)
        {
            if (materializer == null)
                throw new ArgumentNullException(nameof(materializer), $"{nameof(materializer)} is null.");

            var sqlBuilder = m_Table.CreateSqlBuilder(StrictMode);
            sqlBuilder.ApplyRulesForSelect(DataSource);

            if (m_SelectClause == null)
                sqlBuilder.ApplyDesiredColumns(materializer.DesiredColumns());

            //Support check
            if (!Enum.IsDefined(typeof(MySqlLimitOption), m_LimitOptions))
                throw new NotSupportedException($"PostgreSQL does not support limit option {(LimitOptions)m_LimitOptions}");

            //Validation
            if (m_Skip < 0)
                throw new InvalidOperationException($"Cannot skip {m_Skip} rows");

            if (m_Skip > 0 && m_LimitOptions != MySqlLimitOption.Rows)
                throw new InvalidOperationException($"Cannot perform a Skip operation with limit option {m_LimitOptions}");

            if (m_Take <= 0)
                throw new InvalidOperationException($"Cannot take {m_Take} rows");

            if (m_LimitOptions == MySqlLimitOption.RandomSampleRows && m_SortExpressions.Any())
                throw new InvalidOperationException($"Cannot perform random sampling when sorting.");


            //SQL Generation
            List<MySqlParameter> parameters;
            var sql = new StringBuilder();

            if (m_SelectClause != null)
                sql.Append($"SELECT {m_SelectClause} ");
            else
                sqlBuilder.BuildSelectClause(sql, "SELECT ", null, null);

            sql.Append(" FROM " + m_Table.Name);


            if (m_FilterValue != null)
            {
                sql.Append(" WHERE (" + sqlBuilder.ApplyFilterValue(m_FilterValue, m_FilterOptions) + ")");
                sqlBuilder.BuildSoftDeleteClause(sql, " AND (", DataSource, ") ");

                parameters = sqlBuilder.GetParameters();
            }
            else if (!string.IsNullOrWhiteSpace(m_WhereClause))
            {
                sql.Append(" WHERE (" + m_WhereClause + ")");
                sqlBuilder.BuildSoftDeleteClause(sql, " AND (", DataSource, ") ");

                parameters = SqlBuilder.GetParameters<MySqlParameter>(m_ArgumentValue);
                parameters.AddRange(sqlBuilder.GetParameters());
            }
            else
            {
                sqlBuilder.BuildSoftDeleteClause(sql, " WHERE ", DataSource, null);
                parameters = sqlBuilder.GetParameters();
            }

            switch (m_LimitOptions)
            {
                case MySqlLimitOption.RandomSampleRows:
                    sql.Append(" ORDER BY RAND() ");
                    break;

                default:
                    sqlBuilder.BuildOrderByClause(sql, " ORDER BY ", m_SortExpressions, null);
                    break;
            }

            switch (m_LimitOptions)
            {
                case MySqlLimitOption.Rows:

                    if (m_Skip.HasValue)
                    {
                        sql.Append(" LIMIT @offset_row_count_expression, @limit_row_count_expression");
                        parameters.Add(new MySqlParameter("@offset_row_count_expression", m_Skip));
                        parameters.Add(new MySqlParameter("@limit_row_count_expression", m_Take));
                    }
                    else
                    {
                        sql.Append(" LIMIT @limit_row_count_expression");
                        parameters.Add(new MySqlParameter("@limit_row_count_expression", m_Take));
                    }

                    break;
            }

            sql.Append(";");

            return new MySqlCommandExecutionToken(DataSource, "Query " + m_Table.Name, sql.ToString(), parameters);
        }


        /// <summary>
        /// Returns the column associated with the column name.
        /// </summary>
        /// <param name="columnName">Name of the column.</param>
        /// <returns></returns>
        /// <remarks>
        /// If the column name was not found, this will return null
        /// </remarks>
        public override ColumnMetadata TryGetColumn(string columnName)
        {
            return m_Table.Columns.TryGetColumn(columnName);
        }

        /// <summary>
        /// Adds (or replaces) the filter on this command builder.
        /// </summary>
        /// <param name="filterValue">The filter value.</param>
        /// <param name="filterOptions">The filter options.</param>
        /// <returns>TableDbCommandBuilder&lt;MySqlCommand, MySqlParameter, MySqlLimitOption&gt;.</returns>
        public override TableDbCommandBuilder<MySqlCommand, MySqlParameter, MySqlLimitOption> WithFilter(object filterValue, FilterOptions filterOptions = FilterOptions.None)
        {
            m_FilterValue = filterValue;
            m_WhereClause = null;
            m_ArgumentValue = null;
            m_FilterOptions = filterOptions;
            return this;
        }

        /// <summary>
        /// Adds (or replaces) the filter on this command builder.
        /// </summary>
        /// <param name="whereClause">The where clause.</param>
        /// <returns>TableDbCommandBuilder&lt;MySqlCommand, MySqlParameter, MySqlLimitOption&gt;.</returns>
        public override TableDbCommandBuilder<MySqlCommand, MySqlParameter, MySqlLimitOption> WithFilter(string whereClause)
        {
            m_FilterValue = null;
            m_WhereClause = whereClause;
            m_ArgumentValue = null;
            return this;
        }

        /// <summary>
        /// Adds (or replaces) the filter on this command builder.
        /// </summary>
        /// <param name="whereClause">The where clause.</param>
        /// <param name="argumentValue">The argument value.</param>
        /// <returns>TableDbCommandBuilder&lt;MySqlCommand, MySqlParameter, MySqlLimitOption&gt;.</returns>
        public override TableDbCommandBuilder<MySqlCommand, MySqlParameter, MySqlLimitOption> WithFilter(string whereClause, object argumentValue)
        {
            m_FilterValue = null;
            m_WhereClause = whereClause;
            m_ArgumentValue = argumentValue;
            return this;
        }

        /// <summary>
        /// Adds sorting to the command builder.
        /// </summary>
        /// <param name="sortExpressions">The sort expressions.</param>
        /// <returns></returns>
        public override TableDbCommandBuilder<MySqlCommand, MySqlParameter, MySqlLimitOption> WithSorting(IEnumerable<SortExpression> sortExpressions)
        {
            if (sortExpressions == null)
                throw new ArgumentNullException(nameof(sortExpressions), $"{nameof(sortExpressions)} is null.");

            m_SortExpressions = sortExpressions;
            return this;
        }

        /// <summary>
        /// Adds limits to the command builder.
        /// </summary>
        /// <param name="skip">The number of rows to skip.</param>
        /// <param name="take">Number of rows to take.</param>
        /// <param name="limitOptions">The limit options.</param>
        /// <param name="seed">The seed for repeatable reads. Only applies to random sampling</param>
        /// <returns>TableDbCommandBuilder&lt;MySqlCommand, MySqlParameter, MySqlLimitOption&gt;.</returns>
        protected override TableDbCommandBuilder<MySqlCommand, MySqlParameter, MySqlLimitOption> OnWithLimits(int? skip, int? take, MySqlLimitOption limitOptions, int? seed)
        {
            m_Seed = seed;
            m_Skip = skip;
            m_Take = take;
            m_LimitOptions = limitOptions;
            return this;
        }

        /// <summary>
        /// Adds limits to the command builder.
        /// </summary>
        /// <param name="skip">The number of rows to skip.</param>
        /// <param name="take">Number of rows to take.</param>
        /// <param name="limitOptions">The limit options.</param>
        /// <param name="seed">The seed for repeatable reads. Only applies to random sampling</param>
        /// <returns>TableDbCommandBuilder&lt;MySqlCommand, MySqlParameter, MySqlLimitOption&gt;.</returns>
        protected override TableDbCommandBuilder<MySqlCommand, MySqlParameter, MySqlLimitOption> OnWithLimits(int? skip, int? take, LimitOptions limitOptions, int? seed)
        {
            m_Seed = seed;
            m_Skip = skip;
            m_Take = take;
            m_LimitOptions = (MySqlLimitOption)limitOptions;
            return this;
        }
    }
}
