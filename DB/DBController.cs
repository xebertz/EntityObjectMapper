using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using System.Security.Cryptography;

namespace DB {
	/// <summary>
	/// Class <c>DBController</c> gestiona la interacción con una base de datos de SERVREF.
	/// </summary>
	public class DBController {
		private string connectionString;

		public DBController(string connectionString) {
			this.connectionString = connectionString;
		}

		/// <summary>
		/// Method <c>Select</c> ejecuta un select en la base de datos.
		/// <example>
		/// <code>MyClass resultado = Select&lt;MyClass&gt;(query, parameters);
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
		/// string query = "SELECT * FROM Tabla WHERE Col = @Col";
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
		///		["@Col"] = 1
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
		/// </summary>
		/// <typeparam name="T">El tipo de dato en el que se serializa la respuesta.</typeparam>
		/// <param name="query">La query a ejecutar.</param>
		/// <param name="parameters">Los parámetros de la query.</param>
		/// <returns>Un DataTable con los datos.</returns>
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
		/// </summary>
		/// <param name="database">La base de datos donde se ejecuta.</param>
		/// <param name="query">La query a ejecutar.</param>
		/// <returns>La cantidad de filas afectadas.</returns>
		public int NonQuery(string query, Dictionary<string, object> parameters) {
			int value = -1;
			ExecuteCommand(query, parameters, command => {
				try {
					value = command.ExecuteNonQuery();
				} catch (Exception e) {
					System.Diagnostics.Debug.WriteLine(e.ToString());
				}
			});
			return value;
		}

		/// <summary>
		/// Ejectuta un Store Procedure con valor de retorno, que recibe parámetros.
		/// </summary>
		/// <param name="database">La base de datos donde se ejecuta.</param>
		/// <param name="storeProcedure">El nombre del Store Procedure.</param>
		/// <param name="spParams">
		/// Los parámetros que recibe el SP. La key debe ser el nombre del parámetro
		/// en el SP, incluyendo el @. El value debe ser el objeto a utilizar, con el tipo
		/// de dato igual que en el SP.
		/// </param>
		/// <returns></returns>
		public int StoreProcedureWithCodeRet(string storedProcedure, Dictionary<string, object> spParams) {
			using (SqlConnection connection = new SqlConnection(connectionString)) {

				SqlCommand command = new SqlCommand(storedProcedure, connection) {
					CommandType = CommandType.StoredProcedure
				};

				LoadParamsInCommand(command, spParams);

				SqlParameter output = new SqlParameter {
					Direction = ParameterDirection.ReturnValue
				};
				command.Parameters.Add(output);

				try {
					command.Connection.Open();
					command.ExecuteNonQuery();
					return (int)output.Value;
				} catch (Exception e) {
					System.Diagnostics.Debug.WriteLine(e.ToString());
					return -1;
				}
			}
		}

		private void ExecuteCommand(string query, Dictionary<string, object> parameters, Action<SqlCommand> action) {
			try {
				using (SqlConnection connection = new SqlConnection(connectionString)) {
					connection.Open();
					using (SqlCommand command = new SqlCommand(query, connection)) {
						LoadParamsInCommand(command, parameters);
						action(command);
					}
				}
			} catch (Exception e) {
				System.Diagnostics.Debug.WriteLine(e.Data);
			}
		}

		private void LoadParamsInCommand(SqlCommand command, Dictionary<string, object> parameters) {
			if (parameters != null) {
				foreach (var parameter in parameters) {
					string paramName = parameter.Key.StartsWith("@") ? parameter.Key : "@" + parameter.Key;
					command.Parameters.AddWithValue(paramName, parameter.Value ?? DBNull.Value);
				}
			}
		}

		/// <summary>
		/// Method <c>_MapDataReaderToModel</c> mapea la lectura de la fila con un objeto.
		/// </summary>
		/// <typeparam name="T">El tipo de objeto en el que se mapea.</typeparam>
		/// <param name="reader">El lector.</param>
		/// <param name="item">La fila.</param>
		private void MapDataReaderToModel<T>(SqlDataReader reader, T item) where T : new() {
			var properties = item.GetType().GetRuntimeProperties();

			foreach (var property in properties) {
				// Obtener el atributo ColumnName personalizado
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
					}
				}
			}
		}
	}
}

