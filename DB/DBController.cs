using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Reflection;
using System.Security.Cryptography;

namespace DB {
	/// <summary>
	/// Class <c>DBController</c> gestiona la interacción con una base de datos.
	/// </summary>
	public class DBController : IDisposable {

		// Funciona como override de System.Data.IsolationLevel,
		// para evitar que el usuario tenga que usar este enum en DBController,
		// y para evitar que se usen ciertos isolation levels poco estrictos
		public enum IsolationLevel {
			ReadUncomitted = 256,
			ReadCommitted = 4096,
			RepeatableRead = 65536,
			Serializable = 1048576,
			Snapshot = 16777216
		}

		private readonly string connectionString;
		private SqlConnection connection;
		private SqlTransaction transaction;

		/// <summary>
		/// Crea un <c>DBController</c> para una base de datos.
		/// </summary>
		/// <param name="connectionString">El connection string de la base de datos a utilizar.</param>
		public DBController(string connectionString) {
			this.connectionString = connectionString;
		}

		public void Connect() {
			connection = new SqlConnection(connectionString);
			connection.Open();
		}

		public void BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted) {
			int id = (int)isolationLevel;
			transaction = connection.BeginTransaction((System.Data.IsolationLevel)id);
		}

		public void CommitTransaction() => transaction.Commit();
		public void RollbackTransaction() => transaction.Rollback();
		public void CloseConnection() => connection.Close();

		public void Dispose() {
			connection?.Close();
			connection?.Dispose();
			transaction?.Dispose();
		}

		/// <summary>
		/// Method <c>Select</c> ejecuta un select en la base de datos.
		/// <example>
		/// <code>MyClass result = Select&lt;MyClass&gt;(query, parameters);
		/// </code>
		/// </example>
		/// </summary>
		/// <typeparam name="T">
		/// El tipo de dato en el que se serializa la respuesta. Debe tener un constructor
		/// sin parámetros.
		/// </typeparam>
		/// <param name="query">
		/// La query a ejecutar. Esta query puede tener parámetros. Se deben definir con un @.
		/// <example>
		/// <code>
		/// string query = "SELECT * FROM Tabla WHERE ID = @ID";
		/// </code>
		/// </example>
		/// </param>
		/// <param name="parameters">
		/// Los parámetros de la query. La key debe ser el nombre del parámetro, incluyendo el @.
		/// El value debe ser el objeto a utilizar, el tipo de dato debe coincidir con el de 
		/// la columna.
		/// <example>
		/// <code>
		/// Dictionary&lt;string, object&gt; parameters = new() {
		///		["@ID"] = 1
		///	};
		/// </code>
		/// </example>
		/// </param>
		/// <returns>Una lista de objetos, un objeto por fila.</returns>
		public List<T> Select<T>(string query, Dictionary<string, object> parameters) where T : new() {
			List<T> result = new List<T>();
			ExecuteCommand(query, parameters, command => {
				using (SqlDataReader reader = command.ExecuteReader()) {
					while (reader.Read()) {
						T item = new T();
						MapDataReaderToModel(reader, item);
						result.Add(item);
					}
				}
			});
			return result;
		}

		/// <summary>
		/// Method <c>Select</c> ejecuta un select en la base de datos.
		/// <example>
		/// <code>DataTable data = Select(query, parameters);
		/// </code>
		/// </example>
		/// </summary>
		/// <param name="query">
		/// La query a ejecutar. Esta query puede tener parámetros. Se deben definir con un @.
		/// <example>
		/// <code>
		/// string query = "SELECT * FROM Tabla WHERE ID = @ID";
		/// </code>
		/// </example>
		/// </param>
		/// <param name="parameters">
		/// Los parámetros de la query. La key debe ser el nombre del parámetro, incluyendo el @.
		/// El value debe ser el objeto a utilizar, el tipo de dato debe coincidir con el de 
		/// la columna.
		/// <example>
		/// <code>
		/// Dictionary&lt;string, object&gt; parameters = new() {
		///		["@ID"] = 1
		///	};
		/// </code>
		/// </example>
		/// </param>
		/// <returns>Un DataTable con la información requerida.</returns>
		public DataTable Select(string query, Dictionary<string, object> parameters) {
			DataTable dataTable = new DataTable();
			ExecuteCommand(query, parameters, command => {
				using (SqlDataAdapter dataAdapter = new SqlDataAdapter(command)) {
					dataAdapter.Fill(dataTable);
				}
			});
			return dataTable;
		}

		/// <summary>
		/// Method <c>NonQuery</c> ejecuta una query que puede ser de tipo Update, Delete o Insert.
		/// <example>
		/// <code> int affectedRows = NonQuery(query, parameters);
		/// </code>
		/// </example>
		/// </summary>
		/// <param name="database">La base de datos donde se ejecuta.</param>
		/// <param name="query">
		/// La query a ejecutar. Esta query puede tener parámetros. Se deben definir con un @.
		/// <example>
		/// <code>
		/// string query = "DELETE FROM Tabla WHERE ID = @ID";
		/// </code>
		/// </example>
		/// </param>
		/// <param name="parameters">
		/// Los parámetros de la query. La key debe ser el nombre del parámetro, incluyendo el @.
		/// El value debe ser el objeto a utilizar, el tipo de dato debe coincidir con el de 
		/// la columna.
		/// <example>
		/// <code>
		/// Dictionary&lt;string, object&gt; parameters = new() {
		///		["@ID"] = 1
		///	};
		/// </code>
		/// </example>
		/// </param>
		/// <returns>La cantidad de filas afectadas.</returns>
		public int NonQuery(string query, Dictionary<string, object> parameters) {
			int value = -1;
			ExecuteCommand(query, parameters, command => {
				try {
					value = command.ExecuteNonQuery();
				} catch (Exception e) {
					System.Diagnostics.Debug.WriteLine(e.ToString());
					throw e;
				}
			});
			return value;
		}

