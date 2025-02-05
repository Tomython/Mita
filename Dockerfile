# Этап сборки: используем .NET 8.0 SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем файл проекта из папки Mitabot и восстанавливаем зависимости
COPY ["Mitabot/Mitabot.csproj", "Mitabot/"]
RUN dotnet restore "Mitabot/Mitabot.csproj" --verbosity detailed

# Копируем весь исходный код
COPY . .

# Переходим в директорию проекта и публикуем его
WORKDIR "/src/Mitabot"
RUN dotnet publish "Mitabot.csproj" -c Release -o /app/publish

# Этап выполнения: используем .NET 8.0 Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Mitabot.dll"]
