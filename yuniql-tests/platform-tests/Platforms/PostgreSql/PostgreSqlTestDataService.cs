using Yuniql.Extensibility;
using System.IO;
using Npgsql;
using System;
using Yuniql.Core;
using Yuniql.PostgreSql;
using Yuniql.PlatformTests.Setup;

namespace Yuniql.PlatformTests.Platforms.PostgreSql
{
    public class PostgreSqlTestDataService : TestDataServiceBase
    {
        public PostgreSqlTestDataService(IDataService dataService, ITokenReplacementService tokenReplacementService) : base(dataService, tokenReplacementService)
        {
        }

        public override string GetConnectionString(string databaseName)
        {
            var connectionString = EnvironmentHelper.GetEnvironmentVariable("YUNIQL_TEST_CONNECTION_STRING");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ApplicationException("Missing environment variable YUNIQL_TEST_CONNECTION_STRING. See WIKI for developer guides.");
            }

            var result = new NpgsqlConnectionStringBuilder(connectionString);
            result.Database = databaseName;

            return result.ConnectionString;
        }

        public override bool CheckIfDbExist(string connectionString)
        {
            //use the target user database to migrate, this is part of orig connection string
            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            var sqlStatement = $"SELECT 1 from pg_database WHERE datname ='{connectionStringBuilder.Database}';";

            //switch database into master/system database where db catalogs are maintained
            connectionStringBuilder.Database = "postgres";
            return QuerySingleBool(connectionStringBuilder.ConnectionString, sqlStatement);
        }

        public override bool CheckIfDbObjectExist(string connectionString, string objectName)
        {
            var dbObject = GetObjectNameWithSchema(objectName);

            //check from procedures, im just lazy to figure out join in pgsql :)
            var sqlStatement = $"SELECT 1 FROM pg_proc WHERE  proname = '{objectName.ToLower()}'";
            bool result = QuerySingleBool(connectionString, sqlStatement);

            //check from tables, im just lazy to figure out join in pgsql :)
            if (!result)
            {
                sqlStatement = $"SELECT 1 FROM pg_class WHERE  relname = '{objectName.ToLower()}'";
                result = QuerySingleBool(connectionString, sqlStatement);
            }

            if (!result)
            {
                sqlStatement = $"SELECT 1 FROM information_schema.tables WHERE TABLE_SCHEMA = '{dbObject.Item1}'  AND TABLE_NAME = '{dbObject.Item2}'";
                result = QuerySingleBool(connectionString, sqlStatement);
            }

            return result;
        }

        public override string GetSqlForCreateDbSchema(string schemaName)
        {
            return $@"
CREATE SCHEMA {schemaName};
";
        }

        public override string GetSqlForCreateDbObject(string objectName)
        {
            return $@"
CREATE TABLE public.{objectName} (
	VisitorID SERIAL NOT NULL,
	FirstName VARCHAR(255) NULL,
	LastName VARCHAR(255) NULL,
	Address VARCHAR(255) NULL,
	Email VARCHAR(255) NULL
);
";
        }

        public override string GetSqlForCreateDbObjectWithError(string objectName)
        {
            return $@"
CREATE TABLE public.{objectName} (
	VisitorID SERIAL NOT NULL,
	FirstName VARCHAR(255) NULL,
	LastName VARCHAR(255) NULL,
	Address VARCHAR(255) NULL,
	Email [VARCHAR](255) NULL
);
";
        }

        public override string GetSqlForCreateDbObjectWithTokens(string objectName)
        {
            return $@"
CREATE TABLE public.{objectName}_${{Token1}}_${{Token2}}_${{Token3}} (
	VisitorID SERIAL NOT NULL,
	FirstName VARCHAR(255) NULL,
	LastName VARCHAR(255) NULL,
	Address VARCHAR(255) NULL,
	Email VARCHAR(255) NULL
);
";
        }

