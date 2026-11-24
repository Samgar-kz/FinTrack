# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the NuGet layer is cached until a project file changes.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/FinTrack.Domain/FinTrack.Domain.csproj src/FinTrack.Domain/
COPY src/FinTrack.Application/FinTrack.Application.csproj src/FinTrack.Application/
COPY src/FinTrack.Infrastructure/FinTrack.Infrastructure.csproj src/FinTrack.Infrastructure/
COPY src/FinTrack.Api/FinTrack.Api.csproj src/FinTrack.Api/
RUN dotnet restore src/FinTrack.Api/FinTrack.Api.csproj

COPY src/ src/
RUN dotnet publish src/FinTrack.Api/FinTrack.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Run as the image's built-in non-root user.
USER $APP_UID
COPY --from=build /app .
ENTRYPOINT ["dotnet", "FinTrack.Api.dll"]
