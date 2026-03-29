FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copy build configuration files first (needed for restore)
COPY Directory.Build.props Nexora.sln global.json ./

# Copy project files for restore layer caching
COPY src/Nexora.SharedKernel/Nexora.SharedKernel.csproj src/Nexora.SharedKernel/
COPY src/Nexora.Infrastructure/Nexora.Infrastructure.csproj src/Nexora.Infrastructure/
COPY src/Nexora.Host/Nexora.Host.csproj src/Nexora.Host/
COPY src/Modules/Nexora.Modules.Identity/Nexora.Modules.Identity.csproj src/Modules/Nexora.Modules.Identity/
COPY src/Modules/Nexora.Modules.Contacts/Nexora.Modules.Contacts.csproj src/Modules/Nexora.Modules.Contacts/
COPY src/Modules/Nexora.Modules.Documents/Nexora.Modules.Documents.csproj src/Modules/Nexora.Modules.Documents/
COPY src/Modules/Nexora.Modules.Notifications/Nexora.Modules.Notifications.csproj src/Modules/Nexora.Modules.Notifications/
COPY src/Modules/Nexora.Modules.Reporting/Nexora.Modules.Reporting.csproj src/Modules/Nexora.Modules.Reporting/
COPY src/Modules/Nexora.Modules.Audit/Nexora.Modules.Audit.csproj src/Modules/Nexora.Modules.Audit/

# Copy test projects too (needed for sln restore)
COPY tests/Nexora.SharedKernel.Tests/Nexora.SharedKernel.Tests.csproj tests/Nexora.SharedKernel.Tests/
COPY tests/Nexora.Infrastructure.Tests/Nexora.Infrastructure.Tests.csproj tests/Nexora.Infrastructure.Tests/
COPY tests/Nexora.Modules.Identity.Tests/Nexora.Modules.Identity.Tests.csproj tests/Nexora.Modules.Identity.Tests/
COPY tests/Nexora.Modules.Contacts.Tests/Nexora.Modules.Contacts.Tests.csproj tests/Nexora.Modules.Contacts.Tests/
COPY tests/Nexora.Modules.Documents.Tests/Nexora.Modules.Documents.Tests.csproj tests/Nexora.Modules.Documents.Tests/
COPY tests/Nexora.Modules.Notifications.Tests/Nexora.Modules.Notifications.Tests.csproj tests/Nexora.Modules.Notifications.Tests/
COPY tests/Nexora.Modules.Reporting.Tests/Nexora.Modules.Reporting.Tests.csproj tests/Nexora.Modules.Reporting.Tests/
COPY tests/Nexora.Architecture.Tests/Nexora.Architecture.Tests.csproj tests/Nexora.Architecture.Tests/
COPY tests/Nexora.Api.ContractTests/Nexora.Api.ContractTests.csproj tests/Nexora.Api.ContractTests/
COPY tests/Nexora.Modules.Identity.IntegrationTests/Nexora.Modules.Identity.IntegrationTests.csproj tests/Nexora.Modules.Identity.IntegrationTests/

RUN dotnet restore

# Copy everything and publish
COPY . .
RUN dotnet publish src/Nexora.Host/Nexora.Host.csproj -c Release -o /app --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# curl is needed for HEALTHCHECK; installed from the base image's Alpine package repository (not version-pinned)
RUN apk add --no-cache curl \
    && addgroup -S nexora && adduser -S nexora -G nexora
USER nexora

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5000

HEALTHCHECK --interval=10s --timeout=3s --start-period=5s --retries=3 \
  CMD curl --fail --silent http://localhost:5000/health/ready || exit 1

ENTRYPOINT ["dotnet", "Nexora.Host.dll"]