        public override string GetSqlForCreateBulkTable(string tableName)
        {
            return $@"
CREATE TABLE {tableName}(
	FirstName VARCHAR(50) NOT NULL,
	LastName VARCHAR(50) NOT NULL,
	BirthDate TIMESTAMP NULL
);
";
        }

        public override string GetSqlForSingleLine(string objectName)
        {
            throw new NotSupportedException($"Batching statements is not supported in this platform. " +
                $"See {nameof(PostgreSqlDataService)}.{nameof(PostgreSqlDataService.IsBatchSqlSupported)}");
        }

        public override string GetSqlForSingleLineWithoutTerminator(string objectName)
        {
            throw new NotSupportedException($"Batching statements is not supported in this platform. " +
                $"See {nameof(PostgreSqlDataService)}.{nameof(PostgreSqlDataService.IsBatchSqlSupported)}");
        }

        public override string GetSqlForMultilineWithoutTerminatorInLastLine(string objectName1, string objectName2, string objectName3)
        {
            throw new NotSupportedException($"Batching statements is not supported in this platform. " +
                $"See {nameof(PostgreSqlDataService)}.{nameof(PostgreSqlDataService.IsBatchSqlSupported)}");
        }

        public override string GetSqlForMultilineWithTerminatorInCommentBlock(string objectName1, string objectName2, string objectName3)
        {
            throw new NotSupportedException($"Batching statements is not supported in this platform. " +
                $"See {nameof(PostgreSqlDataService)}.{nameof(PostgreSqlDataService.IsBatchSqlSupported)}");
        }

        public override string GetSqlForMultilineWithTerminatorInsideStatements(string objectName1, string objectName2, string objectName3)
        {
            throw new NotSupportedException($"Batching statements is not supported in this platform. " +
                $"See {nameof(PostgreSqlDataService)}.{nameof(PostgreSqlDataService.IsBatchSqlSupported)}");
        }

        public override string GetSqlForMultilineWithError(string objectName1, string objectName2)
        {
            throw new NotSupportedException($"Batching statements is not supported in this platform. " +
                $"See {nameof(PostgreSqlDataService)}.{nameof(PostgreSqlDataService.IsBatchSqlSupported)}");
        }

        public override void CreateScriptFile(string sqlFilePath, string sqlStatement)
        {
            using var sw = File.CreateText(sqlFilePath);
            sw.WriteLine(sqlStatement);
        }

        public override string GetSqlForCleanup()
        {
            return @"
DROP TABLE script1;
DROP TABLE script2;
DROP TABLE script3;
";
        }

        private Tuple<string, string> GetObjectNameWithSchema(string objectName)
        {
            //check if a non-default dbo schema is used
            var schemaName = "public";
            var newObjectName = objectName;

            if (objectName.IndexOf('.') > 0)
            {
                schemaName = objectName.Split('.')[0];
                newObjectName = objectName.Split('.')[1];
            }

            return new Tuple<string, string>(schemaName.ToLower(), newObjectName.ToLower());
        }

        //https://dba.stackexchange.com/questions/11893/force-drop-db-while-others-may-be-connected
        public override void DropDatabase(string connectionString)
        {
            //not needed need since test cases are executed against disposable database containers
            //we could simply docker rm the running test container after tests completed

            //use the target user database to migrate, this is part of orig connection string
            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            var databaseName = connectionStringBuilder.Database;

            var sqlStatement = $@"
--making sure the database exists
SELECT * from pg_database where datname = '{databaseName}';

--disallow new connections
UPDATE pg_database SET datallowconn = 'false' WHERE datname = '{databaseName}';
ALTER DATABASE {databaseName} CONNECTION LIMIT 1;

--terminate existing connections
SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{databaseName}';

--drop database
DROP DATABASE {databaseName};
";

            //switch database into master/system database where db catalogs are maintained
            connectionStringBuilder.Database = "postgres";
            ExecuteNonQuery(connectionStringBuilder.ConnectionString, sqlStatement);
        }
    }
}
