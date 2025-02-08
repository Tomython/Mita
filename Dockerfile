# Этап сборки: используем .NET 8.0 SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем файл проекта и восстанавливаем зависимости
COPY MitaBot/MitaBot.csproj MitaBot/
WORKDIR /src/MitaBot
RUN dotnet restore

# Копируем весь исходный код
COPY MitaBot/. .

# Публикуем приложение
RUN dotnet publish -c Release -o /app/publish

# Этап выполнения: используем .NET 8.0 Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MitaBot.dll"]
