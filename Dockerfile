FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine AS build-env
WORKDIR /build

# Copy everything else and build
COPY ./src/Miner/ ./
RUN dotnet restore
RUN dotnet publish ./Miner.csproj -c Release -o out /property:GenerateFullPaths=true

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:5.0-alpine as runtime
WORKDIR /app

COPY --from=build-env /build/out .

ENTRYPOINT ["dotnet", "Miner.dll"]
