using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using RedStapler.StandardLibrary.DataAccess;
using RedStapler.StandardLibrary.DataAccess.CommandWriting;
using RedStapler.StandardLibrary.DataAccess.CommandWriting.Commands;
using RedStapler.StandardLibrary.DataAccess.CommandWriting.InlineConditionAbstraction.Conditions;
using RedStapler.StandardLibrary.DatabaseSpecification;
using RedStapler.StandardLibrary.DatabaseSpecification.Databases;
using RedStapler.StandardLibrary.IO;

namespace RedStapler.StandardLibrary.InstallationSupportUtility.DatabaseAbstraction.Databases {
	public class Oracle: Database {
		private const string dataPumpOracleDirectoryName = "red_stapler_data_pump_dir";
		private const string databaseFileDumpFileName = "Dump File.dmp";
		private const string databaseFileSchemaNameFileName = "Schema.txt";
		private static readonly string dataPumpFolderPath = StandardLibraryMethods.CombinePaths( AppTools.RedStaplerFolderPath, "Oracle Data Pump" );

		private readonly OracleInfo info;
		private readonly List<string> latestTableSpaces;

		internal Oracle( OracleInfo info, List<string> latestTableSpaces ) {
			this.info = info;
			this.latestTableSpaces = latestTableSpaces;
		}

		string Database.SecondaryDatabaseName { get { return ( info as DatabaseInfo ).SecondaryDatabaseName; } }

		void Database.ExecuteSqlScriptInTransaction( string script ) {
			using( var sw = new StringWriter() ) {
				// Carriage returns seem to be significant here.
				sw.WriteLine( "WHENEVER SQLERROR EXIT SQL.SQLCODE;" );
				sw.Write( script );
				sw.WriteLine( "EXIT SUCCESS COMMIT;" );

				executeMethodWithDbExceptionHandling( delegate {
					try {
						// -L option stops it from prompting on failed logon.
						StandardLibraryMethods.RunProgram( "sqlplus", "-L " + getLogonString(), sw.ToString(), true );
					}
					catch( Exception e ) {
						throw DataAccessMethods.CreateDbConnectionException( info, "updating logic in", e );
					}
				} );
			}
		}

		int Database.GetLineMarker() {
			var value = 0;
			ExecuteDbMethod( delegate( DBConnection cn ) {
				var cmd = cn.DatabaseInfo.CreateCommand();
				cmd.CommandText = "SELECT v FROM global_numbers WHERE k = 'LineMarker'";
				value = Convert.ToInt32( cn.ExecuteScalarCommand( cmd ) );
			} );
			return value;
		}

		void Database.UpdateLineMarker( int value ) {
			ExecuteDbMethod( delegate( DBConnection cn ) {
				var command = new InlineUpdate( "global_numbers" );
				command.AddColumnModification( new InlineDbCommandColumnValue( "v", new DbParameterValue( value ) ) );
				command.AddCondition( new EqualityCondition( new InlineDbCommandColumnValue( "k", new DbParameterValue( "LineMarker" ) ) ) );
				command.Execute( cn );
			} );
		}

		void Database.ExportToFile( string filePath ) {
			Directory.CreateDirectory( dataPumpFolderPath );
			try {
				executeMethodWithDbExceptionHandling( delegate {
					try {
						// We pass an enter keystroke as input in an attempt to kill the program if it gets stuck on a username prompt because of a bad logon string.
						StandardLibraryMethods.RunProgram( "expdp",
						                                   getLogonString() + " DIRECTORY=" + dataPumpOracleDirectoryName + " DUMPFILE=\"\"\"" + getDumpFileName() +
						                                   "\"\"\" NOLOGFILE=y VERSION=10.2",
						                                   Environment.NewLine,
						                                   true );
					}
					catch( Exception e ) {
						throwUserCorrectableExceptionIfNecessary( e );
						throw DataAccessMethods.CreateDbConnectionException( info, "exporting (to file)", e );
					}
				} );

				IoMethods.ExecuteWithTempFolder( folderPath => {
					IoMethods.CopyFile( getDumpFilePath(), StandardLibraryMethods.CombinePaths( folderPath, databaseFileDumpFileName ) );
					File.WriteAllText( StandardLibraryMethods.CombinePaths( folderPath, databaseFileSchemaNameFileName ), info.UserAndSchema );
					ZipOps.ZipFolderAsFile( folderPath, filePath );
				} );
			}
			finally {
				IoMethods.DeleteFile( getDumpFilePath() );
			}
		}

