# EntityObjectMapper

Librería de C# para simplificar la interacción con bases de datos SQL Server, con soporte para mapeo automático de resultados a objetos fuertemente tipados.

## Descripción

**EntityObjectMapper** provee la clase `DBController`, que permite ejecutar consultas SQL y procedimientos almacenados sobre una base de datos SQL Server, gestionando la conexión, los parámetros y el mapeo de filas a objetos de C# de forma sencilla.

El mapeo de columnas a propiedades se realiza mediante el atributo `[ColumnName]`, que indica qué columna de la base de datos corresponde a cada propiedad del modelo.

## Requisitos

- .NET Framework 4.8
- SQL Server
- Paquete NuGet: `System.Data.SqlClient` 4.8.6

## Instalación

1. Clona el repositorio:
   ```bash
   git clone https://github.com/xebertz/EntityObjectMapper.git
   ```
2. Agrega el proyecto `DB.csproj` a tu solución, o compila la librería y referencia el ensamblado `MigracionDBController.dll` en tu proyecto.

3. Restaura los paquetes NuGet:
   ```bash
   nuget restore DB.sln
   ```

## Uso

### Configuración de la conexión

```csharp
using DB;

string connectionString = "Server=miServidor;Database=miBaseDeDatos;User Id=usuario;Password=contraseña;";
using (DBController db = new DBController(connectionString)) {
    db.Connect();
    // ... operaciones con la base de datos
}
```

### Definición del modelo con `[ColumnName]`

Decora las propiedades de tu clase con el atributo `[ColumnName]` para indicar el nombre de la columna correspondiente en la tabla:

```csharp
using DB;

public class Producto {
    [ColumnName("ProductoID")]
    public int Id { get; set; }

    [ColumnName("Nombre")]
    public string Nombre { get; set; }

    [ColumnName("Precio")]
    public decimal Precio { get; set; }
}
```

### SELECT con mapeo a objeto

Ejecuta una consulta y obtiene los resultados como una lista de objetos tipados:

```csharp
string query = "SELECT ProductoID, Nombre, Precio FROM Productos WHERE Precio > @PrecioMin";
Dictionary<string, object> parameters = new Dictionary<string, object> {
    ["@PrecioMin"] = 100.0m
};

List<Producto> productos = db.Select<Producto>(query, parameters);
```

### SELECT como DataTable

Obtén los resultados en un `DataTable` cuando no necesites mapeo a un tipo específico:

```csharp
string query = "SELECT * FROM Productos";
Dictionary<string, object> parameters = null;

DataTable tabla = db.Select(query, parameters);
```

### NonQuery (INSERT, UPDATE, DELETE)

Ejecuta consultas que modifican datos y obtén la cantidad de filas afectadas:

```csharp
string query = "DELETE FROM Productos WHERE ProductoID = @ID";
Dictionary<string, object> parameters = new Dictionary<string, object> {
    ["@ID"] = 5
};

int filasAfectadas = db.NonQuery(query, parameters);
```

### Procedimientos almacenados

Ejecuta un stored procedure sin valor de retorno:

```csharp
string sp = "ActualizarStock";
Dictionary<string, object> spParams = new Dictionary<string, object> {
    ["@ProductoID"] = 10,
    ["@NuevoStock"] = 50
};

db.StoredProcedure(sp, spParams);
```

Ejecuta un stored procedure y obtén su código de retorno:

```csharp
string sp = "GenerarReporte";
Dictionary<string, object> spParams = new Dictionary<string, object> {
    ["@Mes"] = "Enero",
    ["@Anio"] = 2024
};

int statusCode = db.StoredProcedureWithCodeRet(sp, spParams);
```

Ejecuta un stored procedure con parámetro de salida:

```csharp
string sp = "ObtenerNuevoID";
Dictionary<string, object> spParams = new Dictionary<string, object> {
    ["@Tabla"] = "Productos",
    ["@NuevoID"] = 0  // parámetro OUTPUT
};

int nuevoId = db.StoredProcedureWithOutput(sp, spParams, "@NuevoID");
```

### Transacciones

```csharp
db.Connect();
db.BeginTransaction(DBController.IsolationLevel.ReadCommitted);
try {
    db.NonQuery("INSERT INTO Logs (Mensaje) VALUES (@Msg)", new Dictionary<string, object> { ["@Msg"] = "inicio" });
    db.NonQuery("UPDATE Productos SET Stock = Stock - 1 WHERE ProductoID = @ID", new Dictionary<string, object> { ["@ID"] = 1 });
    db.CommitTransaction();
} catch {
    db.RollbackTransaction();
    throw;
}
```

Los niveles de aislamiento disponibles son:

| Nivel               | Descripción                                             |
|---------------------|---------------------------------------------------------|
| `ReadUncommitted`   | Lee datos sin confirmar (menor aislamiento)             |
| `ReadCommitted`     | Lee solo datos confirmados (valor por defecto)          |
| `RepeatableRead`    | Evita lecturas no repetibles                            |
| `Serializable`      | Máximo aislamiento, sin lecturas fantasma               |
| `Snapshot`          | Lee una versión consistente de los datos                |

## API

### `DBController`

| Método | Descripción |
|--------|-------------|
| `Connect()` | Abre la conexión con la base de datos. |
| `CloseConnection()` | Cierra la conexión. |
| `Dispose()` | Libera la conexión y los recursos de la transacción. |
| `BeginTransaction(IsolationLevel)` | Inicia una transacción con el nivel de aislamiento indicado. |
| `CommitTransaction()` | Confirma la transacción activa. |
| `RollbackTransaction()` | Revierte la transacción activa. |
| `Select<T>(query, parameters)` | Ejecuta un SELECT y mapea los resultados a una lista de `T`. |
| `Select(query, parameters)` | Ejecuta un SELECT y retorna un `DataTable`. |
| `NonQuery(query, parameters)` | Ejecuta un INSERT, UPDATE o DELETE y retorna las filas afectadas. |
| `StoredProcedure(sp, params)` | Ejecuta un stored procedure sin valor de retorno. |
| `StoredProcedureWithCodeRet(sp, params)` | Ejecuta un stored procedure y retorna su código de retorno. |
| `StoredProcedureWithOutput(sp, params, outputParam)` | Ejecuta un stored procedure y retorna el valor de un parámetro OUTPUT. |

### `ColumnNameAttribute`

Atributo aplicable a propiedades de una clase para indicar el nombre de la columna de la base de datos que le corresponde.

```csharp
[ColumnName("NombreColumna")]
public string MiPropiedad { get; set; }
```

## Licencia

Este proyecto no incluye una licencia explícita. Para más información, contacta al autor.
