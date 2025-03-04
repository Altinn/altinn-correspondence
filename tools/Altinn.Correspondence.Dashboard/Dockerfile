# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 2526
ENV ASPNETCORE_URLS=http://+:2526

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["tools/Altinn.Correspondence.Dashboard/Altinn.Correspondence.Dashboard.csproj", "tools/Altinn.Correspondence.Dashboard/"]
COPY ["src/Altinn.Correspondence.Persistence/Altinn.Correspondence.Persistence.csproj", "src/Altinn.Correspondence.Persistence/"]
COPY ["src/Altinn.Correspondence.Integrations/Altinn.Correspondence.Integrations.csproj", "src/Altinn.Correspondence.Integrations/"]

RUN dotnet restore "tools/Altinn.Correspondence.Dashboard/Altinn.Correspondence.Dashboard.csproj"
COPY . .
WORKDIR "/src/tools/Altinn.Correspondence.Dashboard"
RUN dotnet build "Altinn.Correspondence.Dashboard.csproj" -c Release -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
RUN dotnet publish "Altinn.Correspondence.Dashboard.csproj" -c Release -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Altinn.Correspondence.Dashboard.dll"]