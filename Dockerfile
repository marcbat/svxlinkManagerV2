# Stage 1: Build .NET Application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS dotnet-builder

# Version injectée lors du build. Par défaut 0.0.1-dev pour que l'environnement
# Docker/développement soit toujours inférieur aux releases publiées par le pipeline.
ARG APP_VERSION=0.0.1-dev

WORKDIR /src

# Copy only source project files (not tests)
COPY src/SvxlinkManagerV2.Domain/SvxlinkManagerV2.Domain.csproj ./src/SvxlinkManagerV2.Domain/
COPY src/SvxlinkManagerV2.Application/SvxlinkManagerV2.Application.csproj ./src/SvxlinkManagerV2.Application/
COPY src/SvxlinkManagerV2.Infrastructure/SvxlinkManagerV2.Infrastructure.csproj ./src/SvxlinkManagerV2.Infrastructure/
COPY src/SvxlinkManagerV2.Presentation/SvxlinkManagerV2.Presentation.csproj ./src/SvxlinkManagerV2.Presentation/

# Restore dependencies for each project
WORKDIR /src/src/SvxlinkManagerV2.Presentation
RUN dotnet restore

# Copy all source code
COPY src/ /src/src/

# Build the Presentation project (Blazor)
RUN dotnet build -c Release -o /app/build

# Publish
RUN dotnet publish -c Release -o /app/publish -p:InformationalVersion=${APP_VERSION}

# Stage 2: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# Set timezone
ENV TZ=Europe/Paris
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# Install SVXLink server via apt-get (plus simple que la compilation)
RUN apt-get update && apt-get install -qq -y \
    svxlink-server \
    svxreflector \
    procps \
    alsa-utils \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Copy .NET application
WORKDIR /app
COPY --from=dotnet-builder /app/publish .
COPY deploy/linux/install-update.sh /app/install-update.sh

# Create logs directory for the application
RUN mkdir -p /app/logs && chmod 0755 /app/install-update.sh

# Expose HTTP port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

# Run the application
ENTRYPOINT ["dotnet", "SvxlinkManagerV2.Presentation.dll"]
