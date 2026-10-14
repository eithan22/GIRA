using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Domain.Constants
{
    public static class Politicas
    {
        // Seguridad
        /// <summary>Crear, editar, desactivar usuarios y asignar roles. Solo Administrador.</summary>
        public const string GestionarUsuarios = nameof(GestionarUsuarios);

        // Reservas
        /// <summary>Agenda del día, check-in, walk-ins y lista de espera.</summary>
        public const string OperarReservas = nameof(OperarReservas);
        /// <summary>Zonas, mesas y franjas horarias.</summary>
        public const string GestionarSalon = nameof(GestionarSalon);

        // Inventario
        /// <summary>Consultar insumos y recetas.</summary>
        public const string ConsultarInventario = nameof(ConsultarInventario);
        /// <summary>Gestionar insumos, platos, recetas y proveedores.</summary>
        public const string GestionarInventario = nameof(GestionarInventario);
        /// <summary>Registrar entradas, mermas y ajustes.</summary>
        public const string RegistrarMovimientos = nameof(RegistrarMovimientos);
        /// <summary>Ver y atender alertas, kardex y proyección de consumo.</summary>
        public const string AtenderAlertasInventario = nameof(AtenderAlertasInventario);

        // Personal
        /// <summary>Gestionar empleados, cargos y ausencias.</summary>
        public const string GestionarPersonal = nameof(GestionarPersonal);
        /// <summary>Asignar turnos y resolver sugerencias.</summary>
        public const string AsignarTurnos = nameof(AsignarTurnos);
        /// <summary>Ver los turnos propios y registrar disponibilidad (todo el staff).</summary>
        public const string ConsultarMisTurnos = nameof(ConsultarMisTurnos);

        // Analítica
        /// <summary>Dashboard, KPIs, historial y recomendaciones.</summary>
        public const string VerDashboard = nameof(VerDashboard);
        /// <summary>Ver predicciones (el Supervisor solo tiene lectura).</summary>
        public const string VerPrediccion = nameof(VerPrediccion);
        /// <summary>Entrenar el modelo y ver sus métricas.</summary>
        public const string GestionarModelos = nameof(GestionarModelos);

        // Configuración
        /// <summary>Parámetros, horarios y días cerrados del restaurante.</summary>
        public const string GestionarConfiguracion = nameof(GestionarConfiguracion);
        /// <summary>Plantillas de correo. Solo Administrador.</summary>
        public const string GestionarPlantillasCorreo = nameof(GestionarPlantillasCorreo);
    }
}
