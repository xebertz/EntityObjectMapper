using System;

namespace DB {
	/// <summary>
	/// Class <c>ColumnNameAttribute</c> mapea una propiedad con un atributo personalizado.
	/// </summary>
	/// <seealso cref="Attribute" />
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
	public class ColumnNameAttribute : Attribute {
		public string ColumnName { get; }

		/// <summary>
		/// Inicializa una nueva instancia de la clase <see cref="ColumnNameAttribute"/>.
		/// </summary>
		/// <param name="columnName">Nombre de la columna.</param>
		public ColumnNameAttribute(string columnName) {
			ColumnName = columnName;
		}
	}
}
