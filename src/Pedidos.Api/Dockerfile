# Contexto de build: a raiz do repositório (docker compose build api).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore em camada própria: só refaz quando algum .csproj ou a configuração de pacotes muda.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Pedidos.Domain/Pedidos.Domain.csproj src/Pedidos.Domain/
COPY src/Pedidos.Application/Pedidos.Application.csproj src/Pedidos.Application/
COPY src/Pedidos.Infrastructure/Pedidos.Infrastructure.csproj src/Pedidos.Infrastructure/
COPY src/Pedidos.Api/Pedidos.Api.csproj src/Pedidos.Api/
RUN dotnet restore src/Pedidos.Api/Pedidos.Api.csproj

COPY src ./src
RUN dotnet publish src/Pedidos.Api/Pedidos.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# A imagem já traz o usuário não-root "app".
USER $APP_UID
ENTRYPOINT ["dotnet", "Pedidos.Api.dll"]
