using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Application.DTOs.Reservas;

public record FranjaDto(Guid Id, string Nombre, TimeOnly HoraInicio, TimeOnly HoraFin, bool Activa);

