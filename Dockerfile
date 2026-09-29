# ---- Stage 1: build the CSS (Tailwind + DaisyUI) ----
# Tailwind v4 emits utility/component classes only for selectors found in the
# scanned source files. The UI spans two projects — the server's Razor
# components (OpenMenu.Web) and the WebAssembly admin pages
# (OpenMenu.Web.Client) — and app.css picks both up via @source directives.
# The copied layout mirrors the repo, so the relative @source paths resolve:
#   /web/OpenMenu.Web/wwwroot/app.css  ->  @source "../"           = /web/OpenMenu.Web
#                                      ->  @source "../../..."     = /web/OpenMenu.Web.Client
FROM node:22-alpine AS css
WORKDIR /web
COPY src/OpenMenu.Web/package.json src/OpenMenu.Web/package-lock.json ./
RUN npm ci
COPY src/OpenMenu.Web/ ./OpenMenu.Web/
COPY src/OpenMenu.Web.Client/ ./OpenMenu.Web.Client/
RUN npx tailwindcss -i ./OpenMenu.Web/wwwroot/app.css -o ./OpenMenu.Web/wwwroot/css/app.min.css --minify

# ---- Stage 2: build the .NET app ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/OpenMenu.Domain/OpenMenu.Domain.csproj", "src/OpenMenu.Domain/"]
COPY ["src/OpenMenu.Infrastructure/OpenMenu.Infrastructure.csproj", "src/OpenMenu.Infrastructure/"]
COPY ["src/OpenMenu.Application/OpenMenu.Application.csproj", "src/OpenMenu.Application/"]
COPY ["src/OpenMenu.Web.Client/OpenMenu.Web.Client.csproj", "src/OpenMenu.Web.Client/"]
COPY ["src/OpenMenu.Web/OpenMenu.Web.csproj", "src/OpenMenu.Web/"]
RUN dotnet restore src/OpenMenu.Web/OpenMenu.Web.csproj
COPY src/ src/
COPY --from=css /web/OpenMenu.Web/wwwroot/css/app.min.css src/OpenMenu.Web/wwwroot/css/app.min.css
RUN dotnet publish src/OpenMenu.Web/OpenMenu.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Stage 3: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "OpenMenu.Web.dll"]
