using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.SqlServer.Server;

namespace ContainsOptimization
{
    public class TvpBuilder
    {
        readonly string _typeName;
        readonly SqlMetaData[] _columns;
        readonly List<SqlDataRecord> _rows;

        public TvpBuilder(string typeName, params SqlMetaData[] columns)
        {
            _typeName = typeName;
            _columns = columns;
            _rows = new List<SqlDataRecord>();
        }

        public TvpBuilder AddRow(params object[] fieldValues)
        {
            var row = new SqlDataRecord(_columns);
            row.SetValues(fieldValues);
            _rows.Add(row);
            return this;
        }

        public SqlParameter CreateParameter(string name)
        {
            return new SqlParameter
            {
                ParameterName = name,
                Value = _rows,
                TypeName = _typeName,
                SqlDbType = SqlDbType.Structured
            };
        }
    }

    public static class TvpExtensions
    { 
        public static SqlParameter CreateTableValuedParameter<T>(this DbContext context, IEnumerable<T> source, string name)
        {
            var typeMapping = context.GetService<IRelationalTypeMappingSource>().GetMapping(typeof(T));
            var sqlDbType = (new SqlParameter() { DbType = typeMapping.DbType.Value }).SqlDbType;
            var storeType = typeMapping.StoreType;
            var tableType = $"dbo.{storeType}_values_table_type";
            var columnName = "value";
            var createStatement = $"if type_id('{tableType}') is null create type {tableType} as table ({columnName} {storeType});";
            
#pragma warning disable EF1000 // Possible SQL injection vulnerability.
            context.Database.ExecuteSqlCommand(createStatement);
#pragma warning restore EF1000 // Possible SQL injection vulnerability.

            var tvp = new TvpBuilder(tableType, new SqlMetaData(columnName, sqlDbType));
            foreach(var value in source)
            {
                tvp.AddRow(value);
            }
            return tvp.CreateParameter(name);
        }
    }
}