		void Database.DeleteAndReCreateFromFile( string filePath, bool keepDbInStandbyMode ) {
			executeDbMethodWithSpecifiedDatabaseInfo(
				new OracleInfo( ( info as DatabaseInfo ).SecondaryDatabaseName,
				                info.DataSource,
				                "sys",
				                ConfigurationLogic.OracleSysPassword,
				                info.SupportsConnectionPooling,
				                info.SupportsLinguisticIndexes ),
				cn => {
					executeUserReCreationCommand( cn, "CREATE OR REPLACE DIRECTORY " + dataPumpOracleDirectoryName + " AS '" + dataPumpFolderPath + "'" );
					deleteAndReCreateUser( cn );
				} );

			try {
				IoMethods.ExecuteWithTempFolder( tempFolderPath => {
					var folderPath = StandardLibraryMethods.CombinePaths( tempFolderPath, "Database File" );
					ZipOps.UnZipFileAsFolder( filePath, folderPath );
					try {
						IoMethods.CopyFile( StandardLibraryMethods.CombinePaths( folderPath, databaseFileDumpFileName ), getDumpFilePath() );

						executeMethodWithDbExceptionHandling( delegate {
							try {
								StandardLibraryMethods.RunProgram( "impdp",
								                                   getLogonString() + " DIRECTORY=" + dataPumpOracleDirectoryName + " DUMPFILE=\"\"\"" + getDumpFileName() +
								                                   "\"\"\" NOLOGFILE=y REMAP_SCHEMA=" +
								                                   File.ReadAllText( StandardLibraryMethods.CombinePaths( folderPath, databaseFileSchemaNameFileName ) ) + ":" +
								                                   info.UserAndSchema,
								                                   "",
								                                   true );
							}
							catch( Exception e ) {
								throwUserCorrectableExceptionIfNecessary( e );
								if( e is FileNotFoundException )
									throw new UserCorrectableException( "The schema name file was not found, probably because of a corrupt database file in the data package.", e );

								// Secondary databases such as RLE cause procedure compilation errors when imported, and since we have no way of
								// distinguishing these from legitimate import problems, we have no choice but to ignore all exceptions.
								if( ( info as DatabaseInfo ).SecondaryDatabaseName.Length == 0 )
									throw DataAccessMethods.CreateDbConnectionException( info, "re-creating (from file)", e );
							}
						} );
					}
					finally {
						IoMethods.DeleteFile( getDumpFilePath() );
					}
				} );
			}
			catch {
				// We don't want to leave a partial user/schema on the machine since it may confuse future ISU operations.
				executeDbMethodWithSpecifiedDatabaseInfo(
					new OracleInfo( ( info as DatabaseInfo ).SecondaryDatabaseName,
					                info.DataSource,
					                "sys",
					                ConfigurationLogic.OracleSysPassword,
					                info.SupportsConnectionPooling,
					                info.SupportsLinguisticIndexes ),
					deleteUser );

				throw;
			}
		}

