# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY src/LogForDev/*.csproj ./LogForDev/
RUN dotnet restore LogForDev/LogForDev.csproj

# Copy everything else and build
COPY src/LogForDev/. ./LogForDev/
WORKDIR /src/LogForDev
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create non-root user
RUN adduser --disabled-password --gecos '' appuser

COPY --from=build /app/publish .

# Change ownership and switch to non-root user
RUN chown -R appuser:appuser /app
USER appuser

EXPOSE 5000

ENTRYPOINT ["dotnet", "LogForDev.dll"]
