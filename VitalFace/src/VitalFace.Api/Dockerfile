# NOTE: the technical brief targets ".NET 11", which has not shipped as of this writing (the
# current LTS is .NET 10, released Nov 2025). This image tracks .NET 10 and should be bumped to
# 11.0 once that SDK/runtime is generally available — see README "Note tecniche e rischi".
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY src/VitalFace.Core/VitalFace.Core.csproj src/VitalFace.Core/
COPY src/VitalFace.Contracts/VitalFace.Contracts.csproj src/VitalFace.Contracts/
COPY src/VitalFace.Infrastructure/VitalFace.Infrastructure.csproj src/VitalFace.Infrastructure/
COPY src/VitalFace.Api/VitalFace.Api.csproj src/VitalFace.Api/
RUN dotnet restore src/VitalFace.Api/VitalFace.Api.csproj

COPY src/VitalFace.Core/ src/VitalFace.Core/
COPY src/VitalFace.Contracts/ src/VitalFace.Contracts/
COPY src/VitalFace.Infrastructure/ src/VitalFace.Infrastructure/
COPY src/VitalFace.Api/ src/VitalFace.Api/

RUN dotnet publish src/VitalFace.Api/VitalFace.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
# Npgsql probes for GSSAPI (Kerberos) support at connection time regardless of whether the
# configured auth actually uses it (password auth here does not); without this, that probe fails
# with a "Cannot load library libgssapi_krb5.so.2" log line on every connection attempt — harmless
# since password auth doesn't need it, but noisy enough to look like a real startup error while
# debugging.
RUN apk add --no-cache krb5-libs
RUN addgroup -S vitalface && adduser -S vitalface -G vitalface
COPY --from=build /app/publish .
USER vitalface

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "VitalFace.Api.dll"]
