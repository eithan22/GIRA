using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Application.DTOs.Reservas;

public record GuardarFranjaRequest(string Nombre, TimeOnly HoraInicio, TimeOnly HoraFin);

