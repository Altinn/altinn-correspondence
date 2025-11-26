FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY src/Altinn.Correspondence.Common/*.csproj ./src/Altinn.Correspondence.Common/
COPY src/Altinn.Correspondence.Core/*.csproj ./src/Altinn.Correspondence.Core/
COPY src/Altinn.Correspondence.Persistence/*.csproj ./src/Altinn.Correspondence.Persistence/
COPY src/Altinn.Correspondence.Integrations/*.csproj ./src/Altinn.Correspondence.Integrations/
COPY src/Altinn.Correspondence.Application/*.csproj ./src/Altinn.Correspondence.Application/
COPY src/Altinn.Correspondence.API/*.csproj ./src/Altinn.Correspondence.API/
RUN dotnet restore ./src/Altinn.Correspondence.API/Altinn.Correspondence.API.csproj

# Copy everything else and build
COPY src ./src
RUN dotnet publish -c Release -o out ./src/Altinn.Correspondence.API/Altinn.Correspondence.API.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
EXPOSE 2525
ENV ASPNETCORE_URLS=http://+:2525

COPY --from=build /app/out .

RUN addgroup -g 3000 dotnet && adduser -u 1000 -G dotnet -D -s /bin/false dotnet

RUN mkdir -p /mnt/storage
RUN chown -R dotnet:dotnet /mnt/storage
USER dotnet
ENTRYPOINT [ "dotnet", "Altinn.Correspondence.API.dll" ]
