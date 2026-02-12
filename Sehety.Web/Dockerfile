# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["nuget.config", "."]
COPY ["Sehety.Web/S2S.Web.csproj", "Sehety.Web/"]
COPY ["Sehety.Persistence/S2S.Persistence.csproj", "Sehety.Persistence/"]
COPY ["Sehety.Domain/S2S.Domain.csproj", "Sehety.Domain/"]
COPY ["Sehety.Presentation/S2S.Presentation.csproj", "Sehety.Presentation/"]
COPY ["Sehety.ServicesAbstraction/S2S.ServicesAbstraction.csproj", "Sehety.ServicesAbstraction/"]
COPY ["Sehety.Shared/S2S.Shared.csproj", "Sehety.Shared/"]
COPY ["Sehety.Services/S2S.Services.csproj", "Sehety.Services/"]
RUN dotnet restore "./Sehety.Web/S2S.Web.csproj"
COPY . .
WORKDIR "/src/Sehety.Web"
RUN dotnet build "./S2S.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./S2S.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "S2S.Web.dll"]