# Stage 1: Build SVXLink 19.09.2
FROM debian:bookworm-slim AS svxlink-builder

# Install SVXLink build dependencies
RUN apt-get update && apt-get install -y \
    cmake \
    g++ \
    make \
    git \
    libsigc++-2.0-dev \
    libgsm1-dev \
    libpopt-dev \
    tcl8.6-dev \
    libgcrypt20-dev \
    libspeex-dev \
    libasound2-dev \
    libopus-dev \
    librtlsdr-dev \
    libcurl4-openssl-dev \
    groff \
    && rm -rf /var/lib/apt/lists/*

# Clone SVXLink 19.09.2
WORKDIR /tmp
RUN git clone --depth 1 --branch 19.09.2 https://github.com/sm0svx/svxlink.git

# Build SVXLink
WORKDIR /tmp/svxlink/src
RUN mkdir build && cd build && \
    cmake -DCMAKE_INSTALL_PREFIX=/usr \
          -DSYSCONF_INSTALL_DIR=/etc \
          -DLOCAL_STATE_DIR=/var \
          -DUSE_QT=OFF \
          -DBUILD_STATIC_LIBS=OFF \
          .. && \
    make -j$(nproc) && \
    make install

# Download and extract SVXLink sounds (French)
WORKDIR /tmp
RUN apt-get update && apt-get install -y wget && \
    wget https://github.com/sm0svx/svxlink-sounds-fr_FR-heather/releases/download/19.09/svxlink-sounds-fr_FR-heather-16k-19.09.tar.bz2 && \
    mkdir -p /usr/share/svxlink/sounds && \
    tar xjf svxlink-sounds-fr_FR-heather-16k-19.09.tar.bz2 -C /usr/share/svxlink/sounds/ && \
    rm svxlink-sounds-fr_FR-heather-16k-19.09.tar.bz2 && \
    rm -rf /var/lib/apt/lists/*

# Stage 2: Build .NET Application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS dotnet-builder

WORKDIR /src

# Copy solution and project files
COPY SvxlinkManagerV2.sln ./
COPY src/SvxlinkManagerV2.Domain/SvxlinkManagerV2.Domain.csproj ./src/SvxlinkManagerV2.Domain/
COPY src/SvxlinkManagerV2.Application/SvxlinkManagerV2.Application.csproj ./src/SvxlinkManagerV2.Application/
COPY src/SvxlinkManagerV2.Infrastructure/SvxlinkManagerV2.Infrastructure.csproj ./src/SvxlinkManagerV2.Infrastructure/
COPY src/SvxlinkManagerV2.Presentation/SvxlinkManagerV2.Presentation.csproj ./src/SvxlinkManagerV2.Presentation/

# Restore dependencies
RUN dotnet restore

# Copy all source code
COPY src/ ./src/

# Build the Presentation project (Blazor)
WORKDIR /src/src/SvxlinkManagerV2.Presentation
RUN dotnet build -c Release -o /app/build

# Publish
RUN dotnet publish -c Release -o /app/publish

# Stage 3: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

# Install SVXLink runtime dependencies
RUN apt-get update && apt-get install -y \
    libsigc++-2.0-0v5 \
    libgsm1 \
    libpopt0 \
    tcl8.6 \
    libgcrypt20 \
    libspeex1 \
    libasound2 \
    libopus0 \
    librtlsdr0 \
    libcurl4 \
    alsa-utils \
    systemctl \
    && rm -rf /var/lib/apt/lists/*

# Copy SVXLink binaries and libraries from builder
COPY --from=svxlink-builder /usr/bin/svxlink /usr/bin/
COPY --from=svxlink-builder /usr/bin/remotetrx /usr/bin/
COPY --from=svxlink-builder /usr/lib/libsvxmisc.so* /usr/lib/
COPY --from=svxlink-builder /usr/lib/libasynccpp.so* /usr/lib/
COPY --from=svxlink-builder /usr/lib/libasynaudio.so* /usr/lib/
COPY --from=svxlink-builder /usr/lib/libasynccore.so* /usr/lib/
COPY --from=svxlink-builder /usr/lib/libasyncqt.so* /usr/lib/
COPY --from=svxlink-builder /usr/lib/libecholib.so* /usr/lib/

# Copy SVXLink configuration directories
COPY --from=svxlink-builder /usr/share/svxlink/ /usr/share/svxlink/

# Create necessary directories
RUN mkdir -p /etc/svxlink /var/spool/svxlink /var/log/svxlink && \
    chmod 755 /var/spool/svxlink /var/log/svxlink

# Copy .NET application
WORKDIR /app
COPY --from=dotnet-builder /app/publish .

# Create logs directory for the application
RUN mkdir -p /app/logs

# Expose HTTP port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

# Run the application
ENTRYPOINT ["dotnet", "SvxlinkManagerV2.Presentation.dll"]
