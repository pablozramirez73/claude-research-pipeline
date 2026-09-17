# See src/VitalFace.Api/Dockerfile for the .NET 11 vs .NET 10 note — same applies here.
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY src/VitalFace.Core/VitalFace.Core.csproj src/VitalFace.Core/
COPY src/VitalFace.Contracts/VitalFace.Contracts.csproj src/VitalFace.Contracts/
COPY src/VitalFace.Web/VitalFace.Web.csproj src/VitalFace.Web/
RUN dotnet restore src/VitalFace.Web/VitalFace.Web.csproj

COPY src/VitalFace.Core/ src/VitalFace.Core/
COPY src/VitalFace.Contracts/ src/VitalFace.Contracts/
COPY src/VitalFace.Web/ src/VitalFace.Web/

RUN dotnet publish src/VitalFace.Web/VitalFace.Web.csproj -c Release -o /app/publish --no-restore

FROM nginx:1.27-alpine AS runtime
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY src/VitalFace.Web/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 8080
