# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY EAD-Arham/*.csproj ./EAD-Arham/
RUN dotnet restore ./EAD-Arham/MessManagementSystem.csproj

# Copy everything else and build
COPY EAD-Arham/ ./EAD-Arham/
RUN dotnet publish ./EAD-Arham/MessManagementSystem.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create data directory for SQLite database
RUN mkdir -p /app/data && chmod 777 /app/data

# Copy published app
COPY --from=build /app/publish .

# Expose port 8080 (internal container port)
EXPOSE 8080

# Set environment to Production
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/Account/Login || exit 1

# Entry point
ENTRYPOINT ["dotnet", "MessManagementSystem.dll"]
