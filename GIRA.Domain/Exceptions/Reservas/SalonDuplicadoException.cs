using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Domain.Exceptions.Reservas;

public class SalonDuplicadoException : Exception
{
    public SalonDuplicadoException(string mensaje, Exception innerException) : base(mensaje, innerException) { }
}
