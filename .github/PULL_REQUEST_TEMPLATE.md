## Qué cambia y por qué

<!-- Resumen corto del cambio y la motivación. -->

## Ticket de Jira

<!-- Ej. GI-123 -->

## Checklist

- [ ] Build sin advertencias (`dotnet build`)
- [ ] Tests en verde (`dotnet test --filter "Category!=Integration"`)
- [ ] Sin secretos en el diff (contraseñas, cadenas de conexión, claves)
- [ ] Namespace = carpeta en los archivos nuevos o movidos
- [ ] Políticas de autorización revisadas (si el cambio toca endpoints o roles)
- [ ] Cambios de base de datos o migraciones indicados explícitamente
- [ ] Documentación actualizada (CLAUDE.md, docs/, README.md) si aplica
