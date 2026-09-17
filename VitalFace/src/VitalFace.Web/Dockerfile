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
RUN apk add --no-cache gettext
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY src/VitalFace.Web/nginx.conf /etc/nginx/conf.d/default.conf
COPY src/VitalFace.Web/docker/appsettings.Production.template.json /usr/share/nginx/html/appsettings.Production.template.json
COPY src/VitalFace.Web/docker/docker-entrypoint.sh /docker-entrypoint.sh
# Defensive normalization: strips any CRLF line endings a Windows checkout (autocrlf=true) may
# have introduced despite .gitattributes — without this, Linux fails to exec the script with a
# misleading "no such file or directory" because the shebang becomes "#!/bin/sh\r".
RUN sed -i 's/\r$//' /docker-entrypoint.sh && chmod +x /docker-entrypoint.sh

EXPOSE 8080
ENTRYPOINT ["/docker-entrypoint.sh"]
CMD ["nginx", "-g", "daemon off;"]