		public void StoredProcedure(string storedProcedure, Dictionary<string, object> spParams) {
			SqlCommand command = new SqlCommand(storedProcedure, connection, transaction) {
				CommandType = CommandType.StoredProcedure
			};

			LoadParamsInCommand(command, spParams);

			try {
				command.ExecuteNonQuery();
			} catch (Exception e) {
				System.Diagnostics.Debug.WriteLine(e.ToString());
				throw e;
			}
		}

		/// <summary>
		/// Ejectuta un Store Procedure con valor de retorno, que recibe parámetros.
		/// <example>
		/// <code> int statusCode = StoreProcedureWithCodeRet(storedProcedure, parameters);
		/// </code>
		/// </example>
		/// </summary>
		/// <param name="storedProcedure">
		/// El nombre del Store Procedure.
		/// <example>
		/// <code>
		/// string storedProcedure = "GenerateReport";
		/// </code>
		/// </example>
		/// </param>
		/// <param name="spParams">
		/// Los parámetros que recibe el SP. La key debe ser el nombre del parámetro
		/// en el SP, incluyendo el @. El value debe ser el objeto a utilizar, con el tipo
		/// de dato igual que en el SP.
		/// <example>
		/// <code>
		/// Dictionary&lt;string, object&gt; spParams = new() {
		///		["@Month"] = "January",
		///		["@Year"] = 2024
		///	};
		/// </code>
		/// </example>
		/// </param>
		/// <returns>El status code output del store procedure.</returns>
		public int StoredProcedureWithCodeRet(string storedProcedure, Dictionary<string, object> spParams) {
			SqlCommand command = new SqlCommand(storedProcedure, connection, transaction) {
				CommandType = CommandType.StoredProcedure
			};

			LoadParamsInCommand(command, spParams);

			SqlParameter output = new SqlParameter {
				Direction = ParameterDirection.ReturnValue
			};
			command.Parameters.Add(output);

			try {
				command.ExecuteNonQuery();
				return (int)output.Value;
			} catch (Exception e) {
				System.Diagnostics.Debug.WriteLine(e.ToString());
				throw e;
				return -1;
			}
		}

		public int StoredProcedureWithOutput(string storedProcedure, Dictionary<string, object> spParams, string outputParameter) {
			SqlCommand command = new SqlCommand(storedProcedure, connection, transaction) {
				CommandType = CommandType.StoredProcedure
			};

			LoadParamsInCommand(command, spParams);

			string output = outputParameter.StartsWith("@") ? outputParameter : "@" + outputParameter;
			command.Parameters[output].Direction = ParameterDirection.Output;

			try {
				command.ExecuteNonQuery();
				return (int)command.Parameters[output].Value;
			} catch (Exception e) {
				System.Diagnostics.Debug.WriteLine(e.ToString());
				throw e;
				return -1;
			}
		}

		/// <summary>
		/// Ejecuta un comando con parámetros.
		/// </summary>
		/// <param name="query">La query a ejecutar.</param>
		/// <param name="parameters">Los parámetros de la query.</param>
		/// <param name="action">La acción a ejecutar para el comando específico.</param>
		private void ExecuteCommand(string query, Dictionary<string, object> parameters, Action<SqlCommand> action) {
			try {
				using (SqlCommand command = new SqlCommand(query, connection, transaction)) {
					LoadParamsInCommand(command, parameters);
					action(command);
				}
			} catch (Exception e) {
				System.Diagnostics.Debug.WriteLine(e.Data);
				throw e;
			}
		}

		/// <summary>
		/// Carga los parámetros a un comando.
		/// </summary>
		/// <param name="command">El comando a utilizar.</param>
		/// <param name="parameters">Los parámetros a cargar.</param>
		private void LoadParamsInCommand(SqlCommand command, Dictionary<string, object> parameters) {
			if (parameters != null) {
				foreach (var parameter in parameters) {
					string paramName = parameter.Key.StartsWith("@") ? parameter.Key : "@" + parameter.Key;
					command.Parameters.AddWithValue(paramName, parameter.Value ?? DBNull.Value);
				}
			}
		}

		/// <summary>
		/// Method <c>MapDataReaderToModel</c> mapea la lectura de la fila con un objeto.
		/// </summary>
		/// <typeparam name="T">El tipo de objeto en el que se mapea.</typeparam>
		/// <param name="reader">El lector.</param>
		/// <param name="item">La fila.</param>
		private void MapDataReaderToModel<T>(SqlDataReader reader, T item) where T : new() {
			var properties = item.GetType().GetRuntimeProperties();

			foreach (var property in properties) {
				var columnNameAttribute = property.GetCustomAttribute<ColumnNameAttribute>();

				if (columnNameAttribute != null && reader[columnNameAttribute.ColumnName] != DBNull.Value) {
					object value = reader[columnNameAttribute.ColumnName];
					try {
						if (value.GetType() == property.PropertyType) {
							property.SetValue(item, value);
						} else {
							System.Diagnostics.Debug.WriteLine($"Tipo de dato incompatible para la propiedad {property.Name}");
						}
					} catch (Exception ex) {
						System.Diagnostics.Debug.WriteLine($"Error al asignar valor a la propiedad {property.Name}: {ex.Message}");
						throw ex;
					}
				}
			}
		}
	}
}

