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

# Stage 2a: Build SVXLink 19.09.2 (legacy) from source
FROM debian:bookworm AS svxlink-legacy-builder

RUN apt-get update && apt-get install -y --no-install-recommends \
    git cmake g++ make ca-certificates \
    libsigc++-2.0-dev libgsm1-dev libpopt-dev tcl8.6-dev \
    libgcrypt20-dev libspeex-dev libasound2-dev libopus-dev \
    libcurl4-openssl-dev libssl-dev \
    && rm -rf /var/lib/apt/lists/*

RUN git clone --depth 1 --branch 19.09.2 \
    https://github.com/sm0svx/svxlink.git /svxlink-src

RUN mkdir /svxlink-build && cd /svxlink-build && \
    cmake -DCMAKE_INSTALL_PREFIX=/opt/svxlink-legacy \
          -DUSE_QT=OFF \
          -DCMAKE_BUILD_TYPE=Release \
          /svxlink-src/src && \
    make -j$(nproc) && \
    make install DESTDIR=/svxlink-install

# Stage 2b: Build SVXLink 25.05 (modern) from source
FROM debian:bookworm AS svxlink-modern-builder

ARG SVXLINK_VERSION=25.05

RUN apt-get update && apt-get install -y --no-install-recommends \
    git cmake g++ make ca-certificates \
    libsigc++-2.0-dev libgsm1-dev libpopt-dev tcl8.6-dev \
    libgcrypt20-dev libspeex-dev libasound2-dev libopus-dev \
    libcurl4-openssl-dev libssl-dev libjsoncpp-dev \
    && rm -rf /var/lib/apt/lists/*

RUN git clone --depth 1 --branch ${SVXLINK_VERSION} \
    https://github.com/sm0svx/svxlink.git /svxlink-src

RUN mkdir /svxlink-build && cd /svxlink-build && \
    cmake -DCMAKE_INSTALL_PREFIX=/opt/svxlink-modern \
          -DUSE_QT=OFF \
          -DCMAKE_BUILD_TYPE=Release \
          /svxlink-src/src && \
    make -j$(nproc) && \
    make install DESTDIR=/svxlink-install

# Stage 3: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# Set timezone
ENV TZ=Europe/Paris
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# Install SVXLink runtime dependencies (both versions)
RUN apt-get update && apt-get install -y --no-install-recommends \
    libsigc++-2.0-0v5 libgsm1 libpopt0 tcl8.6 libgcrypt20 \
    libspeex1 libasound2 libopus0 libcurl4 libssl3 libjsoncpp25 \
    procps alsa-utils \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Copy SVXLink legacy (19.09.2) from builder → /opt/svxlink-legacy
COPY --from=svxlink-legacy-builder /svxlink-install/opt/svxlink-legacy /opt/svxlink-legacy

# Copy SVXLink modern (25.05) from builder → /opt/svxlink-modern
COPY --from=svxlink-modern-builder /svxlink-install/opt/svxlink-modern /opt/svxlink-modern

# Copy Logic.tcl event handler into both version trees
RUN mkdir -p /opt/svxlink-legacy/share/svxlink/events.d/local \
             /opt/svxlink-modern/share/svxlink/events.d/local
COPY src/SvxlinkManagerV2.Infrastructure/SvxLink/Resources/Logic.tcl /opt/svxlink-legacy/share/svxlink/events.d/local/Logic.tcl
COPY src/SvxlinkManagerV2.Infrastructure/SvxLink/Resources/Logic.tcl /opt/svxlink-modern/share/svxlink/events.d/local/Logic.tcl

# Register shared libraries for both versions
RUN echo "/opt/svxlink-legacy/lib" > /etc/ld.so.conf.d/svxlink-legacy.conf && \
    echo "/opt/svxlink-modern/lib" > /etc/ld.so.conf.d/svxlink-modern.conf && \
    ldconfig

# Create required directories
RUN mkdir -p /var/spool/svxlink /var/log/svxlink /var/lib/svxlink/pki \
    /opt/svxlink-legacy/share/svxlink/sounds/fr_FR/svxlinkmanager \
    /opt/svxlink-modern/share/svxlink/sounds/fr_FR/svxlinkmanager \
    /etc/svxlink

# Copy svxreflector CA hook for auto-signing certificates (dev mode)
COPY deploy/docker/dev-ca-hook.sh /usr/local/bin/dev-ca-hook.sh
RUN chmod +x /usr/local/bin/dev-ca-hook.sh

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
