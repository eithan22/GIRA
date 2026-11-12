using System.Security.Cryptography;

namespace GIRA.Infrastructure.Services.Seguridad.Usuarios;

/// <summary>Genera la contraseña temporal que se asigna a un usuario nuevo.</summary>
public static class GeneradorPasswordTemporal
{
    /// <summary>Genera una contraseña de 12 caracteres con mayúscula, minúscula, número y símbolo.</summary>
    public static string Generar()
    {
        const string mayusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string minusculas = "abcdefghijkmnopqrstuvwxyz";
        const string numeros = "23456789";
        const string simbolos = "!@#$%*?";
        const string todos = mayusculas + minusculas + numeros + simbolos;

        var caracteres = new List<char>
        {
            mayusculas[RandomNumberGenerator.GetInt32(mayusculas.Length)],
            minusculas[RandomNumberGenerator.GetInt32(minusculas.Length)],
            numeros[RandomNumberGenerator.GetInt32(numeros.Length)],
            simbolos[RandomNumberGenerator.GetInt32(simbolos.Length)]
        };

        while (caracteres.Count < 12)
            caracteres.Add(todos[RandomNumberGenerator.GetInt32(todos.Length)]);

        // Mezcla para que los 4 tipos no queden siempre al inicio.
        for (var i = caracteres.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (caracteres[i], caracteres[j]) = (caracteres[j], caracteres[i]);
        }

        return new string(caracteres.ToArray());
    }
}
