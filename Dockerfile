# Base stage for runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
# Heroku uses dynamic ports, so we don't fix one here
# The port is handled in Program.cs via Environment Variable

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy all csproj files and restore
COPY ["Sehety.Web/S2S.Web.csproj", "Sehety.Web/"]
COPY ["Sehety.Persistence/S2S.Persistence.csproj", "Sehety.Persistence/"]
COPY ["Sehety.Presentation/S2S.Presentation.csproj", "Sehety.Presentation/"]
COPY ["Sehety.Services/S2S.Services.csproj", "Sehety.Services/"]
COPY ["Sehety.Domain/S2S.Domain.csproj", "Sehety.Domain/"]
COPY ["Sehety.ServicesAbstraction/S2S.ServicesAbstraction.csproj", "Sehety.ServicesAbstraction/"]
COPY ["Sehety.Shared/S2S.Shared.csproj", "Sehety.Shared/"]

RUN dotnet restore "Sehety.Web/S2S.Web.csproj"

# Copy the rest of the source code
COPY . .
WORKDIR "/src/Sehety.Web"
RUN dotnet build "S2S.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "S2S.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Run the app with the DLL name
ENTRYPOINT ["dotnet", "S2S.Web.dll"]
