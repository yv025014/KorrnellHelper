# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy just the project files first so `dotnet restore` is cached across builds unless a
# .csproj itself changes — copying the whole source tree before restore (as this file
# originally did) invalidates that cache on every single source edit.
COPY KorrnellHelper.Api.csproj ./
COPY KorrnellHelper.Domain/KorrnellHelper.Domain.csproj KorrnellHelper.Domain/
COPY KorrnellHelper.Application/KorrnellHelper.Application.csproj KorrnellHelper.Application/
COPY KorrnellHelper.Infrastructure/KorrnellHelper.Infrastructure.csproj KorrnellHelper.Infrastructure/
RUN dotnet restore KorrnellHelper.Api.csproj

COPY . .
RUN dotnet publish KorrnellHelper.Api.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Run as a non-root user rather than the image's default root.
RUN adduser --disabled-password --gecos "" appuser
COPY --from=build /app/publish .
RUN chown -R appuser:appuser /app
USER appuser

# Cloud Run injects PORT (defaults to 8080) and expects the container to listen on it.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "KorrnellHelper.Api.dll"]
