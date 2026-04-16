FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . .

RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "TaleTrackApp.dll"]

# Para produccion
# docker build -t backend-prod .
# docker run -p 8080:8080 backend-prod

# Para desarrollo (con hot reload)
# docker build -t backend-dev --target build .
# docker run -p 8080:8080 backend-dev dotnet run --urls http://0.0.0.0:8080