		private void deleteAndReCreateUser( DBConnection cn ) {
			// Delete the existing user and schema.
			deleteUser( cn );

			// Re-create the user with the minimally required privileges.
			executeUserReCreationCommand( cn, "CREATE USER " + info.UserAndSchema + " IDENTIFIED BY " + info.Password + " ACCOUNT UNLOCK" );

			// This allows the user to connect to the database.
			executeUserReCreationCommand( cn, "GRANT CREATE SESSION TO " + info.UserAndSchema );

			// This overrides all tablespace quotas for this user, which default to 0 and therefore prevent the user from creating any tables or other objects.
			executeUserReCreationCommand( cn, "GRANT UNLIMITED TABLESPACE TO " + info.UserAndSchema );

			executeUserReCreationCommand( cn, "GRANT CREATE PROCEDURE TO " + info.UserAndSchema ); // Necessary for RLE Personnel secondary databases.
			executeUserReCreationCommand( cn, "GRANT CREATE SEQUENCE TO " + info.UserAndSchema );
			executeUserReCreationCommand( cn, "GRANT CREATE TABLE TO " + info.UserAndSchema );
			executeUserReCreationCommand( cn, "GRANT CREATE TRIGGER TO " + info.UserAndSchema ); // Necessary for RLE Personnel secondary databases.
			executeUserReCreationCommand( cn, "GRANT READ, WRITE ON DIRECTORY " + dataPumpOracleDirectoryName + " TO " + info.UserAndSchema );

			// Get all tablespaces currently in the database.
			var command = cn.DatabaseInfo.CreateCommand();
			command.CommandText = "SELECT tablespace_name FROM dba_tablespaces";
			var currentTableSpaces = new List<string>();
			cn.ExecuteReaderCommand( command,
			                         reader => {
				                         while( reader.Read() )
					                         currentTableSpaces.Add( reader.GetString( 0 ).ToLower() );
			                         } );

			// Create necessary tablespaces that don't already exist.
			foreach( var nonExistentTs in latestTableSpaces.Select( s => s.ToLower() ).Except( currentTableSpaces ) ) {
				var tableSpaceFolderPath = StandardLibraryMethods.CombinePaths( AppTools.RedStaplerFolderPath, "Oracle Tablespaces" );
				Directory.CreateDirectory( tableSpaceFolderPath );
				executeUserReCreationCommand( cn,
				                              "CREATE TABLESPACE " + nonExistentTs + " DATAFILE '" +
				                              StandardLibraryMethods.CombinePaths( tableSpaceFolderPath, nonExistentTs + ".dbf" ) + "' SIZE 100M" );
			}
		}

		private string getLogonString() {
			return info.UserAndSchema + "/" + info.Password + "@" + info.DataSource;
		}

		private void throwUserCorrectableExceptionIfNecessary( Exception e ) {
			if( e.Message.Contains( "ORA-04031" ) )
				throw new UserCorrectableException( "Oracle has insufficient memory. You may need to follow the Oracle process to increase the memory_target parameters.", e );
		}

		private string getDumpFilePath() {
			return StandardLibraryMethods.CombinePaths( dataPumpFolderPath, getDumpFileName() );
		}

		private string getDumpFileName() {
			return info.UserAndSchema + ".dmp";
		}

		private void deleteUser( DBConnection cn ) {
			try {
				executeUserReCreationCommand( cn, "DROP USER " + info.UserAndSchema + " CASCADE" );
			}
			catch( Exception e ) {
				if( e.GetBaseException().Message.Contains( "ORA-01940" ) ) {
					throw new UserCorrectableException(
						"Failed to delete one of the Oracle user accounts for this installation. Please stop all web sites for this installation, close all Visual Studio Data Connections for this installation, and try the operation again.",
						e );
				}

				// ORA-01918 means the user and schema did not exist. That's fine.
				if( !e.GetBaseException().Message.Contains( "ORA-01918" ) )
					throw;
			}
		}

		private void executeUserReCreationCommand( DBConnection cn, string commandText ) {
			var command = cn.DatabaseInfo.CreateCommand();
			command.CommandTimeout = 0; // This means the command can take as much time as it needs.
			command.CommandText = commandText;

			// NOTE: Not sure if this is the right execute method to use.
			cn.ExecuteNonQueryCommand( command );
		}

		void Database.BackupTransactionLog( string folderPath ) {
			throw new NotSupportedException();
		}

		void Database.RestoreNewTransactionLogs( string folderPath ) {
			throw new NotSupportedException();
		}

		public string GetLogSummary( string folderPath ) {
			throw new NotSupportedException();
		}

