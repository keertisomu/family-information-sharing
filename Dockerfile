# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY FamilyCalendar.sln .
COPY src/FamilyCalendar.Api/FamilyCalendar.Api.csproj src/FamilyCalendar.Api/

# Restore dependencies
RUN dotnet restore src/FamilyCalendar.Api/FamilyCalendar.Api.csproj

# Copy source code
COPY src/FamilyCalendar.Api/ src/FamilyCalendar.Api/

# Build application
WORKDIR /src/src/FamilyCalendar.Api
RUN dotnet build -c Release

# Publish stage
FROM build AS publish
WORKDIR /src/src/FamilyCalendar.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install PostgreSQL client for migrations
RUN apt-get update && apt-get install -y postgresql-client && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Copy published files
COPY --from=publish /app/publish .

# Set permissions
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 5000

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5000

# Entry point
ENTRYPOINT ["dotnet", "FamilyCalendar.Api.dll"]
