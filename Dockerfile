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
# Ürün videosu yüklenince sunucuda 720p H.264 mp4 + poster + önizleme üretilir; ffmpeg/ffprobe bunun için
# zorunlu (D15 A1). Öneriler kurulmaz: imajı gereksiz büyütür.
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
# --ithal varsayılan tabloyu depo köküne göre arar; imajda depo kökü /app.
COPY docs/ithal-1.md docs/ithal-2.md ./docs/
# Serilog dosya sink'i /app/logs'a, yüklenen görseller /app/wwwroot/uploads'a, fatura/dekont /app/private'a (kalıcı volume)
# yazar; uygulama kullanıcısı root değil.
RUN mkdir -p /app/logs /app/wwwroot/uploads /app/private && chown $APP_UID /app/logs /app/wwwroot/uploads /app/private
USER $APP_UID
# İmajda curl/wget yok; aynı dll /health'e tek istek atıp çıkış koduyla bildirir.
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD ["dotnet", "HerYerde.Web.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "HerYerde.Web.dll"]
