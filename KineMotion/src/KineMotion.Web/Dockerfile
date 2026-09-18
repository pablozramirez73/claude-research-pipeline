FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/KineMotion.Contracts/KineMotion.Contracts.csproj src/KineMotion.Contracts/
COPY src/KineMotion.Web/KineMotion.Web.csproj src/KineMotion.Web/
RUN dotnet restore src/KineMotion.Web/KineMotion.Web.csproj

COPY src/KineMotion.Contracts/ src/KineMotion.Contracts/
COPY src/KineMotion.Web/ src/KineMotion.Web/

RUN dotnet publish src/KineMotion.Web/KineMotion.Web.csproj -c Release -o /app/publish --no-restore

FROM nginx:1.27-alpine AS runtime
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY src/KineMotion.Web/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 8080
