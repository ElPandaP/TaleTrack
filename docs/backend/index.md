# Backend

API REST en ASP.NET Core 10 con Minimal APIs, organizada según el patrón REPR (Request → Endpoint → Response). Cada funcionalidad vive en `TaleTrackApp/Features/<Funcionalidad>/`.

La [referencia de la API](api/TaleTrackApp.yml) se genera a partir de los comentarios XML (`/// <summary>`) del código. Los contratos HTTP de los endpoints (rutas, cuerpos y respuestas) se consultan en Swagger UI (`/swagger`) con el backend en marcha.
