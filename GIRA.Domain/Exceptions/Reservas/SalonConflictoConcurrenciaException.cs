using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Domain.Exceptions.Reservas;

// <summary>Otra persona modificó la zona, mesa o franja mientras se editaba (falla RowVersion).</summary>
public class SalonConflictoConcurrenciaException : Exception
{
    public SalonConflictoConcurrenciaException(string mensaje, Exception innerException) : base(mensaje, innerException) { }
}
