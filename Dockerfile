FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine AS build-env
WORKDIR /build

# Copy everything else and build
COPY ./src/Miner/Miner.csproj ./Miner.csproj
RUN dotnet restore

COPY ./src/Miner/ ./
RUN dotnet publish ./Miner.csproj -c Release -o out /property:GenerateFullPaths=true

ENTRYPOINT ["dotnet", "/build/out/Miner.dll"]
