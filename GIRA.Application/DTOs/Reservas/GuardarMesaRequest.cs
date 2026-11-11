using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Application.DTOs.Reservas;

public record GuardarMesaRequest(int Numero, int Capacidad, Guid ZonaId);