		List<string> Database.GetTables() {
			var tables = new List<string>();
			ExecuteDbMethod( delegate( DBConnection cn ) {
				var command = cn.DatabaseInfo.CreateCommand();
				command.CommandText = "SELECT table_name FROM user_tables";
				cn.ExecuteReaderCommand( command,
				                         reader => {
					                         while( reader.Read() )
						                         tables.Add( reader.GetString( 0 ) );
				                         } );
			} );
			return tables;
		}

		List<string> Database.GetProcedures() {
			var procedures = new List<string>();
			ExecuteDbMethod( delegate( DBConnection cn ) {
				foreach( DataRow row in cn.GetSchema( "Procedures", info.UserAndSchema ).Rows )
					procedures.Add( (string)row[ "OBJECT_NAME" ] );
			} );
			return procedures;
		}

		List<ProcedureParameter> Database.GetProcedureParameters( string procedure ) {
			var parameters = new List<ProcedureParameter>();
			ExecuteDbMethod( delegate( DBConnection cn ) {
				var rows = new List<DataRow>();
				foreach( DataRow row in cn.GetSchema( "ProcedureParameters", null, procedure ).Rows )
					rows.Add( row );
				rows.Sort( ( x, y ) => (int)( (decimal)x[ "POSITION" ] - (decimal)y[ "POSITION" ] ) );
				foreach( var row in rows ) {
					var dataType = (string)row[ "DATA_TYPE" ];
					var parameterDirection = getParameterDirection( (string)row[ "IN_OUT" ] );

					// The parameters returned by this method are used with an OracleCommand object that will be executed using
					// ExecuteReader. Per the Oracle Data Provider for .NET documentation for OracleCommand.ExecuteReader, output
					// REF CURSOR parameters in a procedure can be accessed through the returned data reader and don't need to be
					// treated as ordinary command parameters. That's why we don't include them here.
					if( dataType != "REF CURSOR" || parameterDirection != ParameterDirection.Output )
						parameters.Add( new ProcedureParameter( cn, (string)row[ "ARGUMENT_NAME" ], dataType, parameterDirection ) );
				}
			} );
			return parameters;
		}

		private ParameterDirection getParameterDirection( string direction ) {
			if( direction == "IN" )
				return ParameterDirection.Input;
			if( direction == "OUT" )
				return ParameterDirection.Output;
			if( direction == "IN/OUT" )
				return ParameterDirection.InputOutput;
			throw new ApplicationException( "Unknown parameter direction string." );
		}

		void Database.PerformMaintenance() {}

		void Database.ShrinkAfterPostUpdateDataCommands() {}

		/*
	private static void setupNewDatabase() {
		string script = @"

CREATE TABLE global_numbers (
	k VARCHAR2( 100 )
		CONSTRAINT global_numbers_pk PRIMARY KEY,
	v NUMBER
);

INSERT INTO global_numbers VALUES( 'LineMarker', 0 );

CREATE SEQUENCE main_sequence;
";
	}
*/

		public void ExecuteDbMethod( Action<DBConnection> method ) {
			executeDbMethodWithSpecifiedDatabaseInfo( info, method );
		}

		private void executeDbMethodWithSpecifiedDatabaseInfo( OracleInfo info, Action<DBConnection> method ) {
			executeMethodWithDbExceptionHandling( delegate {
				// Before we disabled pooling, we couldn't repeatedly perform Update Data operations since users with open connections can't be dropped.
				var connection =
					new DBConnection( new OracleInfo( ( info as DatabaseInfo ).SecondaryDatabaseName,
					                                  info.DataSource,
					                                  info.UserAndSchema,
					                                  info.Password,
					                                  false,
					                                  info.SupportsLinguisticIndexes ) );

				connection.ExecuteWithConnectionOpen( () => method( connection ) );
			} );
		}

		private void executeMethodWithDbExceptionHandling( Action method ) {
			try {
				method();
			}
			catch( DbConnectionFailureException e ) {
				throw new UserCorrectableException( "Failed to connect to Oracle.", e );
			}
		}
	}
}