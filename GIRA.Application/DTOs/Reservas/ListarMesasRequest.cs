using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Application.DTOs.Reservas;

public class ListarMesasRequest : ListarSalonRequest
{
    public Guid? ZonaId { get; init; }
}