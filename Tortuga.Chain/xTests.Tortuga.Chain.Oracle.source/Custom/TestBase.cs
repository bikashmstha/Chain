using System;
using System.Collections.Generic;
using System.Configuration;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Tortuga.Chain;
using Tortuga.Chain.AuditRules;
using Tortuga.Chain.DataSources;
using Tortuga.Chain.Oracle;

namespace Tests
{
    public abstract partial class TestBase
    {

        static public readonly string AssemblyName = "Oracle";
        static protected readonly Dictionary<string, OracleDataSource> s_DataSources = new Dictionary<string, OracleDataSource>();
        protected static readonly OracleDataSource s_PrimaryDataSource;

        static TestBase()
        {
            Setup.AssemblyInit();
            foreach (ConnectionStringSettings con in ConfigurationManager.ConnectionStrings)
            {
                var ds = new OracleDataSource(con.Name, con.ConnectionString);
                if (s_PrimaryDataSource == null) s_PrimaryDataSource = ds;
                s_DataSources.Add(con.Name, ds);
            }
        }

        public static string CustomerTableName { get { return "Sales.Customer"; } }

        public static string EmployeeTableName { get { return "HR.Employee"; } }

        public string MultiResultSetProc1Name { get { return "Sales.CustomerWithOrdersByState"; } }

        public string TableFunction1Name { get { return "Sales.CustomersByState"; } }

        //public string TableFunction2Name { get { return "Sales.CustomersByStateInline"; } }

        public OracleDataSource AttachRules(OracleDataSource source)
        {
            return source.WithRules(
                new DateTimeRule("CreatedDate", DateTimeKind.Local, OperationTypes.Insert),
                new DateTimeRule("UpdatedDate", DateTimeKind.Local, OperationTypes.InsertOrUpdate),
                new UserDataRule("CreatedByKey", "EmployeeKey", OperationTypes.Insert),
                new UserDataRule("UpdatedByKey", "EmployeeKey", OperationTypes.InsertOrUpdate),
                new ValidateWithValidatable(OperationTypes.InsertOrUpdate)
                );
        }

        public OracleDataSource DataSource(string name, [CallerMemberName] string caller = null)
        {
            WriteLine($"{caller} requested Data Source {name}");

            return AttachTracers(s_DataSources[name]);
        }

        public OracleDataSourceBase DataSource(string name, DataSourceType mode, [CallerMemberName] string caller = null)
        {
            WriteLine($"{caller} requested Data Source {name} with mode {mode}");

            var ds = s_DataSources[name];
            switch (mode)
            {
                case DataSourceType.Normal: return AttachTracers(ds);
                case DataSourceType.Transactional: return AttachTracers(ds.BeginTransaction());
                case DataSourceType.Open:
                    var root = (IRootDataSource)ds;
                    return AttachTracers((OracleDataSourceBase)root.CreateOpenDataSource(root.CreateConnection(), null));
            }
            throw new ArgumentException($"Unkown mode {mode}");
        }

        public async Task<OracleDataSourceBase> DataSourceAsync(string name, DataSourceType mode, [CallerMemberName] string caller = null)
        {
            WriteLine($"{caller} requested Data Source {name} with mode {mode}");

            var ds = s_DataSources[name];
            switch (mode)
            {
                case DataSourceType.Normal: return AttachTracers(ds);
                case DataSourceType.Transactional: return AttachTracers(await ds.BeginTransactionAsync());
                case DataSourceType.Open:
                    var root = (IRootDataSource)ds;
                    return AttachTracers((OracleDataSourceBase)root.CreateOpenDataSource(await root.CreateConnectionAsync(), null));
            }
            throw new ArgumentException($"Unkown mode {mode}");
        }

        void WriteDetails(ExecutionEventArgs e)
        {
            if (e.ExecutionDetails is OracleCommandExecutionToken)
            {
                WriteLine("");
                WriteLine("Command text: ");
                WriteLine(e.ExecutionDetails.CommandText);
                WriteLine("CommandType: " + e.ExecutionDetails.CommandType);
                //Indent();
                foreach (var item in ((OracleCommandExecutionToken)e.ExecutionDetails).Parameters)
                    WriteLine(item.ParameterName + ": " + (item.Value == null || item.Value == DBNull.Value ? "<NULL>" : item.Value));
                //Unindent();
                WriteLine("******");
                WriteLine("");
            }
        }
    }
}
