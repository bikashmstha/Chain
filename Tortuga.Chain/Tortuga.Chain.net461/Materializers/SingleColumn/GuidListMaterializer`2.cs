﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Tortuga.Chain.CommandBuilders;

namespace Tortuga.Chain.Materializers
{
    /// <summary>
    /// Materializes the result set as a list of Guids.
    /// </summary>
    /// <typeparam name="TCommandType">The type of the t command type.</typeparam>
    /// <typeparam name="TParameterType">The type of the t parameter type.</typeparam>
    public class GuidListMaterializer<TCommandType, TParameterType> : SingleColumnMaterializer<TCommandType, TParameterType, List<Guid>> where TCommandType : DbCommand
        where TParameterType : DbParameter
    {

        readonly ListOptions m_ListOptions;

        /// <summary>
        /// </summary>
        /// <param name="commandBuilder">The command builder.</param>
        /// <param name="listOptions">The list options.</param>
        /// <param name="columnName">Name of the desired column.</param>
        public GuidListMaterializer(DbCommandBuilder<TCommandType, TParameterType> commandBuilder, ListOptions listOptions, string columnName = null)
            : base(commandBuilder, columnName)
        {
            m_ListOptions = listOptions;

        }

        /// <summary>
        /// Execute the operation synchronously.
        /// </summary>
        /// <returns></returns>
        public override List<Guid> Execute(object state = null)
        {
            var result = new List<Guid>();

            ExecuteCore(cmd =>
            {

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.FieldCount > 1 && !m_ListOptions.HasFlag(ListOptions.IgnoreExtraColumns))
                        throw new UnexpectedDataException($"Expected one column but found {reader.FieldCount} columns");

                    var columnCount = m_ListOptions.HasFlag(ListOptions.FlattenExtraColumns) ? reader.FieldCount : 1;
                    var discardNulls = m_ListOptions.HasFlag(ListOptions.DiscardNulls);
                    while (reader.Read())
                    {
                        for (var i = 0; i < columnCount; i++)
                        {
                            if (reader.IsDBNull(i) && !discardNulls)
                                throw new DataException("Unexpected null value");

                            result.Add(reader.GetGuid(i));
                        }
                    }
                }
            }, state);


            return result;
        }


        /// <summary>
        /// Execute the operation asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="state">User defined state, usually used for logging.</param>
        /// <returns></returns>
        public override async Task<List<Guid>> ExecuteAsync(CancellationToken cancellationToken, object state = null)
        {
            var result = new List<Guid>();

            await ExecuteCoreAsync(async cmd =>
            {

                using (var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false))
                {
                    if (reader.FieldCount > 1 && !m_ListOptions.HasFlag(ListOptions.IgnoreExtraColumns))
                        throw new UnexpectedDataException($"Expected one column but found {reader.FieldCount} columns");

                    var columnCount = m_ListOptions.HasFlag(ListOptions.FlattenExtraColumns) ? reader.FieldCount : 1;
                    var discardNulls = m_ListOptions.HasFlag(ListOptions.DiscardNulls);

                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        for (var i = 0; i < columnCount; i++)
                        {
                            if (reader.IsDBNull(i) && !discardNulls)
                                throw new DataException("Unexpected null value");

                            result.Add(reader.GetGuid(i));
                        }
                    }
                }
            }, cancellationToken, state).ConfigureAwait(false);


            return result;
        }

    }
}