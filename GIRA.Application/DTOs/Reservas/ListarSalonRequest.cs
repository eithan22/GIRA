using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Application.DTOs.Reservas;


/// <summary>Paginación y filtro comunes de los listados (page/pageSize, máximo 100).</summary>
public class ListarSalonRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    /// <summary>true = solo activas, false = solo inactivas, null = todas.</summary>
    public bool? Activa { get; init; }
}

