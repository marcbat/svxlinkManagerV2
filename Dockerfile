# Stage 1: Build .NET Application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS dotnet-builder

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
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

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

# Configure ALSA null device for headless operation (pas de hardware audio en Docker)
RUN echo 'pcm.null { type null }' > /etc/asound.conf && \
    echo 'ctl.null { type null }' >> /etc/asound.conf && \
    echo 'pcm.default { type null }' >> /etc/asound.conf && \
    echo 'ctl.default { type null }' >> /etc/asound.conf

# Create required SVXLink runtime directories
RUN mkdir -p /var/log/svxlink /var/spool/svxlink /etc/svxlink/conf.d

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
