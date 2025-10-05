# Multi-stage build for ItemMaster Lambda solution

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy everything
COPY . .

# Restore
RUN dotnet restore ItemMaster.sln

# Build
RUN dotnet build ItemMaster.sln -c Release --no-restore

RUN dotnet test ItemMaster.sln -c Release --no-build --verbosity minimal

RUN dotnet publish ItemMaster.Lambda/src/ItemMaster.Lambda/ItemMaster.Lambda.csproj -c Release -o /app/publish --no-build

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

CMD ["/bin/sh","-c","ls -1"]

