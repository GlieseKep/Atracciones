namespace AtraccionesService.DataManagment.Exceptions;

/// <summary>Error de persistencia expresado sin detalles del proveedor.</summary>
public class DataManagementException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>Otra transacción modificó el registro o se violó una restricción de unicidad.</summary>
public sealed class ConcurrencyException(string message, Exception? innerException = null) : DataManagementException(message, innerException);
