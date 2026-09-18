FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY KineMotion.slnx .
COPY src/KineMotion.Core/KineMotion.Core.csproj src/KineMotion.Core/
COPY src/KineMotion.Contracts/KineMotion.Contracts.csproj src/KineMotion.Contracts/
COPY src/KineMotion.Infrastructure/KineMotion.Infrastructure.csproj src/KineMotion.Infrastructure/
COPY src/KineMotion.Api/KineMotion.Api.csproj src/KineMotion.Api/
RUN dotnet restore src/KineMotion.Api/KineMotion.Api.csproj

COPY src/KineMotion.Core/ src/KineMotion.Core/
COPY src/KineMotion.Contracts/ src/KineMotion.Contracts/
COPY src/KineMotion.Infrastructure/ src/KineMotion.Infrastructure/
COPY src/KineMotion.Api/ src/KineMotion.Api/

RUN dotnet publish src/KineMotion.Api/KineMotion.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "KineMotion.Api.dll"]
