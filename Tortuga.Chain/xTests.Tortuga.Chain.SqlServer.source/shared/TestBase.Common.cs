﻿using System.Runtime.CompilerServices;
using System.Threading;
using Tortuga.Chain;
using Tortuga.Chain.DataSources;
using Xunit.Abstractions;

namespace Tests
{
    partial class TestBase
    {

        private readonly ITestOutputHelper m_Output;

        static int s_DataSourceCount;

        public TestBase(ITestOutputHelper output)
        {
            m_Output = output;
        }
        public T AttachTracers<T>(T dataSource) where T : DataSource
        {
            Interlocked.Increment(ref s_DataSourceCount);
            WriteLine("Data source count: " + s_DataSourceCount);

            dataSource.ExecutionCanceled += DefaultDispatcher_ExecutionCanceled;
            dataSource.ExecutionError += DefaultDispatcher_ExecutionError;
            dataSource.ExecutionFinished += DefaultDispatcher_ExecutionFinished;
            dataSource.ExecutionStarted += DefaultDispatcher_ExecutionStarted;
            CompiledMaterializers.MaterializerCompiled += CompiledMaterializers_MaterializerCompiled;
            CompiledMaterializers.MaterializerCompilerFailed += CompiledMaterializers_MaterializerCompiled;

            return dataSource;
        }

        public IClass2DataSource DataSource2(string name, DataSourceType mode, [CallerMemberName] string caller = null)
        {
            return (IClass2DataSource)DataSource(name, mode, caller);
        }

        public void Release(IDataSource dataSource)
        {
            WriteLine($"Releasing data source {dataSource.Name} ({dataSource.GetType().Name})");

            dataSource.ExecutionCanceled -= DefaultDispatcher_ExecutionCanceled;
            dataSource.ExecutionError -= DefaultDispatcher_ExecutionError;
            dataSource.ExecutionFinished -= DefaultDispatcher_ExecutionFinished;
            dataSource.ExecutionStarted -= DefaultDispatcher_ExecutionStarted;
            CompiledMaterializers.MaterializerCompiled -= CompiledMaterializers_MaterializerCompiled;
            CompiledMaterializers.MaterializerCompilerFailed -= CompiledMaterializers_MaterializerCompiled;


            var trans = dataSource as ITransactionalDataSource;
            trans?.Commit();

            var open = dataSource as IOpenDataSource;
            open?.TryCommit();
            open?.Close();

            Interlocked.Decrement(ref s_DataSourceCount);
            WriteLine("Data source count: " + s_DataSourceCount);
        }
        protected void WriteLine(string message)
        {
#if Debug
            try
            {
                m_Output.WriteLine(message);
            }
            catch
            {
                Debug.WriteLine("Error writing to xUnit log");
            }
            Debug.WriteLine(message);
#endif
        }

        void CompiledMaterializers_MaterializerCompiled(object sender, MaterializerCompilerEventArgs e)
        {
            WriteLine("******");
            WriteLine("Compiled Materializer");
            WriteLine("SQL");
            WriteLine(e.Sql);
            WriteLine("Code");
            WriteLine(e.Code);
        }

        void DefaultDispatcher_ExecutionCanceled(object sender, ExecutionEventArgs e)
        {
            WriteLine("******");
            WriteLine($"Execution canceled: {e.ExecutionDetails.OperationName}. Duration: {e.Duration.Value.TotalSeconds.ToString("N3")} sec.");
            //WriteDetails(e);
        }
        void DefaultDispatcher_ExecutionError(object sender, ExecutionEventArgs e)
        {
            WriteLine("******");
            WriteLine($"Execution error: {e.ExecutionDetails.OperationName}. Duration: {e.Duration.Value.TotalSeconds.ToString("N3")} sec.");
            //WriteDetails(e);
        }
        void DefaultDispatcher_ExecutionFinished(object sender, ExecutionEventArgs e)
        {
            WriteLine("******");
            WriteLine($"Execution finished: {e.ExecutionDetails.OperationName}. Duration: {e.Duration.Value.TotalSeconds.ToString("N3")} sec. Rows affected: {(e.RowsAffected != null ? e.RowsAffected.Value.ToString("N0") : "<NULL>")}.");
            //WriteDetails(e);
        }

        void DefaultDispatcher_ExecutionStarted(object sender, ExecutionEventArgs e)
        {
            WriteLine("******");
            WriteLine($"Execution started: {e.ExecutionDetails.OperationName}");
            WriteDetails(e);
        }


    }
}
