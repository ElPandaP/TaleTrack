# MediaTracker
System to automatically track user's media consumption, including films, seres, books and comics

## Levantar solo DB + migraciones (sin .NET local)

Si tu VM no tiene instalado .NET SDK, puedes correr las migraciones con Docker usando el servicio `migrator`.

1. Levanta Postgres:

```bash
docker compose up -d postgres
```

2. Ejecuta migraciones EF Core desde contenedor SDK:

```bash
docker compose --profile db run --rm migrator
```

Esto aplica `dotnet ef database update` contra la base de datos definida en `.env`.
