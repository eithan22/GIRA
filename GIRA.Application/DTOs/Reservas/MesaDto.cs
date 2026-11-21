using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Application.DTOs.Reservas;

public record MesaDto(Guid Id, int Numero, int Capacidad, Guid ZonaId, string ZonaNombre, bool Activa);

