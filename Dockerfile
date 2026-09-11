# --- CSS (Tailwind) ---
FROM node:22-alpine AS css
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci --ignore-scripts
COPY src ./src
COPY HerYerde.Web/Views ./HerYerde.Web/Views
RUN npm run css:build

# --- .NET build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json HerYerde.sln ./
COPY HerYerde.Core/HerYerde.Core.csproj HerYerde.Core/
COPY HerYerde.Entities/HerYerde.Entities.csproj HerYerde.Entities/
COPY HerYerde.DataAccess/HerYerde.DataAccess.csproj HerYerde.DataAccess/
COPY HerYerde.Business/HerYerde.Business.csproj HerYerde.Business/
COPY HerYerde.Web/HerYerde.Web.csproj HerYerde.Web/
RUN dotnet restore HerYerde.Web/HerYerde.Web.csproj
COPY . .
COPY --from=css /src/HerYerde.Web/wwwroot/css/site.css HerYerde.Web/wwwroot/css/site.css
RUN dotnet publish HerYerde.Web/HerYerde.Web.csproj -c Release -o /app --no-restore

# --- Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app .
# Serilog dosya sink'i /app/logs'a yazar; uygulama kullanıcısı root değil.
RUN mkdir -p /app/logs && chown $APP_UID /app/logs
USER $APP_UID
ENTRYPOINT ["dotnet", "HerYerde.Web.dll"]
