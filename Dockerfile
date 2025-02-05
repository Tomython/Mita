# Этап сборки: используем .NET 8.0 SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем файл проекта из папки Mitabot и восстанавливаем зависимости
COPY ["MitaBot/MitaBot.csproj", "MitaBot/"]
RUN dotnet restore "MitaBot/MitaBot.csproj" --verbosity detailed

# Копируем весь исходный код
COPY . .

# Переходим в директорию проекта и публикуем его
WORKDIR "/src/MitaBot"
RUN dotnet publish "MitaBot.csproj" -c Release -o /app/publish

# Этап выполнения: используем .NET 8.0 Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MitaBot.dll"]
