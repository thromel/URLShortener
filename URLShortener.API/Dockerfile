# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["URLShortener.API/URLShortener.API.csproj", "URLShortener.API/"]
COPY ["src/URLShortener.Core/URLShortener.Core.csproj", "src/URLShortener.Core/"]
COPY ["src/URLShortener.Infrastructure/URLShortener.Infrastructure.csproj", "src/URLShortener.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "URLShortener.API/URLShortener.API.csproj"

# Copy source code
COPY . .

# Build the application
WORKDIR "/src/URLShortener.API"
RUN dotnet build "URLShortener.API.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "URLShortener.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create non-root user
RUN addgroup --system --gid 1001 urlshortener && \
    adduser --system --uid 1001 --ingroup urlshortener urlshortener

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=publish /app/publish .

# Create logs directory
RUN mkdir -p /app/logs && chown -R urlshortener:urlshortener /app

# Switch to non-root user
USER urlshortener

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Set entry point
ENTRYPOINT ["dotnet", "URLShortener.API.dll"] 